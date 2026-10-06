using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace Kargoyeri.Studio.Core.Infrastructure.OrderChannels.Adapters;

/// <summary>
/// Modanisa Marketplace API — canli iskelet.
/// Public dokuman yok; anlasmali saticilara acilan REST endpoint kullanilir.
/// Endpoint:  GET https://api.modanisa.com/marketplace/orders
/// Auth:      Basic Auth -> base64(apiKey:apiSecret) + Header "X-Seller-Id"
/// </summary>
public sealed class ModanisaLiveAdapter : IOrderChannelAdapter
{
    private const string BaseUrl = "https://api.modanisa.com";
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<ModanisaLiveAdapter> _logger;

    public OrderChannelType Type => OrderChannelType.Modanisa;

    public ModanisaLiveAdapter(IHttpClientFactory httpClientFactory, ILogger<ModanisaLiveAdapter> logger)
    {
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    public async Task<OrderChannelTestResult> TestConnectionAsync(
        OrderChannelCredentials credentials, CancellationToken ct)
    {
        var (apiKey, apiSecret, sellerId, missing) = ReadCreds(credentials);
        if (missing is not null) return new OrderChannelTestResult(false, missing);

        try
        {
            using var client = BuildClient(apiKey!, apiSecret!, sellerId!);
            using var resp = await client.GetAsync($"{BaseUrl}/marketplace/orders?page=1&size=1", ct);
            if (resp.IsSuccessStatusCode)
                return new OrderChannelTestResult(true, "Modanisa API bilgileri dogrulandi.");
            var body = await resp.Content.ReadAsStringAsync(ct);
            return new OrderChannelTestResult(false,
                $"Modanisa API hata: {(int)resp.StatusCode} {resp.StatusCode}",
                Truncate(body, 400));
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Modanisa test connection hatasi (tenant={Tenant})", credentials.TenantKey);
            return new OrderChannelTestResult(false, "Modanisa API'ye ulasilamadi: " + ex.Message);
        }
    }

    public async Task<OrderChannelFetchResult> FetchOrdersAsync(
        OrderChannelCredentials credentials, DateTimeOffset since, CancellationToken ct)
    {
        var (apiKey, apiSecret, sellerId, missing) = ReadCreds(credentials);
        if (missing is not null) return new OrderChannelFetchResult(false, missing, Array.Empty<RawIncomingOrder>());

        var startUtc = since.ToUniversalTime();
        var url = $"{BaseUrl}/marketplace/orders" +
                  $"?startDate={startUtc:yyyy-MM-ddTHH:mm:ss}Z" +
                  $"&endDate={DateTimeOffset.UtcNow:yyyy-MM-ddTHH:mm:ss}Z" +
                  $"&page=1&size=200";

        try
        {
            using var client = BuildClient(apiKey!, apiSecret!, sellerId!);
            using var resp = await client.GetAsync(url, ct);
            var body = await resp.Content.ReadAsStringAsync(ct);
            if (!resp.IsSuccessStatusCode)
            {
                return new OrderChannelFetchResult(false,
                    $"Modanisa API: {(int)resp.StatusCode} {resp.StatusCode} — {Truncate(body, 200)}",
                    Array.Empty<RawIncomingOrder>());
            }
            var orders = ParseOrders(body, credentials.TenantKey);
            return new OrderChannelFetchResult(true,
                $"Modanisa: {orders.Count} siparis cekildi.", orders);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Modanisa fetch hatasi (tenant={Tenant})", credentials.TenantKey);
            return new OrderChannelFetchResult(false,
                "Modanisa siparis cekiminde beklenmedik hata: " + ex.Message,
                Array.Empty<RawIncomingOrder>());
        }
    }

    private HttpClient BuildClient(string apiKey, string apiSecret, string sellerId)
    {
        var client = _httpClientFactory.CreateClient("modanisa-orders");
        var basic = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{apiKey}:{apiSecret}"));
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", basic);
        client.DefaultRequestHeaders.Remove("X-Seller-Id");
        client.DefaultRequestHeaders.Add("X-Seller-Id", sellerId);
        client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        client.Timeout = TimeSpan.FromSeconds(30);
        return client;
    }

    private static (string? apiKey, string? apiSecret, string? sellerId, string? missingMsg) ReadCreds(OrderChannelCredentials c)
    {
        var apiKey = c.Get("apiKey");
        var apiSecret = c.Get("apiSecret");
        var sellerId = c.Get("sellerId");
        var missing = new List<string>();
        if (string.IsNullOrWhiteSpace(apiKey)) missing.Add("API Key");
        if (string.IsNullOrWhiteSpace(apiSecret)) missing.Add("API Secret");
        if (string.IsNullOrWhiteSpace(sellerId)) missing.Add("Satici ID");
        return missing.Count > 0
            ? (null, null, null, $"Eksik alanlar: {string.Join(", ", missing)}")
            : (apiKey, apiSecret, sellerId, null);
    }

    private static IReadOnlyList<RawIncomingOrder> ParseOrders(string json, string tenantKey)
    {
        using var doc = JsonDocument.Parse(json);
        JsonElement items = default;
        bool found = false;
        if (doc.RootElement.TryGetProperty("data", out var data))
        {
            if (data.ValueKind == JsonValueKind.Array) { items = data; found = true; }
            else if (data.TryGetProperty("items", out var arr) && arr.ValueKind == JsonValueKind.Array)
            { items = arr; found = true; }
            else if (data.TryGetProperty("orders", out var ord) && ord.ValueKind == JsonValueKind.Array)
            { items = ord; found = true; }
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
                FullName: Get("customerName") ?? Get("buyerName") ?? "Modanisa Musterisi",
                Phone: Get("phone") ?? Get("gsm"),
                Email: Get("email"),
                City: Get("city"),
                District: Get("district"),
                AddressLine: Get("address"));

            list.Add(new RawIncomingOrder(
                SourceChannel: OrderChannelType.Modanisa,
                ChannelExternalOrderId: $"modanisa:{orderNumber}",
                OrderNumber: orderNumber!,
                CreatedAtUtc: created,
                CollectionAmount: null,
                CurrencyCode: "TRY",
                Recipient: recipient,
                Items: Array.Empty<RawOrderItem>(),
                Notes: $"Modanisa siparis ({tenantKey})"));
        }
        return list;
    }

    private static string Truncate(string s, int max) => s.Length <= max ? s : s.Substring(0, max) + "...";
}
