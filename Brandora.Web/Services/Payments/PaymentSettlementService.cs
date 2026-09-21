using Brandora.Web.Data;
using Brandora.Web.Models.Domain;
using Microsoft.EntityFrameworkCore;

namespace Brandora.Web.Services.Payments;

public record PaymentInitiationResult(bool Success, string? RedirectUrl, string? Error);
public record PaymentSettlementResult(bool Success, string? Error, bool AlreadySettled = false);

// Orchestrates the real gateway round trip for a Payment: starts a checkout session with
// whichever IPaymentGateway the Brand picked (bKash or Nagad), then — once that provider
// redirects the Brand's browser back — verifies the payment against the provider's own
// servers before anything is marked paid.
// PaymentsController owns the IDOR check (a Payment is only ever loaded scoped to the
// current Brand's own campaigns); this service assumes that's already been done and
// operates on the tracked entity it's given.
public class PaymentSettlementService(
    ApplicationDbContext db,
    IEnumerable<IPaymentGateway> gateways,
    WalletService wallet,
    NotificationService notifications,
    IConfiguration config,
    ILogger<PaymentSettlementService> logger)
{
    // Platform default; overridable via PLATFORM_COMMISSION_PERCENT in .env.
    private decimal CommissionPercent =>
        decimal.TryParse(config["PLATFORM_COMMISSION_PERCENT"], out var pct) ? pct : 10m;

    public IReadOnlyList<IPaymentGateway> Gateways => gateways.ToList();

    public IPaymentGateway? GatewayFor(PaymentMethod method) => gateways.FirstOrDefault(g => g.Method == method);

    private IPaymentGateway? GatewayNamed(string? name) =>
        gateways.FirstOrDefault(g => string.Equals(g.Name, name, StringComparison.OrdinalIgnoreCase));

    // Our order/invoice number for a checkout: "BRD{paymentId}T{base36 unix seconds}".
    // Short enough for Nagad's order-id limit, unique per attempt, and it carries the
    // Payment id so a verified transaction can be matched back to exactly one Payment.
    private static string OrderNumberFor(Payment payment)
    {
        const string digits = "0123456789ABCDEFGHIJKLMNOPQRSTUVWXYZ";
        var n = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var stamp = "";
        do { stamp = digits[(int)(n % 36)] + stamp; n /= 36; } while (n > 0);
        return $"BRD{payment.Id}T{stamp}";
    }

    // callbackUrl is built by the controller per provider (bKash's carries our "ref";
    // Nagad appends its own query string, so its callback URL carries none).
    public async Task<PaymentInitiationResult> InitiatePaymentAsync(Payment payment, PaymentMethod method, string callbackUrl, string? clientIp, CancellationToken ct = default)
    {
        if (payment.Status != PaymentStatus.Pending)
        {
            return new PaymentInitiationResult(false, null, "This payment is no longer pending.");
        }

        var gateway = GatewayFor(method);
        if (gateway is null)
        {
            return new PaymentInitiationResult(false, null, "That payment method isn't supported.");
        }

        if (!gateway.IsConfigured)
        {
            return new PaymentInitiationResult(false, null, $"{gateway.Name} isn't set up on this server yet. Please choose another payment method.");
        }

        var merchantInvoiceNumber = OrderNumberFor(payment);

        var result = await gateway.CreatePaymentAsync(
            new GatewayCreateRequest(payment.Amount, merchantInvoiceNumber, payment.Id.ToString(), callbackUrl, clientIp),
            ct);

        if (!result.Success || result.GatewayPaymentId is null)
        {
            db.PaymentAttempts.Add(new PaymentAttempt
            {
                PaymentId = payment.Id,
                Gateway = gateway.Name,
                Status = PaymentAttemptStatus.Failed,
                FailureReason = result.ErrorMessage,
                CreateResponseJson = result.RawResponseJson
            });
            await db.SaveChangesAsync(ct);
            return new PaymentInitiationResult(false, null, result.ErrorMessage ?? $"Could not start the {gateway.Name} payment.");
        }

        payment.GatewayPaymentId = result.GatewayPaymentId;
        db.PaymentAttempts.Add(new PaymentAttempt
        {
            PaymentId = payment.Id,
            Gateway = gateway.Name,
            GatewayPaymentId = result.GatewayPaymentId,
            Status = PaymentAttemptStatus.AwaitingCustomer,
            CreateResponseJson = result.RawResponseJson
        });
        await db.SaveChangesAsync(ct);

        return new PaymentInitiationResult(true, result.RedirectUrl, null);
    }

    // Nagad's callback identifies the checkout only by its own payment_ref_id; map it back
    // to the Payment through the attempt WE recorded when the checkout was started.
    public async Task<int?> FindPaymentIdForAttemptAsync(string gatewayName, string? gatewayPaymentId, CancellationToken ct = default)
    {
        if (string.IsNullOrEmpty(gatewayPaymentId))
        {
            return null;
        }

        return await db.PaymentAttempts
            .Where(a => a.Gateway == gatewayName && a.GatewayPaymentId == gatewayPaymentId)
            .OrderByDescending(a => a.CreatedAt)
            .Select(a => (int?)a.PaymentId)
            .FirstOrDefaultAsync(ct);
    }

    // What the provider's browser redirect says about the checkout. Only ever used to
    // short-circuit a cancelled/failed checkout — a "success" here is never trusted; the
    // provider's own verify/execute API decides that.
    private static PaymentAttemptStatus? ReturnedAsUnsuccessful(IPaymentGateway gateway, string? status)
    {
        if (string.IsNullOrWhiteSpace(status))
        {
            return null;
        }

        if (status.Equals("cancel", StringComparison.OrdinalIgnoreCase) ||
            status.Equals("cancelled", StringComparison.OrdinalIgnoreCase) ||
            status.Equals("aborted", StringComparison.OrdinalIgnoreCase))
        {
            return PaymentAttemptStatus.Cancelled;
        }

        if (gateway.Method == PaymentMethod.Bkash)
        {
            return status.Equals("failure", StringComparison.OrdinalIgnoreCase) ? PaymentAttemptStatus.Failed : null;
        }

        // Nagad reports "Success" or a failure word (Failed, InvalidRequest, Fraud, ...).
        return status.Equals("success", StringComparison.OrdinalIgnoreCase) ? null : PaymentAttemptStatus.Failed;
    }

    public async Task<PaymentSettlementResult> HandleCallbackAsync(int paymentId, string? gatewayPaymentId, string? returnedStatus, CancellationToken ct = default)
    {
        var payment = await db.Payments
            .Include(p => p.Collaboration).ThenInclude(c => c.Campaign)
            .Include(p => p.Collaboration).ThenInclude(c => c.InfluencerProfile)
            .Include(p => p.Milestone)
            .Include(p => p.Attempts)
            .FirstOrDefaultAsync(p => p.Id == paymentId, ct);

        if (payment is null)
        {
            return new PaymentSettlementResult(false, "Payment not found.");
        }

        if (payment.Status == PaymentStatus.Completed)
        {
            // The Brand can land back on this callback more than once (a refresh, a
            // double redirect) — settlement already happened, so this is success, not
            // an error, and nothing is applied a second time.
            return new PaymentSettlementResult(true, null, AlreadySettled: true);
        }

        // The provider's own payment id must match an attempt WE created for THIS payment —
        // never trust a gatewayPaymentId supplied only via the query string on its own.
        var attempt = payment.Attempts
            .Where(a => a.GatewayPaymentId == gatewayPaymentId && !string.IsNullOrEmpty(gatewayPaymentId))
            .OrderByDescending(a => a.CreatedAt)
            .FirstOrDefault();

        if (attempt is null)
        {
            logger.LogWarning("Payment callback for Payment {PaymentId} carried an unrecognized gatewayPaymentId {GatewayPaymentId}.", paymentId, gatewayPaymentId);
            return new PaymentSettlementResult(false, "This payment session doesn't match our records.");
        }

        // Settle only with the provider that started this attempt.
        var gateway = GatewayNamed(attempt.Gateway);
        if (gateway is null)
        {
            logger.LogError("No payment gateway named {Gateway} is registered (Payment {PaymentId}).", attempt.Gateway, paymentId);
            return new PaymentSettlementResult(false, "This payment method is no longer available. Please start the payment again.");
        }

        var unsuccessful = ReturnedAsUnsuccessful(gateway, returnedStatus);
        if (unsuccessful is not null)
        {
            attempt.Status = unsuccessful.Value;
            attempt.FailureReason = $"{gateway.Name} reported status={returnedStatus}.";
            attempt.CompletedAt = DateTime.UtcNow;
            await db.SaveChangesAsync(ct);
            return new PaymentSettlementResult(false, unsuccessful == PaymentAttemptStatus.Cancelled
                ? $"The {gateway.Name} checkout was cancelled — nothing was charged. You can pay again anytime."
                : $"The {gateway.Name} payment was not completed. You can try again.");
        }

        var execute = await gateway.ExecutePaymentAsync(gatewayPaymentId!, ct);

        // Even a "successful" confirmation must be for this payment's exact amount and,
        // where the provider reports it, for the order number we issued for this payment.
        if (execute.Success)
        {
            if (execute.Amount is { } confirmedAmount && confirmedAmount != payment.Amount)
            {
                logger.LogError("{Gateway} confirmed ৳{Confirmed} for Payment {PaymentId}, expected ৳{Expected}.", gateway.Name, confirmedAmount, paymentId, payment.Amount);
                execute = execute with { Success = false, ErrorMessage = $"{gateway.Name} confirmed a different amount than this milestone. Nothing was marked paid — please contact support." };
            }
            else if (execute.MerchantReference is { Length: > 0 } reference && !reference.StartsWith($"BRD{payment.Id}T", StringComparison.Ordinal))
            {
                logger.LogError("{Gateway} confirmation for Payment {PaymentId} carried order {Reference}.", gateway.Name, paymentId, reference);
                execute = execute with { Success = false, ErrorMessage = $"{gateway.Name}'s confirmation doesn't match this payment. Nothing was marked paid — please contact support." };
            }
        }

        if (!execute.Success)
        {
            attempt.Status = PaymentAttemptStatus.Failed;
            attempt.FailureReason = execute.ErrorMessage;
            attempt.ExecuteResponseJson = execute.RawResponseJson;
            attempt.CompletedAt = DateTime.UtcNow;
            await db.SaveChangesAsync(ct);
            return new PaymentSettlementResult(false, execute.ErrorMessage ?? $"{gateway.Name} could not confirm this payment.");
        }

        var fee = Math.Round(payment.Amount * CommissionPercent / 100m, 2);
        var net = payment.Amount - fee;

        payment.Status = PaymentStatus.Completed;
        payment.PaidAt = DateTime.UtcNow;
        payment.Method = gateway.Method;
        payment.TransactionReference = execute.GatewayTransactionId;
        payment.PlatformFeeAmount = fee;
        payment.NetAmount = net;

        attempt.Status = PaymentAttemptStatus.Completed;
        attempt.GatewayTransactionId = execute.GatewayTransactionId;
        attempt.ExecuteResponseJson = execute.RawResponseJson;
        attempt.CompletedAt = DateTime.UtcNow;

        if (payment.Milestone is not null)
        {
            payment.Milestone.Status = MilestoneStatus.Paid;
        }

        // Gross budget consumption — matches how SpentAmount was tracked before this
        // integration existed; the platform's commission is a separate concern from what
        // the Brand actually spent against their campaign budget.
        payment.Collaboration.Campaign.SpentAmount += payment.Amount;

        await wallet.CreditEarningAsync(
            payment.Collaboration.InfluencerProfileId,
            payment.Id,
            net,
            $"Payment for \"{payment.Collaboration.Campaign.Title}\"");

        await notifications.NotifyAsync(
            payment.Collaboration.InfluencerProfile.UserId,
            "Payment",
            "Payment received",
            $"৳{net:N0} credited to your wallet for {payment.Collaboration.Campaign.Title}.",
            "/InfluencerPayments");

        await db.SaveChangesAsync(ct);

        return new PaymentSettlementResult(true, null);
    }
}
