using System.Globalization;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Text.RegularExpressions;
using Brandora.Web.Models.Domain;

namespace Brandora.Web.Services.Payments;

// Real integration against Nagad's Online Payment (check-out) API. Flow:
//   1. check-out/initialize/{merchantId}/{orderId} — RSA-encrypted handshake; Nagad answers
//      with a paymentReferenceId + challenge (encrypted to OUR public key).
//   2. check-out/complete/{paymentReferenceId} — sends the amount and our callback URL;
//      Nagad answers with its hosted payment page URL, where the Brand pays.
//   3. Nagad redirects the Brand back to NagadCallback with payment_ref_id + status.
//   4. verify/payment/{paymentReferenceId} — a server-to-server check with Nagad. Only a
//      "Success" here, for the right amount and order, marks anything paid.
//
// Nagad does not publish shared sandbox credentials: each merchant receives a merchant
// ID and key pair from Nagad (and gets its server IP / callback domain whitelisted).
// Until NAGAD_MERCHANT_ID / NAGAD_MERCHANT_PRIVATE_KEY / NAGAD_PG_PUBLIC_KEY are set,
// IsConfigured is false and the Nagad option is shown as unavailable — it never fakes a
// successful checkout.
public class NagadGateway : IPaymentGateway
{
    private readonly HttpClient http;
    private readonly IConfiguration config;
    private readonly ILogger<NagadGateway> logger;

    private static readonly JsonSerializerOptions JsonOpts = new() { PropertyNameCaseInsensitive = true };

    public string Name => "Nagad";
    public PaymentMethod Method => PaymentMethod.Nagad;

    public bool IsConfigured =>
        !string.IsNullOrWhiteSpace(config["NAGAD_MERCHANT_ID"]) &&
        !string.IsNullOrWhiteSpace(config["NAGAD_MERCHANT_PRIVATE_KEY"]) &&
        !string.IsNullOrWhiteSpace(config["NAGAD_PG_PUBLIC_KEY"]);

    public NagadGateway(HttpClient http, IConfiguration config, ILogger<NagadGateway> logger)
    {
        this.http = http;
        this.config = config;
        this.logger = logger;

        if (this.http.BaseAddress is null)
        {
            var baseUrl = config["NAGAD_BASE_URL"] ?? "http://sandbox.mynagad.com:10080/remote-payment-gateway-1.0/api/dfs";
            this.http.BaseAddress = new Uri(baseUrl.TrimEnd('/') + "/");
            this.http.Timeout = TimeSpan.FromSeconds(30);
        }
    }

    public async Task<GatewayCreateResult> CreatePaymentAsync(GatewayCreateRequest request, CancellationToken ct = default)
    {
        var merchantId = config["NAGAD_MERCHANT_ID"]?.Trim();
        using var merchantKey = LoadPrivateKey(config["NAGAD_MERCHANT_PRIVATE_KEY"]);
        using var nagadKey = LoadPublicKey(config["NAGAD_PG_PUBLIC_KEY"]);

        if (string.IsNullOrEmpty(merchantId) || merchantKey is null || nagadKey is null)
        {
            logger.LogError("Nagad is not configured, or its keys could not be read (NAGAD_MERCHANT_ID / NAGAD_MERCHANT_PRIVATE_KEY / NAGAD_PG_PUBLIC_KEY).");
            return new GatewayCreateResult(false, null, null, null, "Nagad isn't set up on this server yet. Please pay with bKash, or try Nagad later.");
        }

        var orderId = request.MerchantInvoiceNumber;
        var clientIp = request.ClientIp ?? "127.0.0.1";

        // ---- 1. Initialize ------------------------------------------------------------
        var dateTime = BangladeshNow().ToString("yyyyMMddHHmmss", CultureInfo.InvariantCulture);
        var initPlain = JsonSerializer.Serialize(new
        {
            merchantId,
            datetime = dateTime,
            orderId,
            challenge = RandomChallenge()
        });

        var initBody = new Dictionary<string, string>
        {
            ["dateTime"] = dateTime,
            ["sensitiveData"] = Encrypt(nagadKey, initPlain),
            ["signature"] = Sign(merchantKey, initPlain)
        };
        var merchantNumber = config["NAGAD_MERCHANT_NUMBER"];
        if (!string.IsNullOrWhiteSpace(merchantNumber))
        {
            initBody["accountNumber"] = merchantNumber.Trim();
        }

        var (initOk, initRaw) = await PostAsync($"check-out/initialize/{Uri.EscapeDataString(merchantId)}/{Uri.EscapeDataString(orderId)}", initBody, clientIp, ct);
        var init = TryDeserialize<NagadSealedResponse>(initRaw);

        if (!initOk || string.IsNullOrEmpty(init?.sensitiveData))
        {
            logger.LogError("Nagad initialize failed: {Body}", initRaw);
            return new GatewayCreateResult(false, null, null, initRaw, NagadError(init?.message ?? init?.reason, "Nagad could not start the checkout."));
        }

        string handshakePlain;
        try
        {
            handshakePlain = Encoding.UTF8.GetString(merchantKey.Decrypt(Convert.FromBase64String(init.sensitiveData), RSAEncryptionPadding.Pkcs1));
        }
        catch (Exception ex) when (ex is CryptographicException or FormatException)
        {
            logger.LogError(ex, "Could not decrypt Nagad's initialize response — the merchant private key probably doesn't match the one registered with Nagad.");
            return new GatewayCreateResult(false, null, null, initRaw, "Nagad's response could not be read. Please pay with bKash, or try Nagad later.");
        }

        if (!string.IsNullOrEmpty(init.signature) && !VerifySignature(nagadKey, handshakePlain, init.signature))
        {
            // Not fatal on its own: nothing is trusted from this step except the reference
            // we hand straight back to Nagad; the verify step is the real check.
            logger.LogWarning("Nagad initialize response signature did not verify against NAGAD_PG_PUBLIC_KEY.");
        }

        var handshake = TryDeserialize<NagadHandshake>(handshakePlain);
        if (string.IsNullOrEmpty(handshake?.paymentReferenceId) || string.IsNullOrEmpty(handshake.challenge))
        {
            logger.LogError("Nagad initialize response had no paymentReferenceId/challenge.");
            return new GatewayCreateResult(false, null, null, initRaw, "Nagad could not start the checkout.");
        }

        // ---- 2. Complete (place the order, get the hosted payment page) ----------------
        var orderPlain = JsonSerializer.Serialize(new
        {
            merchantId,
            orderId,
            currencyCode = "050", // BDT (ISO 4217 numeric)
            amount = request.Amount.ToString("0.00", CultureInfo.InvariantCulture),
            challenge = handshake.challenge
        });

        var completeBody = new Dictionary<string, string>
        {
            ["sensitiveData"] = Encrypt(nagadKey, orderPlain),
            ["signature"] = Sign(merchantKey, orderPlain),
            ["merchantCallbackURL"] = request.CallbackUrl
        };

        var (completeOk, completeRaw) = await PostAsync($"check-out/complete/{Uri.EscapeDataString(handshake.paymentReferenceId)}", completeBody, clientIp, ct);
        var complete = TryDeserialize<NagadCompleteResponse>(completeRaw);

        if (!completeOk || !string.Equals(complete?.status, "Success", StringComparison.OrdinalIgnoreCase) || string.IsNullOrEmpty(complete?.callBackUrl))
        {
            logger.LogError("Nagad complete-order failed: {Body}", completeRaw);
            return new GatewayCreateResult(false, null, null, completeRaw, NagadError(complete?.message ?? complete?.reason, "Nagad could not start the checkout."));
        }

        return new GatewayCreateResult(true, handshake.paymentReferenceId, complete.callBackUrl, completeRaw, null);
    }

    public async Task<GatewayExecuteResult> ExecutePaymentAsync(string gatewayPaymentId, CancellationToken ct = default)
    {
        if (!IsConfigured)
        {
            return new GatewayExecuteResult(false, null, null, null, "Nagad isn't set up on this server, so this payment can't be confirmed.");
        }

        string body;
        HttpResponseMessage response;
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Get, $"verify/payment/{Uri.EscapeDataString(gatewayPaymentId)}");
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            response = await http.SendAsync(request, ct);
            body = await response.Content.ReadAsStringAsync(ct);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            logger.LogError(ex, "Nagad verify-payment request failed for {PaymentRef}.", gatewayPaymentId);
            return new GatewayExecuteResult(false, null, null, null, "Nagad couldn't be reached to confirm this payment. Nothing has been marked paid — please try again.");
        }

        var verify = TryDeserialize<NagadVerifyResponse>(body);

        var succeeded = response.IsSuccessStatusCode &&
            string.Equals(verify?.status, "Success", StringComparison.OrdinalIgnoreCase) &&
            (string.IsNullOrEmpty(verify?.paymentRefId) || verify.paymentRefId == gatewayPaymentId);

        if (!succeeded)
        {
            logger.LogWarning("Nagad verify payment not successful: {Body}", body);
            var message = verify?.status is { Length: > 0 } status
                ? $"Nagad reports this payment as {status}."
                : "Nagad could not confirm this payment.";
            return new GatewayExecuteResult(false, verify?.issuerPaymentRefNo, null, body, message, verify?.orderId);
        }

        decimal? amount = decimal.TryParse(verify!.amount, NumberStyles.Number, CultureInfo.InvariantCulture, out var parsed) ? parsed : null;
        return new GatewayExecuteResult(true, verify.issuerPaymentRefNo ?? verify.paymentRefId, amount, body, null, verify.orderId);
    }

    // ---- helpers -------------------------------------------------------------------------

    private async Task<(bool Ok, string Body)> PostAsync(string path, Dictionary<string, string> payload, string clientIp, CancellationToken ct)
    {
        try
        {
            using var request = new HttpRequestMessage(HttpMethod.Post, path);
            request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            request.Headers.Add("X-KM-Api-Version", "v-0.2.0");
            request.Headers.Add("X-KM-IP-V4", clientIp);
            request.Headers.Add("X-KM-Client-Type", "PC_WEB");
            request.Content = new StringContent(JsonSerializer.Serialize(payload), Encoding.UTF8, "application/json");

            using var response = await http.SendAsync(request, ct);
            var body = await response.Content.ReadAsStringAsync(ct);
            return (response.IsSuccessStatusCode, body);
        }
        catch (Exception ex) when (ex is HttpRequestException or TaskCanceledException)
        {
            logger.LogError(ex, "Nagad request to {Path} failed.", path);
            return (false, JsonSerializer.Serialize(new { reason = "unreachable", message = "Nagad couldn't be reached." }));
        }
    }

    private static string NagadError(string? providerMessage, string fallback) =>
        string.IsNullOrWhiteSpace(providerMessage) ? fallback : $"Nagad: {providerMessage.Trim()}";

    private static DateTime BangladeshNow() => DateTime.UtcNow.AddHours(6);

    private static string RandomChallenge() => Convert.ToHexString(RandomNumberGenerator.GetBytes(20)).ToLowerInvariant();

    private static string Encrypt(RSA nagadPublicKey, string plain) =>
        Convert.ToBase64String(nagadPublicKey.Encrypt(Encoding.UTF8.GetBytes(plain), RSAEncryptionPadding.Pkcs1));

    private static string Sign(RSA merchantPrivateKey, string plain) =>
        Convert.ToBase64String(merchantPrivateKey.SignData(Encoding.UTF8.GetBytes(plain), HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1));

    private static bool VerifySignature(RSA nagadPublicKey, string plain, string signatureBase64)
    {
        try
        {
            return nagadPublicKey.VerifyData(Encoding.UTF8.GetBytes(plain), Convert.FromBase64String(signatureBase64), HashAlgorithmName.SHA256, RSASignaturePadding.Pkcs1);
        }
        catch (FormatException)
        {
            return false;
        }
    }

    // Keys may be given as a full PEM block, or — as Nagad's merchant portal hands them
    // out — as the bare base64 body (PKCS#1 or PKCS#8 for the private key). "\n"
    // sequences from a single-line .env value are accepted too.
    private static RSA? LoadPrivateKey(string? value) => LoadKey(value, isPrivate: true);
    private static RSA? LoadPublicKey(string? value) => LoadKey(value, isPrivate: false);

    private static RSA? LoadKey(string? value, bool isPrivate)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var rsa = RSA.Create();
        try
        {
            var text = value.Replace("\\n", "\n").Trim().Trim('"');
            if (text.Contains("-----BEGIN", StringComparison.Ordinal))
            {
                rsa.ImportFromPem(text);
                return rsa;
            }

            var der = Convert.FromBase64String(Regex.Replace(text, @"\s+", ""));
            if (isPrivate)
            {
                try { rsa.ImportRSAPrivateKey(der, out _); }
                catch (CryptographicException) { rsa.ImportPkcs8PrivateKey(der, out _); }
            }
            else
            {
                try { rsa.ImportSubjectPublicKeyInfo(der, out _); }
                catch (CryptographicException) { rsa.ImportRSAPublicKey(der, out _); }
            }
            return rsa;
        }
        catch (Exception ex) when (ex is CryptographicException or FormatException or ArgumentException)
        {
            rsa.Dispose();
            return null;
        }
    }

    private static T? TryDeserialize<T>(string? body) where T : class
    {
        if (string.IsNullOrWhiteSpace(body))
        {
            return null;
        }

        try
        {
            return JsonSerializer.Deserialize<T>(body, JsonOpts);
        }
        catch (JsonException)
        {
            return null;
        }
    }

    private sealed class NagadSealedResponse
    {
        public string? sensitiveData { get; set; }
        public string? signature { get; set; }
        public string? reason { get; set; }
        public string? message { get; set; }
    }

    private sealed class NagadHandshake
    {
        public string? paymentReferenceId { get; set; }
        public string? challenge { get; set; }
        public string? acceptDateTime { get; set; }
    }

    private sealed class NagadCompleteResponse
    {
        public string? status { get; set; }
        public string? callBackUrl { get; set; }
        public string? reason { get; set; }
        public string? message { get; set; }
    }

    private sealed class NagadVerifyResponse
    {
        public string? merchantId { get; set; }
        public string? orderId { get; set; }
        public string? paymentRefId { get; set; }
        public string? amount { get; set; }
        public string? status { get; set; }
        public string? statusCode { get; set; }
        public string? issuerPaymentRefNo { get; set; }
        public string? issuerPaymentDateTime { get; set; }
    }
}
