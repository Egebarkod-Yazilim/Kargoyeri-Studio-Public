using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace Kargoyeri.Studio.Core.Infrastructure.OrderChannels.Adapters;

/// <summary>
/// Trendyol Seller API (gercek/canli).
/// Docs: https://developers.trendyol.com/v3.0/docs/2-order-integration-api-endpoints
/// Endpoint:  GET https://apigw.trendyol.com/integration/order/sellers/{supplierId}/orders
/// Auth:      Basic Auth -> base64(apiKey:apiSecret)
/// User-Agent: "{supplierId} - SelfIntegration" (zorunlu)
/// Date:      Unix epoch milisaniye (max 14 gunluk pencere)
/// Rate:      ~1000 req/dk
/// </summary>
public sealed class TrendyolLiveAdapter : IOrderChannelAdapter
{
    private const string BaseUrl = "https://apigw.trendyol.com/integration/order/sellers";
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<TrendyolLiveAdapter> _logger;

    public OrderChannelType Type => OrderChannelType.Trendyol;

    public TrendyolLiveAdapter(IHttpClientFactory httpClientFactory, ILogger<TrendyolLiveAdapter> logger)
    {
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    public async Task<OrderChannelTestResult> TestConnectionAsync(
        OrderChannelCredentials credentials, CancellationToken ct)
    {
        var (supplierId, apiKey, apiSecret, missing) = ReadCreds(credentials);
        if (missing is not null) return new OrderChannelTestResult(false, missing);

        try
        {
            // 1 sayfa, 1 boyut yeterli — sadece auth'u test eder
            var url = $"{BaseUrl}/{supplierId}/orders?size=1&page=0";
            using var client = BuildClient(supplierId!, apiKey!, apiSecret!);
            using var resp = await client.GetAsync(url, ct);
            if (resp.IsSuccessStatusCode)
            {
                return new OrderChannelTestResult(true, "Trendyol API bilgileri dogrulandi.");
            }
            var body = await resp.Content.ReadAsStringAsync(ct);
            return new OrderChannelTestResult(false,
                $"Trendyol API hata kodu: {(int)resp.StatusCode} {resp.StatusCode}",
                Truncate(body, 400));
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Trendyol test connection hatasi (tenant={Tenant})", credentials.TenantKey);
            return new OrderChannelTestResult(false, "Trendyol API'ye ulasilamadi: " + ex.Message);
        }
    }

    public async Task<OrderChannelFetchResult> FetchOrdersAsync(
        OrderChannelCredentials credentials, DateTimeOffset since, CancellationToken ct)
    {
        var (supplierId, apiKey, apiSecret, missing) = ReadCreds(credentials);
        if (missing is not null) return new OrderChannelFetchResult(false, missing, Array.Empty<RawIncomingOrder>());

        // Trendyol max 14 gun penceresi — daha gerideyse 14 gune sıkıstir
        var startUtc = since.ToUniversalTime();
        var endUtc = DateTimeOffset.UtcNow;
        if ((endUtc - startUtc).TotalDays > 13) startUtc = endUtc.AddDays(-13);

        var startMs = startUtc.ToUnixTimeMilliseconds();
        var endMs = endUtc.ToUnixTimeMilliseconds();
        var url = $"{BaseUrl}/{supplierId}/orders" +
                  $"?startDate={startMs}&endDate={endMs}" +
                  $"&orderByField=PackageLastModifiedDate&orderByDirection=DESC&size=200&page=0";

        try
        {
            using var client = BuildClient(supplierId!, apiKey!, apiSecret!);
            using var resp = await client.GetAsync(url, ct);
            var body = await resp.Content.ReadAsStringAsync(ct);

            if (!resp.IsSuccessStatusCode)
            {
                return new OrderChannelFetchResult(false,
                    $"Trendyol API: {(int)resp.StatusCode} {resp.StatusCode} — {Truncate(body, 200)}",
                    Array.Empty<RawIncomingOrder>());
            }

            var orders = ParseOrders(body, credentials.TenantKey);
            return new OrderChannelFetchResult(true,
                $"Trendyol: {orders.Count} siparis cekildi ({startUtc:yyyy-MM-dd}..{endUtc:yyyy-MM-dd}).",
                orders);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Trendyol fetch hatasi (tenant={Tenant})", credentials.TenantKey);
            return new OrderChannelFetchResult(false,
                "Trendyol siparis cekiminde beklenmedik hata: " + ex.Message,
                Array.Empty<RawIncomingOrder>());
        }
    }

    private HttpClient BuildClient(string supplierId, string apiKey, string apiSecret)
    {
        var client = _httpClientFactory.CreateClient("trendyol-orders");
        var basic = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{apiKey}:{apiSecret}"));
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", basic);
        client.DefaultRequestHeaders.UserAgent.ParseAdd($"{supplierId} - SelfIntegration");
        client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        client.Timeout = TimeSpan.FromSeconds(30);
        return client;
    }

    private static (string? supplierId, string? apiKey, string? apiSecret, string? missingMsg) ReadCreds(OrderChannelCredentials c)
    {
        var supplierId = c.Get("supplierId");
        var apiKey = c.Get("apiKey");
        var apiSecret = c.Get("apiSecret");
        var missing = new List<string>();
        if (string.IsNullOrWhiteSpace(supplierId)) missing.Add("Supplier ID");
        if (string.IsNullOrWhiteSpace(apiKey)) missing.Add("API Key");
        if (string.IsNullOrWhiteSpace(apiSecret)) missing.Add("API Secret");
        return missing.Count > 0
            ? (null, null, null, $"Eksik alanlar: {string.Join(", ", missing)}")
            : (supplierId, apiKey, apiSecret, null);
    }

    private static IReadOnlyList<RawIncomingOrder> ParseOrders(string json, string tenantKey)
    {
        using var doc = JsonDocument.Parse(json);
        if (!doc.RootElement.TryGetProperty("content", out var content) || content.ValueKind != JsonValueKind.Array)
            return Array.Empty<RawIncomingOrder>();

        var list = new List<RawIncomingOrder>(content.GetArrayLength());
        foreach (var o in content.EnumerateArray())
        {
            var orderNumber = o.TryGetProperty("orderNumber", out var on) ? on.GetString() ?? "" : "";
            if (string.IsNullOrEmpty(orderNumber)) continue;

            var orderDateMs = o.TryGetProperty("orderDate", out var od) && od.ValueKind == JsonValueKind.Number
                ? od.GetInt64() : 0;
            var createdAt = orderDateMs > 0
                ? DateTimeOffset.FromUnixTimeMilliseconds(orderDateMs)
                : DateTimeOffset.UtcNow;

            decimal? totalPrice = o.TryGetProperty("totalPrice", out var tp) && tp.ValueKind == JsonValueKind.Number
                ? tp.GetDecimal() : null;

            var currency = o.TryGetProperty("currencyCode", out var cur) ? cur.GetString() ?? "TRY" : "TRY";

            var recipient = ReadAddress(o, "shipmentAddress");
            var items = ReadItems(o);

            list.Add(new RawIncomingOrder(
                SourceChannel: OrderChannelType.Trendyol,
                ChannelExternalOrderId: $"trendyol:{orderNumber}",
                OrderNumber: orderNumber,
                CreatedAtUtc: createdAt,
                CollectionAmount: null, // Trendyol pre-paid varsayim
                CurrencyCode: currency,
                Recipient: recipient,
                Items: items,
                Notes: $"Trendyol siparis ({tenantKey})"));
        }
        return list;
    }

    private static RawAddress ReadAddress(JsonElement order, string key)
    {
        if (!order.TryGetProperty(key, out var addr) || addr.ValueKind != JsonValueKind.Object)
            return new RawAddress("", null, null, null, null, null);

        string? Get(string p) => addr.TryGetProperty(p, out var v) && v.ValueKind == JsonValueKind.String
            ? v.GetString() : null;

        return new RawAddress(
            FullName: Get("fullName") ?? Get("firstName") ?? "Trendyol Musterisi",
            Phone: Get("phone") ?? Get("gsm"),
            Email: order.TryGetProperty("customerEmail", out var em) ? em.GetString() : null,
            City: Get("city"),
            District: Get("district"),
            AddressLine: Get("address1") ?? Get("fullAddress"),
            PostalCode: Get("postalCode"));
    }

    private static IReadOnlyList<RawOrderItem> ReadItems(JsonElement order)
    {
        if (!order.TryGetProperty("lines", out var lines) || lines.ValueKind != JsonValueKind.Array)
            return Array.Empty<RawOrderItem>();
        var result = new List<RawOrderItem>(lines.GetArrayLength());
        foreach (var l in lines.EnumerateArray())
        {
            string? Get(string p) => l.TryGetProperty(p, out var v) && v.ValueKind == JsonValueKind.String
                ? v.GetString() : null;
            int qty = l.TryGetProperty("quantity", out var q) && q.ValueKind == JsonValueKind.Number
                ? q.GetInt32() : 1;
            decimal? price = l.TryGetProperty("price", out var p) && p.ValueKind == JsonValueKind.Number
                ? p.GetDecimal() : null;
            result.Add(new RawOrderItem(
                ProductName: Get("productName") ?? "Trendyol urun",
                Quantity: qty,
                UnitPrice: price,
                Sku: Get("merchantSku") ?? Get("barcode")));
        }
        return result;
    }

    private static string Truncate(string s, int max) =>
        s.Length <= max ? s : s.Substring(0, max) + "...";
}
