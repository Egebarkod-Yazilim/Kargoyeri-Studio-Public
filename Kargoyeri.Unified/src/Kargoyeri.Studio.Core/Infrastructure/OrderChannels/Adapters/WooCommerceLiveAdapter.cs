using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace Kargoyeri.Studio.Core.Infrastructure.OrderChannels.Adapters;

/// <summary>
/// WooCommerce REST API v3 (gercek/canli).
/// Docs:     https://woocommerce.github.io/woocommerce-rest-api-docs/
/// Endpoint: GET (siteUrl)/wp-json/wc/v3/orders?after=ISO8601 + per page=100
/// Auth:     Basic Auth — consumerKey/consumerSecret (HTTPS uzerinde)
///           HTTP icin: query string (consumer key/secret) — onerilmez
/// Rate:     WordPress host-bagimli, varsayilan limit yok
/// </summary>
public sealed class WooCommerceLiveAdapter : IOrderChannelAdapter
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<WooCommerceLiveAdapter> _logger;

    public OrderChannelType Type => OrderChannelType.WooCommerce;

    public WooCommerceLiveAdapter(IHttpClientFactory httpClientFactory, ILogger<WooCommerceLiveAdapter> logger)
    {
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    public async Task<OrderChannelTestResult> TestConnectionAsync(
        OrderChannelCredentials credentials, CancellationToken ct)
    {
        var (siteUrl, ck, cs, missing) = ReadCreds(credentials);
        if (missing is not null) return new OrderChannelTestResult(false, missing);

        try
        {
            // Hafif test: 1 siparis listele (auth dogrulamasi yeterli)
            var url = $"{siteUrl}/wp-json/wc/v3/orders?per_page=1";
            using var client = BuildClient(ck!, cs!);
            using var resp = await client.GetAsync(url, ct);
            if (resp.IsSuccessStatusCode)
            {
                return new OrderChannelTestResult(true, "WooCommerce API bilgileri dogrulandi.");
            }
            var body = await resp.Content.ReadAsStringAsync(ct);
            return new OrderChannelTestResult(false,
                $"WooCommerce API hata kodu: {(int)resp.StatusCode} {resp.StatusCode}",
                Truncate(body, 400));
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "WooCommerce test connection hatasi (tenant={Tenant})", credentials.TenantKey);
            return new OrderChannelTestResult(false, "WooCommerce API'ye ulasilamadi: " + ex.Message);
        }
    }

    public async Task<OrderChannelFetchResult> FetchOrdersAsync(
        OrderChannelCredentials credentials, DateTimeOffset since, CancellationToken ct)
    {
        var (siteUrl, ck, cs, missing) = ReadCreds(credentials);
        if (missing is not null) return new OrderChannelFetchResult(false, missing, Array.Empty<RawIncomingOrder>());

        var startUtc = since.ToUniversalTime();
        // WooCommerce "after" parametresi: created_at sonrasi (UTC ISO8601)
        // status=any tum durumlari ceker; processing|on-hold genelde yeni siparisler
        var url = $"{siteUrl}/wp-json/wc/v3/orders" +
                  $"?after={Uri.EscapeDataString(startUtc.ToString("o"))}" +
                  $"&per_page=100&orderby=date&order=desc";

        try
        {
            using var client = BuildClient(ck!, cs!);
            using var resp = await client.GetAsync(url, ct);
            var body = await resp.Content.ReadAsStringAsync(ct);

            if (!resp.IsSuccessStatusCode)
            {
                return new OrderChannelFetchResult(false,
                    $"WooCommerce API: {(int)resp.StatusCode} {resp.StatusCode} — {Truncate(body, 200)}",
                    Array.Empty<RawIncomingOrder>());
            }

            var orders = ParseOrders(body, credentials.TenantKey);
            return new OrderChannelFetchResult(true,
                $"WooCommerce: {orders.Count} siparis cekildi (since {startUtc:yyyy-MM-dd HH:mm}).",
                orders);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "WooCommerce fetch hatasi (tenant={Tenant})", credentials.TenantKey);
            return new OrderChannelFetchResult(false,
                "WooCommerce siparis cekiminde beklenmedik hata: " + ex.Message,
                Array.Empty<RawIncomingOrder>());
        }
    }

    private HttpClient BuildClient(string consumerKey, string consumerSecret)
    {
        var client = _httpClientFactory.CreateClient("woocommerce-orders");
        var basic = Convert.ToBase64String(Encoding.UTF8.GetBytes($"{consumerKey}:{consumerSecret}"));
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Basic", basic);
        client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        client.Timeout = TimeSpan.FromSeconds(30);
        return client;
    }

    private static (string? siteUrl, string? ck, string? cs, string? missing) ReadCreds(OrderChannelCredentials c)
    {
        var siteUrl = c.Get("siteUrl")?.Trim().TrimEnd('/');
        var ck = c.Get("consumerKey")?.Trim();
        var cs = c.Get("consumerSecret")?.Trim();

        // URL normalizasyonu: https:// otomatik eklenir, port korunur
        if (!string.IsNullOrWhiteSpace(siteUrl) && !siteUrl!.StartsWith("http", StringComparison.OrdinalIgnoreCase))
            siteUrl = "https://" + siteUrl;

        var missing = new List<string>();
        if (string.IsNullOrWhiteSpace(siteUrl)) missing.Add("Site URL");
        if (string.IsNullOrWhiteSpace(ck)) missing.Add("Consumer Key");
        if (string.IsNullOrWhiteSpace(cs)) missing.Add("Consumer Secret");
        return missing.Count > 0
            ? (null, null, null, $"Eksik alanlar: {string.Join(", ", missing)}")
            : (siteUrl, ck, cs, null);
    }

    private static IReadOnlyList<RawIncomingOrder> ParseOrders(string json, string tenantKey)
    {
        using var doc = JsonDocument.Parse(json);
        if (doc.RootElement.ValueKind != JsonValueKind.Array)
            return Array.Empty<RawIncomingOrder>();

        var arr = doc.RootElement;
        var list = new List<RawIncomingOrder>(arr.GetArrayLength());
        foreach (var o in arr.EnumerateArray())
        {
            var idStr = o.TryGetProperty("id", out var id) && id.ValueKind == JsonValueKind.Number
                ? id.GetInt64().ToString() : "";
            if (string.IsNullOrEmpty(idStr)) continue;

            var orderNumber = o.TryGetProperty("number", out var nm) && nm.ValueKind == JsonValueKind.String
                ? nm.GetString() ?? idStr : idStr;

            DateTimeOffset createdAt = DateTimeOffset.UtcNow;
            if (o.TryGetProperty("date_created_gmt", out var dc) && dc.ValueKind == JsonValueKind.String
                && DateTimeOffset.TryParse(dc.GetString() + "Z", out var parsed))
                createdAt = parsed;
            else if (o.TryGetProperty("date_created", out var dc2) && dc2.ValueKind == JsonValueKind.String
                && DateTimeOffset.TryParse(dc2.GetString(), out var parsed2))
                createdAt = parsed2;

            string currency = o.TryGetProperty("currency", out var cur) && cur.ValueKind == JsonValueKind.String
                ? cur.GetString() ?? "TRY" : "TRY";

            decimal? totalPrice = null;
            if (o.TryGetProperty("total", out var tp) && tp.ValueKind == JsonValueKind.String
                && decimal.TryParse(tp.GetString(),
                    System.Globalization.NumberStyles.Any,
                    System.Globalization.CultureInfo.InvariantCulture, out var tv))
                totalPrice = tv;

            // COD detection: payment_method == "cod" veya "cash_on_delivery"
            bool isCod = false;
            if (o.TryGetProperty("payment_method", out var pm) && pm.ValueKind == JsonValueKind.String)
            {
                var pmStr = (pm.GetString() ?? "").ToLowerInvariant();
                isCod = pmStr.Contains("cod") || pmStr.Contains("cash_on_delivery") || pmStr.Contains("kapida");
            }
            decimal? collection = isCod ? totalPrice : null;

            var recipient = ReadAddress(o, "shipping", o);
            var items = ReadItems(o);
            string? note = o.TryGetProperty("customer_note", out var nt) && nt.ValueKind == JsonValueKind.String
                ? nt.GetString() : null;

            list.Add(new RawIncomingOrder(
                SourceChannel: OrderChannelType.WooCommerce,
                ChannelExternalOrderId: $"woocommerce:{idStr}",
                OrderNumber: orderNumber,
                CreatedAtUtc: createdAt,
                CollectionAmount: collection,
                CurrencyCode: currency,
                Recipient: recipient,
                Items: items,
                Notes: string.IsNullOrWhiteSpace(note) ? $"WooCommerce siparis ({tenantKey})" : note));
        }
        return list;
    }

    private static RawAddress ReadAddress(JsonElement order, string key, JsonElement orderRoot)
    {
        if (!order.TryGetProperty(key, out var addr) || addr.ValueKind != JsonValueKind.Object)
            return new RawAddress("WooCommerce Musterisi", null, null, null, null, null);

        string? Get(string p) => addr.TryGetProperty(p, out var v) && v.ValueKind == JsonValueKind.String
            ? v.GetString() : null;

        var first = Get("first_name") ?? "";
        var last = Get("last_name") ?? "";
        var name = $"{first} {last}".Trim();
        if (string.IsNullOrWhiteSpace(name)) name = "WooCommerce Musterisi";

        // Billing'de telefon/email; shipping'de yoksa fallback
        string? phone = Get("phone");
        string? email = null;
        if (orderRoot.TryGetProperty("billing", out var billing) && billing.ValueKind == JsonValueKind.Object)
        {
            if (string.IsNullOrEmpty(phone)
                && billing.TryGetProperty("phone", out var bphone) && bphone.ValueKind == JsonValueKind.String)
                phone = bphone.GetString();
            if (billing.TryGetProperty("email", out var bemail) && bemail.ValueKind == JsonValueKind.String)
                email = bemail.GetString();
        }

        var line = Get("address_1");
        var line2 = Get("address_2");
        if (!string.IsNullOrWhiteSpace(line2)) line = $"{line} {line2}".Trim();

        return new RawAddress(
            FullName: name,
            Phone: phone,
            Email: email,
            City: Get("city"),
            District: Get("state"),
            AddressLine: line,
            PostalCode: Get("postcode"));
    }

    private static IReadOnlyList<RawOrderItem> ReadItems(JsonElement order)
    {
        if (!order.TryGetProperty("line_items", out var items) || items.ValueKind != JsonValueKind.Array)
            return Array.Empty<RawOrderItem>();

        var result = new List<RawOrderItem>(items.GetArrayLength());
        foreach (var l in items.EnumerateArray())
        {
            string? GetStr(string p) => l.TryGetProperty(p, out var v) && v.ValueKind == JsonValueKind.String
                ? v.GetString() : null;

            int qty = l.TryGetProperty("quantity", out var q) && q.ValueKind == JsonValueKind.Number
                ? q.GetInt32() : 1;

            decimal? price = null;
            if (l.TryGetProperty("price", out var p))
            {
                if (p.ValueKind == JsonValueKind.Number) price = p.GetDecimal();
                else if (p.ValueKind == JsonValueKind.String
                    && decimal.TryParse(p.GetString(),
                        System.Globalization.NumberStyles.Any,
                        System.Globalization.CultureInfo.InvariantCulture, out var pv))
                    price = pv;
            }

            result.Add(new RawOrderItem(
                ProductName: GetStr("name") ?? "WooCommerce urun",
                Quantity: qty,
                UnitPrice: price,
                Sku: GetStr("sku")));
        }
        return result;
    }

    private static string Truncate(string s, int max) =>
        s.Length <= max ? s : s.Substring(0, max) + "...";
}
