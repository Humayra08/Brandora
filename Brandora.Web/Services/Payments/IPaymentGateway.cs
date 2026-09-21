using Brandora.Web.Models.Domain;

namespace Brandora.Web.Services.Payments;

// One implementation per real checkout provider (BkashGateway, NagadGateway). All of them
// are registered side by side; PaymentSettlementService picks the one the Brand chose at
// checkout, and — on the way back — the one that created the PaymentAttempt, so a
// callback can never be settled by a different provider than the one that started it.
public interface IPaymentGateway
{
    // Stored on PaymentAttempt.Gateway and shown to the Brand ("bKash", "Nagad").
    string Name { get; }

    // Recorded on Payment.Method once this gateway settles a payment.
    PaymentMethod Method { get; }

    // False when this server has no merchant credentials for the provider — the option is
    // then shown as unavailable instead of failing mid-checkout.
    bool IsConfigured { get; }

    Task<GatewayCreateResult> CreatePaymentAsync(GatewayCreateRequest request, CancellationToken ct = default);

    // Server-to-server confirmation with the provider. This — never the browser
    // redirect — is what decides whether money actually moved.
    Task<GatewayExecuteResult> ExecutePaymentAsync(string gatewayPaymentId, CancellationToken ct = default);
}

public record GatewayCreateRequest(decimal Amount, string MerchantInvoiceNumber, string PayerReference, string CallbackUrl, string? ClientIp = null);
public record GatewayCreateResult(bool Success, string? GatewayPaymentId, string? RedirectUrl, string? RawResponseJson, string? ErrorMessage);

// MerchantReference: the order/invoice number the provider reports back for the verified
// transaction, when it reports one — checked against the Payment it's being applied to.
public record GatewayExecuteResult(bool Success, string? GatewayTransactionId, decimal? Amount, string? RawResponseJson, string? ErrorMessage, string? MerchantReference = null);
