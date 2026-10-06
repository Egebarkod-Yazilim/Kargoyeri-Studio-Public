using System.Net.Http.Headers;
using System.Text.Json;

namespace Kargoyeri.Studio.Core.Infrastructure.OrderChannels.Adapters;

/// <summary>
/// Amazon SP-API (Selling Partner) — canli iskelet adapter.
/// Docs: https://developer-docs.amazon.com/sp-api
/// Akis: 1) LWA refresh -> access token (api.amazon.com/auth/o2/token)
///       2) GET sellingpartnerapi-eu.amazon.com/orders/v0/orders
/// Not: AWS SigV4 imzasi olmadan SP-API access token tek basina yeterli olabiliyor (yeni RDT/grantless flow);
///      restricted operasyonlar icin RDT gerekir. Bu adapter listeleme operasyonu icin yeterli.
/// </summary>
public sealed class AmazonLiveAdapter : IOrderChannelAdapter
{
    private const string LwaUrl = "https://api.amazon.com/auth/o2/token";
    private const string MarketplaceTr = "A33AVAJ2PDY3EV";  // Amazon.com.tr
    // EU bolgesi (TR icin)
    private const string SpApiBase = "https://sellingpartnerapi-eu.amazon.com";

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<AmazonLiveAdapter> _logger;

    public OrderChannelType Type => OrderChannelType.Amazon;

    public AmazonLiveAdapter(IHttpClientFactory httpClientFactory, ILogger<AmazonLiveAdapter> logger)
    {
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    public async Task<OrderChannelTestResult> TestConnectionAsync(
        OrderChannelCredentials credentials, CancellationToken ct)
    {
        var (sellerId, refreshToken, lwaClientId, lwaClientSecret, missing) = ReadCreds(credentials);
        if (missing is not null) return new OrderChannelTestResult(false, missing);

        try
        {
            var token = await ExchangeRefreshTokenAsync(refreshToken!, lwaClientId!, lwaClientSecret!, ct);
            if (string.IsNullOrEmpty(token))
                return new OrderChannelTestResult(false, "Amazon LWA token alinamadi (refresh token gecersiz olabilir).");

            // Hizli sorgu: son 1 saatlik siparisler
            var since = DateTimeOffset.UtcNow.AddHours(-1).ToString("yyyy-MM-ddTHH:mm:ssZ");
            var url = $"{SpApiBase}/orders/v0/orders?MarketplaceIds={MarketplaceTr}&CreatedAfter={since}&MaxResultsPerPage=1";

            using var client = _httpClientFactory.CreateClient("amazon-orders");
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
            client.DefaultRequestHeaders.Remove("x-amz-access-token");
            client.DefaultRequestHeaders.Add("x-amz-access-token", token);

            using var resp = await client.GetAsync(url, ct);
            if (resp.IsSuccessStatusCode)
                return new OrderChannelTestResult(true, "Amazon SP-API bilgileri dogrulandi.");
            var body = await resp.Content.ReadAsStringAsync(ct);
            return new OrderChannelTestResult(false,
                $"Amazon SP-API hata: {(int)resp.StatusCode} {resp.StatusCode}",
                Truncate(body, 400));
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Amazon test connection hatasi (tenant={Tenant})", credentials.TenantKey);
            return new OrderChannelTestResult(false, "Amazon SP-API'ye ulasilamadi: " + ex.Message);
        }
    }

    public async Task<OrderChannelFetchResult> FetchOrdersAsync(
        OrderChannelCredentials credentials, DateTimeOffset since, CancellationToken ct)
    {
        var (sellerId, refreshToken, lwaClientId, lwaClientSecret, missing) = ReadCreds(credentials);
        if (missing is not null) return new OrderChannelFetchResult(false, missing, Array.Empty<RawIncomingOrder>());

        try
        {
            var token = await ExchangeRefreshTokenAsync(refreshToken!, lwaClientId!, lwaClientSecret!, ct);
            if (string.IsNullOrEmpty(token))
                return new OrderChannelFetchResult(false, "Amazon LWA token alinamadi.", Array.Empty<RawIncomingOrder>());

            var sinceStr = since.ToUniversalTime().ToString("yyyy-MM-ddTHH:mm:ssZ");
            var url = $"{SpApiBase}/orders/v0/orders" +
                      $"?MarketplaceIds={MarketplaceTr}" +
                      $"&CreatedAfter={sinceStr}" +
                      $"&MaxResultsPerPage=100";

            using var client = _httpClientFactory.CreateClient("amazon-orders");
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
            client.DefaultRequestHeaders.Remove("x-amz-access-token");
            client.DefaultRequestHeaders.Add("x-amz-access-token", token);

            using var resp = await client.GetAsync(url, ct);
            var body = await resp.Content.ReadAsStringAsync(ct);
            if (!resp.IsSuccessStatusCode)
            {
                return new OrderChannelFetchResult(false,
                    $"Amazon SP-API: {(int)resp.StatusCode} {resp.StatusCode} — {Truncate(body, 200)}",
                    Array.Empty<RawIncomingOrder>());
            }

            var orders = ParseOrders(body, credentials.TenantKey);
            return new OrderChannelFetchResult(true,
                $"Amazon: {orders.Count} siparis cekildi (since {sinceStr}).",
                orders);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Amazon fetch hatasi (tenant={Tenant})", credentials.TenantKey);
            return new OrderChannelFetchResult(false,
                "Amazon siparis cekiminde beklenmedik hata: " + ex.Message,
                Array.Empty<RawIncomingOrder>());
        }
    }

    private async Task<string?> ExchangeRefreshTokenAsync(string refreshToken, string clientId, string clientSecret, CancellationToken ct)
    {
        using var client = _httpClientFactory.CreateClient("amazon-lwa");
        var form = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["grant_type"] = "refresh_token",
            ["refresh_token"] = refreshToken,
            ["client_id"] = clientId,
            ["client_secret"] = clientSecret
        });
        using var resp = await client.PostAsync(LwaUrl, form, ct);
        if (!resp.IsSuccessStatusCode) return null;
        var json = await resp.Content.ReadAsStringAsync(ct);
        using var doc = JsonDocument.Parse(json);
        return doc.RootElement.TryGetProperty("access_token", out var at) ? at.GetString() : null;
    }

    private static (string? sellerId, string? refreshToken, string? lwaClientId, string? lwaClientSecret, string? missingMsg)
        ReadCreds(OrderChannelCredentials c)
    {
        // Field semasi: sellerId, refreshToken, accessKeyId (= LWA client id), secretKey (= LWA client secret)
        var sellerId = c.Get("sellerId");
        var refreshToken = c.Get("refreshToken");
        var lwaClientId = c.Get("accessKeyId");      // descriptor'da bu alan LWA client ID
        var lwaClientSecret = c.Get("secretKey");    // descriptor'da bu alan LWA client secret
        var missing = new List<string>();
        if (string.IsNullOrWhiteSpace(sellerId)) missing.Add("Seller ID");
        if (string.IsNullOrWhiteSpace(refreshToken)) missing.Add("LWA Refresh Token");
        if (string.IsNullOrWhiteSpace(lwaClientId)) missing.Add("LWA Client ID");
        if (string.IsNullOrWhiteSpace(lwaClientSecret)) missing.Add("LWA Client Secret");
        return missing.Count > 0
            ? (null, null, null, null, $"Eksik alanlar: {string.Join(", ", missing)}")
            : (sellerId, refreshToken, lwaClientId, lwaClientSecret, null);
    }

    private static IReadOnlyList<RawIncomingOrder> ParseOrders(string json, string tenantKey)
    {
        using var doc = JsonDocument.Parse(json);
        if (!doc.RootElement.TryGetProperty("payload", out var payload)) return Array.Empty<RawIncomingOrder>();
        if (!payload.TryGetProperty("Orders", out var orders) || orders.ValueKind != JsonValueKind.Array)
            return Array.Empty<RawIncomingOrder>();

        var list = new List<RawIncomingOrder>(orders.GetArrayLength());
        foreach (var o in orders.EnumerateArray())
        {
            string? Get(string p) => o.TryGetProperty(p, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;
            var amazonId = Get("AmazonOrderId");
            if (string.IsNullOrEmpty(amazonId)) continue;

            DateTimeOffset created = DateTimeOffset.UtcNow;
            if (o.TryGetProperty("PurchaseDate", out var pd) && pd.ValueKind == JsonValueKind.String &&
                DateTimeOffset.TryParse(pd.GetString(), out var d)) created = d;

            // Amazon adresi ayri call (GetOrderAddress) — burada placeholder
            var recipient = new RawAddress(
                FullName: Get("BuyerEmail") ?? "Amazon Musterisi",
                Phone: null,
                Email: Get("BuyerEmail"),
                City: null,
                District: null,
                AddressLine: null);

            decimal? total = null;
            if (o.TryGetProperty("OrderTotal", out var ot) && ot.ValueKind == JsonValueKind.Object &&
                ot.TryGetProperty("Amount", out var amt) && amt.ValueKind == JsonValueKind.String &&
                decimal.TryParse(amt.GetString(), System.Globalization.NumberStyles.Any,
                    System.Globalization.CultureInfo.InvariantCulture, out var t)) total = t;

            list.Add(new RawIncomingOrder(
                SourceChannel: OrderChannelType.Amazon,
                ChannelExternalOrderId: $"amazon:{amazonId}",
                OrderNumber: amazonId!,
                CreatedAtUtc: created,
                CollectionAmount: null, // Amazon pre-paid
                CurrencyCode: o.TryGetProperty("OrderTotal", out var ot2) && ot2.TryGetProperty("CurrencyCode", out var cc)
                    ? cc.GetString() ?? "TRY" : "TRY",
                Recipient: recipient,
                Items: Array.Empty<RawOrderItem>(),  // GetOrderItems ayri call
                Notes: $"Amazon SP-API siparis ({tenantKey})"));
        }
        return list;
    }

    private static string Truncate(string s, int max) => s.Length <= max ? s : s.Substring(0, max) + "...";
}
