using Brandora.Web.Data;
using Brandora.Web.Models.Domain;
using Microsoft.EntityFrameworkCore;

namespace Brandora.Web.Services.Payments;

public record PaymentInitiationResult(bool Success, string? RedirectUrl, string? Error);
public record PaymentSettlementResult(bool Success, string? Error, bool AlreadySettled = false);

// Orchestrates the real gateway round trip for a Payment: starts a checkout session with
// IPaymentGateway, then — once bKash redirects the Brand's browser back — verifies and
// executes the payment against bKash's own servers before anything is marked paid.
// PaymentsController owns the IDOR check (a Payment is only ever loaded scoped to the
// current Brand's own campaigns); this service assumes that's already been done and
// operates on the tracked entity it's given.
public class PaymentSettlementService(
    ApplicationDbContext db,
    IPaymentGateway gateway,
    WalletService wallet,
    NotificationService notifications,
    IConfiguration config,
    ILogger<PaymentSettlementService> logger)
{
    // bKash's own sandbox default; overridable via PLATFORM_COMMISSION_PERCENT in .env.
    private decimal CommissionPercent =>
        decimal.TryParse(config["PLATFORM_COMMISSION_PERCENT"], out var pct) ? pct : 10m;

    public async Task<PaymentInitiationResult> InitiatePaymentAsync(Payment payment, string callbackBaseUrl, CancellationToken ct = default)
    {
        if (payment.Status != PaymentStatus.Pending)
        {
            return new PaymentInitiationResult(false, null, "This payment is no longer pending.");
        }

        // Our own internal reference lives under "ref" — deliberately NOT "paymentId".
        // bKash appends its own "paymentID" (capital ID) query parameter to whatever
        // callback URL we give it, and ASP.NET Core's query-string model binding matches
        // parameter names case-insensitively, so "paymentId" would silently collide with
        // bKash's "paymentID" on the way back. "ref" avoids that entirely.
        var callbackUrl = $"{callbackBaseUrl}?ref={payment.Id}";
        var merchantInvoiceNumber = $"BRD{payment.Id}{DateTime.UtcNow:yyyyMMddHHmmssfff}";

        var result = await gateway.CreatePaymentAsync(
            new GatewayCreateRequest(payment.Amount, merchantInvoiceNumber, payment.Id.ToString(), callbackUrl),
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
            return new PaymentInitiationResult(false, null, result.ErrorMessage ?? "Could not start the bKash payment.");
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

    public async Task<PaymentSettlementResult> HandleCallbackAsync(int paymentId, string? gatewayPaymentId, string? bkashStatus, CancellationToken ct = default)
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

        // The attempt bKash's own paymentID must match one WE created for THIS payment —
        // never trust a gatewayPaymentId supplied only via the query string on its own.
        var attempt = payment.Attempts
            .Where(a => a.GatewayPaymentId == gatewayPaymentId && !string.IsNullOrEmpty(gatewayPaymentId))
            .OrderByDescending(a => a.CreatedAt)
            .FirstOrDefault();

        if (attempt is null)
        {
            logger.LogWarning("bKash callback for Payment {PaymentId} carried an unrecognized gatewayPaymentId {GatewayPaymentId}.", paymentId, gatewayPaymentId);
            return new PaymentSettlementResult(false, "This payment session doesn't match our records.");
        }

        if (string.Equals(bkashStatus, "cancel", StringComparison.OrdinalIgnoreCase) ||
            string.Equals(bkashStatus, "failure", StringComparison.OrdinalIgnoreCase))
        {
            attempt.Status = string.Equals(bkashStatus, "cancel", StringComparison.OrdinalIgnoreCase)
                ? PaymentAttemptStatus.Cancelled
                : PaymentAttemptStatus.Failed;
            attempt.FailureReason = $"bKash reported status={bkashStatus}.";
            attempt.CompletedAt = DateTime.UtcNow;
            await db.SaveChangesAsync(ct);
            return new PaymentSettlementResult(false, "The bKash payment was not completed. You can try again.");
        }

        var execute = await gateway.ExecutePaymentAsync(gatewayPaymentId!, ct);

        if (!execute.Success)
        {
            attempt.Status = PaymentAttemptStatus.Failed;
            attempt.FailureReason = execute.ErrorMessage;
            attempt.ExecuteResponseJson = execute.RawResponseJson;
            attempt.CompletedAt = DateTime.UtcNow;
            await db.SaveChangesAsync(ct);
            return new PaymentSettlementResult(false, execute.ErrorMessage ?? "bKash could not confirm this payment.");
        }

        var fee = Math.Round(payment.Amount * CommissionPercent / 100m, 2);
        var net = payment.Amount - fee;

        payment.Status = PaymentStatus.Completed;
        payment.PaidAt = DateTime.UtcNow;
        payment.Method = PaymentMethod.Bkash;
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
