namespace Brandora.Web.Services.Payments;

public interface IPaymentGateway
{
    string Name { get; }
    Task<GatewayCreateResult> CreatePaymentAsync(GatewayCreateRequest request, CancellationToken ct = default);
    Task<GatewayExecuteResult> ExecutePaymentAsync(string gatewayPaymentId, CancellationToken ct = default);
}

public record GatewayCreateRequest(decimal Amount, string MerchantInvoiceNumber, string PayerReference, string CallbackUrl);
public record GatewayCreateResult(bool Success, string? GatewayPaymentId, string? RedirectUrl, string? RawResponseJson, string? ErrorMessage);
public record GatewayExecuteResult(bool Success, string? GatewayTransactionId, decimal? Amount, string? RawResponseJson, string? ErrorMessage);
