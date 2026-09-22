using System.Net.Http.Headers;
using System.Text.Json;

namespace Kargoyeri.Studio.Core.Infrastructure.OrderChannels.Adapters;

/// <summary>
/// Shopify Admin REST API (gercek/canli).
/// Docs:     https://shopify.dev/docs/api/admin-rest/2024-10/resources/order
/// Endpoint: GET https://{shop}.myshopify.com/admin/api/{version}/orders.json
/// Auth:     X-Shopify-Access-Token header
/// Filter:   updated_at_min=ISO8601 + status=any (varsayilan open siparisleri kacirmamak icin)
/// Rate:     2 req/s (bucket-based, Retry-After header'i takip eder)
/// </summary>
public sealed class ShopifyLiveAdapter : IOrderChannelAdapter
{
    private const string DefaultApiVersion = "2024-10";
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<ShopifyLiveAdapter> _logger;

    public OrderChannelType Type => OrderChannelType.Shopify;

    public ShopifyLiveAdapter(IHttpClientFactory httpClientFactory, ILogger<ShopifyLiveAdapter> logger)
    {
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    public async Task<OrderChannelTestResult> TestConnectionAsync(
        OrderChannelCredentials credentials, CancellationToken ct)
    {
        var (shop, token, version, missing) = ReadCreds(credentials);
        if (missing is not null) return new OrderChannelTestResult(false, missing);

        try
        {
            // Hafif endpoint: shop bilgisi — auth'u dogrular, sayim 0 olabilir.
            var url = $"https://{shop}/admin/api/{version}/shop.json";
            using var client = BuildClient(token!);
            using var resp = await client.GetAsync(url, ct);
            if (resp.IsSuccessStatusCode)
            {
                return new OrderChannelTestResult(true, "Shopify API bilgileri dogrulandi.");
            }
            var body = await resp.Content.ReadAsStringAsync(ct);
            return new OrderChannelTestResult(false,
                $"Shopify API hata kodu: {(int)resp.StatusCode} {resp.StatusCode}",
                Truncate(body, 400));
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Shopify test connection hatasi (tenant={Tenant})", credentials.TenantKey);
            return new OrderChannelTestResult(false, "Shopify API'ye ulasilamadi: " + ex.Message);
        }
    }

    public async Task<OrderChannelFetchResult> FetchOrdersAsync(
        OrderChannelCredentials credentials, DateTimeOffset since, CancellationToken ct)
    {
        var (shop, token, version, missing) = ReadCreds(credentials);
        if (missing is not null) return new OrderChannelFetchResult(false, missing, Array.Empty<RawIncomingOrder>());

        var startUtc = since.ToUniversalTime();
        // Shopify ISO8601 ile filtreler; status=any tum (open+closed+cancelled) gosterir.
        var url = $"https://{shop}/admin/api/{version}/orders.json" +
                  $"?status=any&updated_at_min={Uri.EscapeDataString(startUtc.ToString("o"))}&limit=250";

        try
        {
            using var client = BuildClient(token!);
            using var resp = await client.GetAsync(url, ct);
            var body = await resp.Content.ReadAsStringAsync(ct);

            if (!resp.IsSuccessStatusCode)
            {
                return new OrderChannelFetchResult(false,
                    $"Shopify API: {(int)resp.StatusCode} {resp.StatusCode} — {Truncate(body, 200)}",
                    Array.Empty<RawIncomingOrder>());
            }

            var orders = ParseOrders(body, credentials.TenantKey);
            return new OrderChannelFetchResult(true,
                $"Shopify: {orders.Count} siparis cekildi (since {startUtc:yyyy-MM-dd HH:mm}).",
                orders);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Shopify fetch hatasi (tenant={Tenant})", credentials.TenantKey);
            return new OrderChannelFetchResult(false,
                "Shopify siparis cekiminde beklenmedik hata: " + ex.Message,
                Array.Empty<RawIncomingOrder>());
        }
    }

    private HttpClient BuildClient(string accessToken)
    {
        var client = _httpClientFactory.CreateClient("shopify-orders");
        client.DefaultRequestHeaders.Add("X-Shopify-Access-Token", accessToken);
        client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        client.Timeout = TimeSpan.FromSeconds(30);
        return client;
    }

    private static (string? shop, string? token, string version, string? missing) ReadCreds(OrderChannelCredentials c)
    {
        var shop = c.Get("shopDomain")?.Trim();
        var token = c.Get("accessToken")?.Trim();
        var version = c.Get("apiVersion")?.Trim();
        if (string.IsNullOrWhiteSpace(version)) version = DefaultApiVersion;

        // Shop domain normalizasyonu: "https://" / sondaki "/" temizlenir, .myshopify.com eklenir
        if (!string.IsNullOrWhiteSpace(shop))
        {
            shop = shop.Replace("https://", "").Replace("http://", "").TrimEnd('/');
            if (!shop.Contains(".myshopify.com", StringComparison.OrdinalIgnoreCase))
                shop = shop + ".myshopify.com";
        }

        var missing = new List<string>();
        if (string.IsNullOrWhiteSpace(shop)) missing.Add("Shop Domain");
        if (string.IsNullOrWhiteSpace(token)) missing.Add("Access Token");
        return missing.Count > 0
            ? (null, null, version, $"Eksik alanlar: {string.Join(", ", missing)}")
            : (shop, token, version, null);
    }

    private static IReadOnlyList<RawIncomingOrder> ParseOrders(string json, string tenantKey)
    {
        using var doc = JsonDocument.Parse(json);
        if (!doc.RootElement.TryGetProperty("orders", out var arr) || arr.ValueKind != JsonValueKind.Array)
            return Array.Empty<RawIncomingOrder>();

        var list = new List<RawIncomingOrder>(arr.GetArrayLength());
        foreach (var o in arr.EnumerateArray())
        {
            // Shopify "id" numeric, "name" insan tarafindan okunan ("#1001")
            var idStr = o.TryGetProperty("id", out var id) && id.ValueKind == JsonValueKind.Number
                ? id.GetInt64().ToString()
                : (o.TryGetProperty("id", out var id2) ? id2.GetString() ?? "" : "");
            if (string.IsNullOrEmpty(idStr)) continue;

            var orderNumber = o.TryGetProperty("name", out var nm) ? nm.GetString() ?? idStr : idStr;
            // Shopify "name" prefix '#' atilir
            if (orderNumber.StartsWith("#")) orderNumber = orderNumber.Substring(1);

            DateTimeOffset createdAt = DateTimeOffset.UtcNow;
            if (o.TryGetProperty("created_at", out var ca) && ca.ValueKind == JsonValueKind.String
                && DateTimeOffset.TryParse(ca.GetString(), out var parsed)) createdAt = parsed;

            string currency = o.TryGetProperty("currency", out var cur) && cur.ValueKind == JsonValueKind.String
                ? cur.GetString() ?? "TRY" : "TRY";

            decimal? totalPrice = null;
            if (o.TryGetProperty("total_price", out var tp) && tp.ValueKind == JsonValueKind.String
                && decimal.TryParse(tp.GetString(),
                    System.Globalization.NumberStyles.Any,
                    System.Globalization.CultureInfo.InvariantCulture, out var tv))
                totalPrice = tv;

            // COD detection: gateways "cash_on_delivery" iceriyorsa nakit tahsilat
            bool isCod = false;
            if (o.TryGetProperty("gateway", out var gw) && gw.ValueKind == JsonValueKind.String)
                isCod = (gw.GetString() ?? "").Contains("cash", StringComparison.OrdinalIgnoreCase);
            decimal? collection = isCod ? totalPrice : null;

            var recipient = ReadAddress(o, "shipping_address", o);
            var items = ReadItems(o);
            string? customerNote = o.TryGetProperty("note", out var nt) && nt.ValueKind == JsonValueKind.String
                ? nt.GetString() : null;

            list.Add(new RawIncomingOrder(
                SourceChannel: OrderChannelType.Shopify,
                ChannelExternalOrderId: $"shopify:{idStr}",
                OrderNumber: orderNumber,
                CreatedAtUtc: createdAt,
                CollectionAmount: collection,
                CurrencyCode: currency,
                Recipient: recipient,
                Items: items,
                Notes: customerNote ?? $"Shopify siparis ({tenantKey})"));
        }
        return list;
    }

    private static RawAddress ReadAddress(JsonElement order, string key, JsonElement orderRoot)
    {
        if (!order.TryGetProperty(key, out var addr) || addr.ValueKind != JsonValueKind.Object)
            return new RawAddress("Shopify Musterisi", null, null, null, null, null);

        string? Get(string p) => addr.TryGetProperty(p, out var v) && v.ValueKind == JsonValueKind.String
            ? v.GetString() : null;

        var first = Get("first_name") ?? "";
        var last = Get("last_name") ?? "";
        var name = $"{first} {last}".Trim();
        if (string.IsNullOrWhiteSpace(name)) name = Get("name") ?? "Shopify Musterisi";

        // Email order root'unda
        string? email = null;
        if (orderRoot.TryGetProperty("email", out var em) && em.ValueKind == JsonValueKind.String)
            email = em.GetString();

        // Shopify: address1 + address2; city; province; country; zip
        var line = Get("address1");
        var line2 = Get("address2");
        if (!string.IsNullOrWhiteSpace(line2)) line = $"{line} {line2}".Trim();

        return new RawAddress(
            FullName: name,
            Phone: Get("phone"),
            Email: email,
            City: Get("city") ?? Get("province"),
            District: Get("province") ?? null,
            AddressLine: line,
            PostalCode: Get("zip"));
    }

    private static IReadOnlyList<RawOrderItem> ReadItems(JsonElement order)
    {
        if (!order.TryGetProperty("line_items", out var items) || items.ValueKind != JsonValueKind.Array)
            return Array.Empty<RawOrderItem>();

        var result = new List<RawOrderItem>(items.GetArrayLength());
        foreach (var l in items.EnumerateArray())
        {
            string? Get(string p) => l.TryGetProperty(p, out var v) && v.ValueKind == JsonValueKind.String
                ? v.GetString() : null;

            int qty = l.TryGetProperty("quantity", out var q) && q.ValueKind == JsonValueKind.Number
                ? q.GetInt32() : 1;

            decimal? price = null;
            if (l.TryGetProperty("price", out var p) && p.ValueKind == JsonValueKind.String
                && decimal.TryParse(p.GetString(),
                    System.Globalization.NumberStyles.Any,
                    System.Globalization.CultureInfo.InvariantCulture, out var pv))
                price = pv;

            decimal? weight = null;
            if (l.TryGetProperty("grams", out var g) && g.ValueKind == JsonValueKind.Number)
                weight = g.GetDecimal() / 1000m;

            result.Add(new RawOrderItem(
                ProductName: Get("title") ?? Get("name") ?? "Shopify urun",
                Quantity: qty,
                UnitPrice: price,
                Sku: Get("sku") ?? Get("variant_id"),
                WeightKg: weight));
        }
        return result;
    }

    private static string Truncate(string s, int max) =>
        s.Length <= max ? s : s.Substring(0, max) + "...";
}
