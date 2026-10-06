using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace Kargoyeri.Studio.Core.Infrastructure.OrderChannels.Adapters;

/// <summary>
/// Pazarama Marketplace API (canli).
/// Docs:    https://isortagim.pazarama.com/developers
/// Endpoint: GET https://isortagimapi.pazarama.com/orders (startDate, endDate, page, size query)
/// Auth:    Basic Auth -> base64(clientId:clientSecret)
/// </summary>
public sealed class PazaramaLiveAdapter : IOrderChannelAdapter
{
    private const string BaseUrl = "https://isortagimapi.pazarama.com";
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<PazaramaLiveAdapter> _logger;

    public OrderChannelType Type => OrderChannelType.Pazarama;

    public PazaramaLiveAdapter(IHttpClientFactory httpClientFactory, ILogger<PazaramaLiveAdapter> logger)
    {
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    public async Task<OrderChannelTestResult> TestConnectionAsync(
        OrderChannelCredentials credentials, CancellationToken ct)
    {
        var (clientId, clientSecret, missing) = ReadCreds(credentials);
        if (missing is not null) return new OrderChannelTestResult(false, missing);

        try
        {
            using var client = BuildClient(clientId!, clientSecret!);
            using var resp = await client.GetAsync($"{BaseUrl}/orders?page=0&size=1", ct);
            if (resp.IsSuccessStatusCode)
                return new OrderChannelTestResult(true, "Pazarama API bilgileri dogrulandi.");
            var body = await resp.Content.ReadAsStringAsync(ct);
            return new OrderChannelTestResult(false,
                $"Pazarama API hata: {(int)resp.StatusCode} {resp.StatusCode}",
                Truncate(body, 400));
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Pazarama test connection hatasi (tenant={Tenant})", credentials.TenantKey);
            return new OrderChannelTestResult(false, "Pazarama API'ye ulasilamadi: " + ex.Message);
        }
    }

    public async Task<OrderChannelFetchResult> FetchOrdersAsync(
        OrderChannelCredentials credentials, DateTimeOffset since, CancellationToken ct)
    {
        var (clientId, clientSecret, missing) = ReadCreds(credentials);
        if (missing is not null) return new OrderChannelFetchResult(false, missing, Array.Empty<RawIncomingOrder>());

        var startUtc = since.ToUniversalTime();
        var endUtc = DateTimeOffset.UtcNow;
        var url = $"{BaseUrl}/orders" +
                  $"?startDate={startUtc:yyyy-MM-ddTHH:mm:ss}Z" +
                  $"&endDate={endUtc:yyyy-MM-ddTHH:mm:ss}Z" +
                  $"&page=0&size=200";

        try
        {
            using var client = BuildClient(clientId!, clientSecret!);
            using var resp = await client.GetAsync(url, ct);
            var body = await resp.Content.ReadAsStringAsync(ct);
            if (!resp.IsSuccessStatusCode)
            {
                return new OrderChannelFetchResult(false,
                    $"Pazarama API: {(int)resp.StatusCode} {resp.StatusCode} — {Truncate(body, 200)}",
                    Array.Empty<RawIncomingOrder>());
            }
            var orders = ParseOrders(body, credentials.TenantKey);
            return new OrderChannelFetchResult(true,
                $"Pazarama: {orders.Count} siparis cekildi ({startUtc:yyyy-MM-dd}..{endUtc:yyyy-MM-dd}).",
                orders);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Pazarama fetch hatasi (tenant={Tenant})", credentials.TenantKey);
            return new OrderChannelFetchResult(false,
                "Pazarama siparis cekiminde beklenmedik hata: " + ex.Message,
                Array.Empty<RawIncomingOrder>());
        }
    }

    private HttpClient BuildClient(string clientId, string clientSecret)
    {
        var client = _httpClientFactory.CreateClient("pazarama-orders");
        var basic = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{clientId}:{clientSecret}"));
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", basic);
        client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        client.Timeout = TimeSpan.FromSeconds(30);
        return client;
    }

    private static (string? clientId, string? clientSecret, string? missingMsg) ReadCreds(OrderChannelCredentials c)
    {
        var clientId = c.Get("clientId");
        var clientSecret = c.Get("clientSecret");
        var missing = new List<string>();
        if (string.IsNullOrWhiteSpace(clientId)) missing.Add("Client ID");
        if (string.IsNullOrWhiteSpace(clientSecret)) missing.Add("Client Secret");
        return missing.Count > 0
            ? (null, null, $"Eksik alanlar: {string.Join(", ", missing)}")
            : (clientId, clientSecret, null);
    }

    private static IReadOnlyList<RawIncomingOrder> ParseOrders(string json, string tenantKey)
    {
        using var doc = JsonDocument.Parse(json);
        // Pazarama: { "data": { "items": [...] } } veya { "data": [...] } — iki sema da yakalansin
        JsonElement items = default;
        bool found = false;
        if (doc.RootElement.TryGetProperty("data", out var data))
        {
            if (data.ValueKind == JsonValueKind.Array) { items = data; found = true; }
            else if (data.ValueKind == JsonValueKind.Object && data.TryGetProperty("items", out var arr) && arr.ValueKind == JsonValueKind.Array)
            { items = arr; found = true; }
        }
        if (!found && doc.RootElement.TryGetProperty("items", out var topItems) && topItems.ValueKind == JsonValueKind.Array)
        { items = topItems; found = true; }
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

            decimal? total = o.TryGetProperty("totalPrice", out var tp) && tp.ValueKind == JsonValueKind.Number
                ? tp.GetDecimal() : null;

            var addr = o.TryGetProperty("shippingAddress", out var sa) && sa.ValueKind == JsonValueKind.Object ? sa : default;
            string? A(string p) => addr.ValueKind == JsonValueKind.Object && addr.TryGetProperty(p, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;

            var recipient = new RawAddress(
                FullName: A("fullName") ?? Get("buyerName") ?? "Pazarama Musterisi",
                Phone: A("phone") ?? A("gsm"),
                Email: Get("email"),
                City: A("city"),
                District: A("district"),
                AddressLine: A("address") ?? A("addressLine"));

            var itemsList = new List<RawOrderItem>();
            if (o.TryGetProperty("items", out var lines) && lines.ValueKind == JsonValueKind.Array)
            {
                foreach (var l in lines.EnumerateArray())
                {
                    string? Lg(string p) => l.TryGetProperty(p, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;
                    int qty = l.TryGetProperty("quantity", out var q) && q.ValueKind == JsonValueKind.Number ? q.GetInt32() : 1;
                    decimal? price = l.TryGetProperty("price", out var p) && p.ValueKind == JsonValueKind.Number ? p.GetDecimal() : null;
                    itemsList.Add(new RawOrderItem(
                        ProductName: Lg("productName") ?? Lg("name") ?? "Pazarama urun",
                        Quantity: qty,
                        UnitPrice: price,
                        Sku: Lg("sku") ?? Lg("barcode")));
                }
            }

            list.Add(new RawIncomingOrder(
                SourceChannel: OrderChannelType.Pazarama,
                ChannelExternalOrderId: $"pazarama:{orderNumber}",
                OrderNumber: orderNumber!,
                CreatedAtUtc: created,
                CollectionAmount: null,
                CurrencyCode: "TRY",
                Recipient: recipient,
                Items: itemsList,
                Notes: $"Pazarama siparis ({tenantKey})"));
        }
        return list;
    }

    private static string Truncate(string s, int max) => s.Length <= max ? s : s.Substring(0, max) + "...";
}
