using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace Kargoyeri.Studio.Core.Infrastructure.OrderChannels.Adapters;

/// <summary>
/// Hepsiburada Marketplace OMS API (gercek/canli).
/// Docs: https://developers.hepsiburada.com/hepsiburada/reference/get_packages-merchantid-merchantid
/// Endpoint:  GET https://oms-external.hepsiburada.com/packages/merchantid/{merchantId}
/// Auth:      Basic Auth -> base64(username:password)  (panel uyesi degil; entegrasyon kullanicisi)
/// User-Agent: "{merchantId} - SelfIntegration" (zorunlu)
/// Date:      ISO 8601 (begindate / enddate)
/// HB siparisleri "package" (paket) olarak dondurur — biz bunu RawIncomingOrder'a cevirirken
/// packageNumber'i ChannelExternalOrderId yapariz.
/// </summary>
public sealed class HepsiburadaLiveAdapter : IOrderChannelAdapter
{
    private const string BaseUrl = "https://oms-external.hepsiburada.com";
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<HepsiburadaLiveAdapter> _logger;

    public OrderChannelType Type => OrderChannelType.Hepsiburada;

    public HepsiburadaLiveAdapter(IHttpClientFactory httpClientFactory, ILogger<HepsiburadaLiveAdapter> logger)
    {
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    public async Task<OrderChannelTestResult> TestConnectionAsync(
        OrderChannelCredentials credentials, CancellationToken ct)
    {
        var (merchantId, username, password, missing) = ReadCreds(credentials);
        if (missing is not null) return new OrderChannelTestResult(false, missing);

        try
        {
            var url = $"{BaseUrl}/packages/merchantid/{merchantId}?offset=0&limit=1";
            using var client = BuildClient(merchantId!, username!, password!);
            using var resp = await client.GetAsync(url, ct);
            if (resp.IsSuccessStatusCode)
            {
                return new OrderChannelTestResult(true, "Hepsiburada API bilgileri dogrulandi.");
            }
            var body = await resp.Content.ReadAsStringAsync(ct);
            return new OrderChannelTestResult(false,
                $"Hepsiburada API hata kodu: {(int)resp.StatusCode} {resp.StatusCode}",
                Truncate(body, 400));
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Hepsiburada test connection hatasi (tenant={Tenant})", credentials.TenantKey);
            return new OrderChannelTestResult(false, "Hepsiburada API'ye ulasilamadi: " + ex.Message);
        }
    }

    public async Task<OrderChannelFetchResult> FetchOrdersAsync(
        OrderChannelCredentials credentials, DateTimeOffset since, CancellationToken ct)
    {
        var (merchantId, username, password, missing) = ReadCreds(credentials);
        if (missing is not null) return new OrderChannelFetchResult(false, missing, Array.Empty<RawIncomingOrder>());

        var startUtc = since.ToUniversalTime();
        var endUtc = DateTimeOffset.UtcNow;
        var begin = startUtc.ToString("yyyy-MM-ddTHH:mm:ss");
        var end = endUtc.ToString("yyyy-MM-ddTHH:mm:ss");

        var url = $"{BaseUrl}/packages/merchantid/{merchantId}" +
                  $"?offset=0&limit=100&begindate={begin}&enddate={end}";

        try
        {
            using var client = BuildClient(merchantId!, username!, password!);
            using var resp = await client.GetAsync(url, ct);
            var body = await resp.Content.ReadAsStringAsync(ct);

            if (!resp.IsSuccessStatusCode)
            {
                return new OrderChannelFetchResult(false,
                    $"Hepsiburada API: {(int)resp.StatusCode} {resp.StatusCode} — {Truncate(body, 200)}",
                    Array.Empty<RawIncomingOrder>());
            }

            var orders = ParsePackages(body, credentials.TenantKey);
            return new OrderChannelFetchResult(true,
                $"Hepsiburada: {orders.Count} paket cekildi ({startUtc:yyyy-MM-dd}..{endUtc:yyyy-MM-dd}).",
                orders);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Hepsiburada fetch hatasi (tenant={Tenant})", credentials.TenantKey);
            return new OrderChannelFetchResult(false,
                "Hepsiburada paket cekiminde beklenmedik hata: " + ex.Message,
                Array.Empty<RawIncomingOrder>());
        }
    }

    private HttpClient BuildClient(string merchantId, string username, string password)
    {
        var client = _httpClientFactory.CreateClient("hepsiburada-orders");
        var basic = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{username}:{password}"));
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", basic);
        client.DefaultRequestHeaders.UserAgent.ParseAdd($"{merchantId} - SelfIntegration");
        client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        client.Timeout = TimeSpan.FromSeconds(30);
        return client;
    }

    private static (string? merchantId, string? username, string? password, string? missingMsg)
        ReadCreds(OrderChannelCredentials c)
    {
        var merchantId = c.Get("merchantId");
        var username = c.Get("username");
        var password = c.Get("password");
        var missing = new List<string>();
        if (string.IsNullOrWhiteSpace(merchantId)) missing.Add("Merchant ID");
        if (string.IsNullOrWhiteSpace(username)) missing.Add("API Kullanici Adi");
        if (string.IsNullOrWhiteSpace(password)) missing.Add("API Sifresi");
        return missing.Count > 0
            ? (null, null, null, $"Eksik alanlar: {string.Join(", ", missing)}")
            : (merchantId, username, password, null);
    }

    private static IReadOnlyList<RawIncomingOrder> ParsePackages(string json, string tenantKey)
    {
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;
        // Bazi endpoint'ler {items:[...]}, bazilari raw array doner
        JsonElement items;
        if (root.ValueKind == JsonValueKind.Array)
            items = root;
        else if (root.TryGetProperty("items", out var i) && i.ValueKind == JsonValueKind.Array)
            items = i;
        else
            return Array.Empty<RawIncomingOrder>();

        var list = new List<RawIncomingOrder>(items.GetArrayLength());
        foreach (var p in items.EnumerateArray())
        {
            var packageNumber = p.TryGetProperty("packageNumber", out var pn) ? pn.GetString() ?? "" : "";
            var orderNumber = p.TryGetProperty("orderNumber", out var on) ? on.GetString() ?? packageNumber : packageNumber;
            if (string.IsNullOrEmpty(packageNumber)) continue;

            var orderDate = p.TryGetProperty("orderDate", out var od) && od.ValueKind == JsonValueKind.String
                && DateTimeOffset.TryParse(od.GetString(), out var parsed)
                ? parsed
                : DateTimeOffset.UtcNow;

            var recipient = ReadAddress(p, "shippingAddress");
            var orderItems = ReadItems(p);

            list.Add(new RawIncomingOrder(
                SourceChannel: OrderChannelType.Hepsiburada,
                ChannelExternalOrderId: $"hepsiburada:{packageNumber}",
                OrderNumber: orderNumber,
                CreatedAtUtc: orderDate,
                CollectionAmount: null,
                CurrencyCode: "TRY",
                Recipient: recipient,
                Items: orderItems,
                Notes: $"HB paket {packageNumber} ({tenantKey})"));
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
            FullName: Get("name") ?? "HB Musterisi",
            Phone: Get("phone"),
            Email: null,
            City: Get("city"),
            District: Get("town") ?? Get("district"),
            AddressLine: Get("address"),
            PostalCode: Get("postalCode"));
    }

    private static IReadOnlyList<RawOrderItem> ReadItems(JsonElement order)
    {
        if (!order.TryGetProperty("items", out var items) || items.ValueKind != JsonValueKind.Array)
            return Array.Empty<RawOrderItem>();
        var result = new List<RawOrderItem>(items.GetArrayLength());
        foreach (var l in items.EnumerateArray())
        {
            string? Get(string p) => l.TryGetProperty(p, out var v) && v.ValueKind == JsonValueKind.String
                ? v.GetString() : null;
            int qty = l.TryGetProperty("quantity", out var q) && q.ValueKind == JsonValueKind.Number
                ? q.GetInt32() : 1;
            decimal? price = l.TryGetProperty("unitPrice", out var p) && p.ValueKind == JsonValueKind.Number
                ? p.GetDecimal() : null;
            result.Add(new RawOrderItem(
                ProductName: Get("productName") ?? "HB urun",
                Quantity: qty,
                UnitPrice: price,
                Sku: Get("merchantSku") ?? Get("hbSku") ?? Get("sku")));
        }
        return result;
    }

    private static string Truncate(string s, int max) => s.Length <= max ? s : s.Substring(0, max) + "...";
}
