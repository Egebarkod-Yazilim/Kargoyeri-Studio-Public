using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace Kargoyeri.Studio.Core.Infrastructure.OrderChannels.Adapters;

/// <summary>
/// Cicek Sepeti Marketplace API (canli).
/// Docs:    https://apidocs.ciceksepeti.com
/// Endpoint: POST https://apis.ciceksepeti.com/api/v1/Order/GetOrders
/// Auth:    Header "x-api-key: {apiKey}"
/// Body:    { startDate, endDate, pageSize, pageNumber }
/// </summary>
public sealed class CicekSepetiLiveAdapter : IOrderChannelAdapter
{
    private const string BaseUrl = "https://apis.ciceksepeti.com";
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<CicekSepetiLiveAdapter> _logger;

    public OrderChannelType Type => OrderChannelType.CicekSepeti;

    public CicekSepetiLiveAdapter(IHttpClientFactory httpClientFactory, ILogger<CicekSepetiLiveAdapter> logger)
    {
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    public async Task<OrderChannelTestResult> TestConnectionAsync(
        OrderChannelCredentials credentials, CancellationToken ct)
    {
        var (apiKey, dealerCode, missing) = ReadCreds(credentials);
        if (missing is not null) return new OrderChannelTestResult(false, missing);

        try
        {
            using var client = BuildClient(apiKey!);
            var body = JsonSerializer.Serialize(new
            {
                startDate = DateTimeOffset.UtcNow.AddDays(-1).ToString("yyyy-MM-ddTHH:mm:ss"),
                endDate = DateTimeOffset.UtcNow.ToString("yyyy-MM-ddTHH:mm:ss"),
                pageSize = 1,
                pageNumber = 1
            });
            using var req = new HttpRequestMessage(HttpMethod.Post, $"{BaseUrl}/api/v1/Order/GetOrders")
            { Content = new StringContent(body, Encoding.UTF8, "application/json") };
            using var resp = await client.SendAsync(req, ct);
            if (resp.IsSuccessStatusCode)
                return new OrderChannelTestResult(true, "Cicek Sepeti API bilgileri dogrulandi.");
            var err = await resp.Content.ReadAsStringAsync(ct);
            return new OrderChannelTestResult(false,
                $"Cicek Sepeti API hata: {(int)resp.StatusCode} {resp.StatusCode}",
                Truncate(err, 400));
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "CicekSepeti test connection hatasi (tenant={Tenant})", credentials.TenantKey);
            return new OrderChannelTestResult(false, "Cicek Sepeti API'ye ulasilamadi: " + ex.Message);
        }
    }

    public async Task<OrderChannelFetchResult> FetchOrdersAsync(
        OrderChannelCredentials credentials, DateTimeOffset since, CancellationToken ct)
    {
        var (apiKey, dealerCode, missing) = ReadCreds(credentials);
        if (missing is not null) return new OrderChannelFetchResult(false, missing, Array.Empty<RawIncomingOrder>());

        var startUtc = since.ToUniversalTime();
        var endUtc = DateTimeOffset.UtcNow;

        var reqBody = JsonSerializer.Serialize(new
        {
            startDate = startUtc.ToString("yyyy-MM-ddTHH:mm:ss"),
            endDate = endUtc.ToString("yyyy-MM-ddTHH:mm:ss"),
            pageSize = 200,
            pageNumber = 1
        });

        try
        {
            using var client = BuildClient(apiKey!);
            using var req = new HttpRequestMessage(HttpMethod.Post, $"{BaseUrl}/api/v1/Order/GetOrders")
            { Content = new StringContent(reqBody, Encoding.UTF8, "application/json") };
            using var resp = await client.SendAsync(req, ct);
            var body = await resp.Content.ReadAsStringAsync(ct);
            if (!resp.IsSuccessStatusCode)
            {
                return new OrderChannelFetchResult(false,
                    $"Cicek Sepeti API: {(int)resp.StatusCode} {resp.StatusCode} — {Truncate(body, 200)}",
                    Array.Empty<RawIncomingOrder>());
            }
            var orders = ParseOrders(body, credentials.TenantKey);
            return new OrderChannelFetchResult(true,
                $"Cicek Sepeti: {orders.Count} siparis cekildi ({startUtc:yyyy-MM-dd}..{endUtc:yyyy-MM-dd}).",
                orders);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "CicekSepeti fetch hatasi (tenant={Tenant})", credentials.TenantKey);
            return new OrderChannelFetchResult(false,
                "Cicek Sepeti siparis cekiminde beklenmedik hata: " + ex.Message,
                Array.Empty<RawIncomingOrder>());
        }
    }

    private HttpClient BuildClient(string apiKey)
    {
        var client = _httpClientFactory.CreateClient("ciceksepeti-orders");
        client.DefaultRequestHeaders.Remove("x-api-key");
        client.DefaultRequestHeaders.Add("x-api-key", apiKey);
        client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        client.Timeout = TimeSpan.FromSeconds(30);
        return client;
    }

    private static (string? apiKey, string? dealerCode, string? missingMsg) ReadCreds(OrderChannelCredentials c)
    {
        var apiKey = c.Get("apiKey");
        var dealerCode = c.Get("dealerCode");
        var missing = new List<string>();
        if (string.IsNullOrWhiteSpace(apiKey)) missing.Add("API Key");
        if (string.IsNullOrWhiteSpace(dealerCode)) missing.Add("Bayi Kodu");
        return missing.Count > 0
            ? (null, null, $"Eksik alanlar: {string.Join(", ", missing)}")
            : (apiKey, dealerCode, null);
    }

    private static IReadOnlyList<RawIncomingOrder> ParseOrders(string json, string tenantKey)
    {
        using var doc = JsonDocument.Parse(json);
        // CS: { "orderList": [...] } yaygın şema; "data.items" da olabilir
        JsonElement items = default;
        bool found = false;
        if (doc.RootElement.TryGetProperty("orderList", out var ol) && ol.ValueKind == JsonValueKind.Array)
        { items = ol; found = true; }
        else if (doc.RootElement.TryGetProperty("data", out var data))
        {
            if (data.ValueKind == JsonValueKind.Array) { items = data; found = true; }
            else if (data.TryGetProperty("orderList", out var ol2) && ol2.ValueKind == JsonValueKind.Array)
            { items = ol2; found = true; }
        }
        if (!found) return Array.Empty<RawIncomingOrder>();

        var list = new List<RawIncomingOrder>(items.GetArrayLength());
        foreach (var o in items.EnumerateArray())
        {
            string? Get(string p) => o.TryGetProperty(p, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;
            var orderNumber = Get("orderNumber") ?? Get("orderId") ?? Get("id");
            if (string.IsNullOrEmpty(orderNumber)) continue;

            DateTimeOffset created = DateTimeOffset.UtcNow;
            if (o.TryGetProperty("orderDate", out var od) && od.ValueKind == JsonValueKind.String &&
                DateTimeOffset.TryParse(od.GetString(), out var d)) created = d;

            var recipient = new RawAddress(
                FullName: Get("buyerName") ?? Get("customerName") ?? "Cicek Sepeti Musterisi",
                Phone: Get("phone") ?? Get("gsm"),
                Email: Get("email"),
                City: Get("city"),
                District: Get("district"),
                AddressLine: Get("address"));

            var itemsList = new List<RawOrderItem>();
            if (o.TryGetProperty("items", out var lines) && lines.ValueKind == JsonValueKind.Array)
            {
                foreach (var l in lines.EnumerateArray())
                {
                    string? Lg(string p) => l.TryGetProperty(p, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;
                    int qty = l.TryGetProperty("quantity", out var q) && q.ValueKind == JsonValueKind.Number ? q.GetInt32() : 1;
                    decimal? price = l.TryGetProperty("price", out var p) && p.ValueKind == JsonValueKind.Number ? p.GetDecimal() : null;
                    itemsList.Add(new RawOrderItem(
                        ProductName: Lg("productName") ?? Lg("name") ?? "Cicek Sepeti urun",
                        Quantity: qty,
                        UnitPrice: price,
                        Sku: Lg("sku") ?? Lg("barcode")));
                }
            }

            list.Add(new RawIncomingOrder(
                SourceChannel: OrderChannelType.CicekSepeti,
                ChannelExternalOrderId: $"ciceksepeti:{orderNumber}",
                OrderNumber: orderNumber!,
                CreatedAtUtc: created,
                CollectionAmount: null,
                CurrencyCode: "TRY",
                Recipient: recipient,
                Items: itemsList,
                Notes: $"Cicek Sepeti siparis ({tenantKey})"));
        }
        return list;
    }

    private static string Truncate(string s, int max) => s.Length <= max ? s : s.Substring(0, max) + "...";
}
