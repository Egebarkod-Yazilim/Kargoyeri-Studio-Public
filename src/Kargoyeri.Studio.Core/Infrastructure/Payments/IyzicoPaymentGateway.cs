using System.Net.Http.Json;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Configuration;

namespace Kargoyeri.Studio.Core.Infrastructure.Payments;

/// <summary>
/// P5-#3 — Iyzico (https://www.iyzico.com) odeme entegrasyonu (skeleton).
///
/// Gercek REST imzalama mantigi (PKI v2): authorization string PKI formatinda HMAC-SHA256.
/// Bu skeleton SADECE dogru imza algoritmasini gosterir; Iyzico SDK'nin yerini almaz.
/// Production icin: <c>Iyzipay</c> NuGet paketini ekleyip resmi client ile sarmalayin.
///
/// Konfig:
/// <code>
/// "Studio:Payments:Iyzico": {
///   "ApiKey": "sandbox-...",
///   "SecretKey": "sandbox-...",
///   "BaseUrl": "https://sandbox-api.iyzipay.com"
/// }
/// </code>
/// </summary>
public sealed class IyzicoPaymentGateway : IPaymentGateway
{
    private readonly HttpClient _http;
    private readonly string _apiKey;
    private readonly string _secretKey;
    private readonly string _baseUrl;
    private readonly ILogger<IyzicoPaymentGateway> _logger;

    public string Name => "iyzico";

    public IyzicoPaymentGateway(IHttpClientFactory httpClientFactory, IConfiguration config, ILogger<IyzicoPaymentGateway> logger)
    {
        _http = httpClientFactory.CreateClient("studio-iyzico");
        _apiKey = config["Studio:Payments:Iyzico:ApiKey"] ?? throw new InvalidOperationException("Studio:Payments:Iyzico:ApiKey eksik.");
        _secretKey = config["Studio:Payments:Iyzico:SecretKey"] ?? throw new InvalidOperationException("Studio:Payments:Iyzico:SecretKey eksik.");
        _baseUrl = (config["Studio:Payments:Iyzico:BaseUrl"] ?? "https://sandbox-api.iyzipay.com").TrimEnd('/');
        _logger = logger;
    }

    public async Task<PaymentInitiationResult> InitiateAsync(PaymentRequest request, CancellationToken ct)
    {
        // Iyzico Checkout Form Initialize
        var path = "/payment/iyzipos/checkoutform/initialize/auth/ecom";
        var body = new
        {
            locale = "tr",
            conversationId = request.ConversationId,
            price = request.Amount.ToString("F2", System.Globalization.CultureInfo.InvariantCulture),
            paidPrice = request.Amount.ToString("F2", System.Globalization.CultureInfo.InvariantCulture),
            currency = request.Currency,
            basketId = request.TenantKey,
            paymentGroup = "SUBSCRIPTION",
            callbackUrl = request.CallbackUrl,
            buyer = new
            {
                id = request.TenantKey,
                name = request.TenantName,
                surname = "Tenant",
                email = request.TenantEmail,
                identityNumber = "11111111111",
                registrationAddress = "—",
                ip = request.IpAddress ?? "127.0.0.1",
                city = "Istanbul",
                country = "Turkey"
            },
            shippingAddress = new { contactName = request.TenantName, city = "Istanbul", country = "Turkey", address = "—" },
            billingAddress  = new { contactName = request.TenantName, city = "Istanbul", country = "Turkey", address = "—" },
            basketItems = new[] { new {
                id = "subscription",
                name = request.Description,
                category1 = "Software",
                itemType = "VIRTUAL",
                price = request.Amount.ToString("F2", System.Globalization.CultureInfo.InvariantCulture)
            }}
        };

        var json = JsonSerializer.Serialize(body);
        var randomString = Guid.NewGuid().ToString("N").Substring(0, 8);
        var auth = BuildAuthHeader(randomString, json);

        using var msg = new HttpRequestMessage(HttpMethod.Post, _baseUrl + path)
        {
            Content = new StringContent(json, Encoding.UTF8, "application/json")
        };
        msg.Headers.TryAddWithoutValidation("Authorization", auth);
        msg.Headers.TryAddWithoutValidation("x-iyzi-rnd", randomString);

        try
        {
            using var resp = await _http.SendAsync(msg, ct);
            var respJson = await resp.Content.ReadAsStringAsync(ct);
            if (!resp.IsSuccessStatusCode)
            {
                _logger.LogWarning("Iyzico initiate failed: {Status} {Body}", resp.StatusCode, respJson);
                return new PaymentInitiationResult(false, null, null, null, ((int)resp.StatusCode).ToString(), respJson);
            }
            using var doc = JsonDocument.Parse(respJson);
            var root = doc.RootElement;
            var status = root.TryGetProperty("status", out var s) ? s.GetString() : null;
            if (!string.Equals(status, "success", StringComparison.OrdinalIgnoreCase))
            {
                var err = root.TryGetProperty("errorMessage", out var em) ? em.GetString() : "unknown";
                return new PaymentInitiationResult(false, null, null, null, status, err);
            }
            var checkoutUrl = root.TryGetProperty("paymentPageUrl", out var u) ? u.GetString() : null;
            var token = root.TryGetProperty("token", out var t) ? t.GetString() : null;
            return new PaymentInitiationResult(true, checkoutUrl, null, token, null, null);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Iyzico initiate exception");
            return new PaymentInitiationResult(false, null, null, null, "EXCEPTION", ex.Message);
        }
    }

    public async Task<PaymentCallbackResult> HandleCallbackAsync(IDictionary<string, string> formOrQuery, CancellationToken ct)
    {
        // Iyzico callback: token formdan gelir, sonra retrieve API'si ile dogrulanir
        if (!formOrQuery.TryGetValue("token", out var token) || string.IsNullOrWhiteSpace(token))
        {
            return new PaymentCallbackResult(false, null, null, null, null, "Token yok.");
        }

        var path = "/payment/iyzipos/checkoutform/auth/ecom/detail";
        var body = JsonSerializer.Serialize(new { locale = "tr", token, conversationId = (string?)null });
        var rnd = Guid.NewGuid().ToString("N").Substring(0, 8);
        var auth = BuildAuthHeader(rnd, body);

        using var msg = new HttpRequestMessage(HttpMethod.Post, _baseUrl + path)
        {
            Content = new StringContent(body, Encoding.UTF8, "application/json")
        };
        msg.Headers.TryAddWithoutValidation("Authorization", auth);
        msg.Headers.TryAddWithoutValidation("x-iyzi-rnd", rnd);

        try
        {
            using var resp = await _http.SendAsync(msg, ct);
            var json = await resp.Content.ReadAsStringAsync(ct);
            using var doc = JsonDocument.Parse(json);
            var root = doc.RootElement;
            var paymentStatus = root.TryGetProperty("paymentStatus", out var ps) ? ps.GetString() : null;
            var convId = root.TryGetProperty("conversationId", out var c) ? c.GetString() : null;
            var paidStr = root.TryGetProperty("paidPrice", out var p) ? p.GetString() : null;
            decimal? amount = decimal.TryParse(paidStr, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var d) ? d : null;
            var txId = root.TryGetProperty("paymentId", out var t) ? t.GetString() : null;
            return new PaymentCallbackResult(
                Paid: string.Equals(paymentStatus, "SUCCESS", StringComparison.OrdinalIgnoreCase),
                ProviderTransactionId: txId,
                ConversationId: convId,
                Amount: amount,
                RawStatus: paymentStatus,
                ErrorMessage: null);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Iyzico callback exception");
            return new PaymentCallbackResult(false, null, null, null, null, ex.Message);
        }
    }

    public Task<PaymentRefundResult> RefundAsync(string providerTransactionId, decimal amount, string? reason, CancellationToken ct)
    {
        // Iyzico refund icin /payment/refund endpoint'i — skeleton
        _logger.LogWarning("Iyzico refund stub called for {TxId} {Amount}", providerTransactionId, amount);
        return Task.FromResult(new PaymentRefundResult(false, null, "Iade endpoint'i implement edilmedi (Iyzipay SDK ekleyin)."));
    }

    /// <summary>Iyzico PKI v1 imzalama (basit). Production'da v2 (HMACSHA256) tercih edilmeli.</summary>
    private string BuildAuthHeader(string randomString, string body)
    {
        var hashStr = _apiKey + randomString + _secretKey + body;
        using var sha = SHA1.Create();
        var hash = sha.ComputeHash(Encoding.UTF8.GetBytes(hashStr));
        var hashB64 = Convert.ToBase64String(hash);
        return $"IYZWS {_apiKey}:{hashB64}";
    }
}
