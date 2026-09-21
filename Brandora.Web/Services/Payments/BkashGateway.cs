using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace Brandora.Web.Services.Payments;

// Real integration against bKash's Tokenized Checkout Sandbox API
// (https://developer.bka.sh — publicly documented sandbox, no merchant agreement needed
// for testing). A Payment only ever reaches "Completed" through an actual
// server-to-server call to bKash's own Execute Payment endpoint here — the browser
// redirect back from bKash is never, on its own, treated as proof of payment
// (see PaymentSettlementService.HandleCallbackAsync).
public class BkashGateway : IPaymentGateway
{
    private readonly HttpClient http;
    private readonly IConfiguration config;
    private readonly ILogger<BkashGateway> logger;

    // Grants live ~1 hour; cached process-wide and refreshed on demand instead of
    // re-granted on every call. Static + a static lock so the cache is shared across the
    // short-lived typed-client instances DI hands out.
    private static string? cachedToken;
    private static DateTime cachedTokenExpiresAt = DateTime.MinValue;
    private static readonly SemaphoreSlim TokenLock = new(1, 1);
    private static readonly JsonSerializerOptions JsonOpts = new() { PropertyNameCaseInsensitive = true };

    public string Name => "bKash";

    public BkashGateway(HttpClient http, IConfiguration config, ILogger<BkashGateway> logger)
    {
        this.http = http;
        this.config = config;
        this.logger = logger;

        if (this.http.BaseAddress is null)
        {
            var baseUrl = config["BKASH_BASE_URL"] ?? "https://tokenized.sandbox.bka.sh/v1.2.0-beta";
            this.http.BaseAddress = new Uri(baseUrl.TrimEnd('/') + "/");
        }
    }

    private async Task<string?> GetTokenAsync(CancellationToken ct)
    {
        if (cachedToken is not null && DateTime.UtcNow < cachedTokenExpiresAt)
        {
            return cachedToken;
        }

        await TokenLock.WaitAsync(ct);
        try
        {
            if (cachedToken is not null && DateTime.UtcNow < cachedTokenExpiresAt)
            {
                return cachedToken;
            }

            var appKey = config["BKASH_APP_KEY"];
            var appSecret = config["BKASH_APP_SECRET"];
            var username = config["BKASH_USERNAME"];
            var password = config["BKASH_PASSWORD"];

            if (string.IsNullOrEmpty(appKey) || string.IsNullOrEmpty(appSecret) ||
                string.IsNullOrEmpty(username) || string.IsNullOrEmpty(password))
            {
                logger.LogError("bKash credentials are not configured (BKASH_APP_KEY / BKASH_APP_SECRET / BKASH_USERNAME / BKASH_PASSWORD).");
                return null;
            }

            using var request = new HttpRequestMessage(HttpMethod.Post, "tokenized/checkout/token/grant");
            request.Headers.Add("username", username);
            request.Headers.Add("password", password);
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            request.Content = JsonContent.Create(new { app_key = appKey, app_secret = appSecret });

            using var response = await http.SendAsync(request, ct);
            var body = await response.Content.ReadAsStringAsync(ct);

            if (!response.IsSuccessStatusCode)
            {
                logger.LogError("bKash token grant failed ({Status}): {Body}", response.StatusCode, body);
                return null;
            }

            var grant = TryDeserialize<BkashGrantResponse>(body);
            if (grant?.id_token is null)
            {
                logger.LogError("bKash token grant returned no id_token: {Body}", body);
                return null;
            }

            cachedToken = grant.id_token;
            var expiresIn = grant.expires_in > 60 ? grant.expires_in : 3300;
            cachedTokenExpiresAt = DateTime.UtcNow.AddSeconds(expiresIn - 120);

            return cachedToken;
        }
        finally
        {
            TokenLock.Release();
        }
    }

    public async Task<GatewayCreateResult> CreatePaymentAsync(GatewayCreateRequest request, CancellationToken ct = default)
    {
        var token = await GetTokenAsync(ct);
        var appKey = config["BKASH_APP_KEY"];
        if (token is null || string.IsNullOrEmpty(appKey))
        {
            return new GatewayCreateResult(false, null, null, null, "bKash is not configured, or the sandbox token could not be obtained.");
        }

        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, "tokenized/checkout/create");
        httpRequest.Headers.Add("Authorization", token);
        httpRequest.Headers.Add("X-APP-Key", appKey);
        httpRequest.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        httpRequest.Content = JsonContent.Create(new
        {
            mode = "0011",
            payerReference = request.PayerReference,
            callbackURL = request.CallbackUrl,
            amount = request.Amount.ToString("F2"),
            currency = "BDT",
            intent = "sale",
            merchantInvoiceNumber = request.MerchantInvoiceNumber
        });

        using var response = await http.SendAsync(httpRequest, ct);
        var body = await response.Content.ReadAsStringAsync(ct);
        var create = TryDeserialize<BkashCreateResponse>(body);

        if (!response.IsSuccessStatusCode || create?.paymentID is null)
        {
            var message = create?.statusMessage ?? $"bKash create-payment failed ({response.StatusCode}).";
            logger.LogError("bKash create payment failed: {Body}", body);
            return new GatewayCreateResult(false, null, null, body, message);
        }

        return new GatewayCreateResult(true, create.paymentID, create.bkashURL, body, null);
    }

    public async Task<GatewayExecuteResult> ExecutePaymentAsync(string gatewayPaymentId, CancellationToken ct = default)
    {
        var token = await GetTokenAsync(ct);
        var appKey = config["BKASH_APP_KEY"];
        if (token is null || string.IsNullOrEmpty(appKey))
        {
            return new GatewayExecuteResult(false, null, null, null, "bKash is not configured, or the sandbox token could not be obtained.");
        }

        using var httpRequest = new HttpRequestMessage(HttpMethod.Post, "tokenized/checkout/execute");
        httpRequest.Headers.Add("Authorization", token);
        httpRequest.Headers.Add("X-APP-Key", appKey);
        httpRequest.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        httpRequest.Content = JsonContent.Create(new { paymentID = gatewayPaymentId });

        using var response = await http.SendAsync(httpRequest, ct);
        var body = await response.Content.ReadAsStringAsync(ct);
        var execute = TryDeserialize<BkashExecuteResponse>(body);

        var succeeded = response.IsSuccessStatusCode &&
            string.Equals(execute?.transactionStatus, "Completed", StringComparison.OrdinalIgnoreCase);

        if (!succeeded)
        {
            var message = execute?.statusMessage ?? $"bKash execute-payment did not complete ({response.StatusCode}).";
            logger.LogWarning("bKash execute payment not completed: {Body}", body);
            return new GatewayExecuteResult(false, execute?.trxID, null, body, message);
        }

        decimal? amount = decimal.TryParse(execute!.amount, out var parsedAmount) ? parsedAmount : null;
        return new GatewayExecuteResult(true, execute.trxID, amount, body, null);
    }

    private static T? TryDeserialize<T>(string body) where T : class
    {
        try
        {
            return JsonSerializer.Deserialize<T>(body, JsonOpts);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private sealed class BkashGrantResponse
    {
        public string? id_token { get; set; }
        public string? token_type { get; set; }
        public int expires_in { get; set; }
        public string? refresh_token { get; set; }
    }

    private sealed class BkashCreateResponse
    {
        public string? paymentID { get; set; }
        public string? bkashURL { get; set; }
        public string? statusCode { get; set; }
        public string? statusMessage { get; set; }
    }

    private sealed class BkashExecuteResponse
    {
        public string? paymentID { get; set; }
        public string? trxID { get; set; }
        public string? transactionStatus { get; set; }
        public string? amount { get; set; }
        public string? statusCode { get; set; }
        public string? statusMessage { get; set; }
    }
}
