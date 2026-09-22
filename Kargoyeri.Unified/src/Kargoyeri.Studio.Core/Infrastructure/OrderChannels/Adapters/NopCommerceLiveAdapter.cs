using System.Collections.Concurrent;
using System.Net.Http.Headers;
using System.Text.Json;

namespace Kargoyeri.Studio.Core.Infrastructure.OrderChannels.Adapters;

/// <summary>
/// NopCommerce Web API (4.40+) — gercek/canli.
/// Docs:     https://docs.nopcommerce.com/en/developer/plugins/web-api-plugin.html
/// Prerequisite: NopCommerce instance'inda "Nop.Plugin.Misc.WebApi" yuklu olmali.
/// Auth:     POST /api/customers/login (username/password) -> JWT access token
///           Sonraki istekler: Authorization: Bearer {token}
/// Endpoint: GET /api/orders?createdOnFrom=ISO8601
/// Rate:     Self-hosted oldugu icin sunucu konfigurasyonuna bagli
/// </summary>
public sealed class NopCommerceLiveAdapter : IOrderChannelAdapter
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<NopCommerceLiveAdapter> _logger;

    // (tenantKey, siteUrl) -> (token, expiresAtUtc)
    private static readonly ConcurrentDictionary<string, (string token, DateTimeOffset expiresAt)> _tokenCache = new();

    public OrderChannelType Type => OrderChannelType.NopCommerce;

    public NopCommerceLiveAdapter(IHttpClientFactory httpClientFactory, ILogger<NopCommerceLiveAdapter> logger)
    {
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    public async Task<OrderChannelTestResult> TestConnectionAsync(
        OrderChannelCredentials credentials, CancellationToken ct)
    {
        var (siteUrl, username, password, missing) = ReadCreds(credentials);
        if (missing is not null) return new OrderChannelTestResult(false, missing);

        try
        {
            var token = await EnsureTokenAsync(credentials.TenantKey, siteUrl!, username!, password!, ct);
            if (token is null)
                return new OrderChannelTestResult(false,
                    "NopCommerce JWT alinamadi. Web API plugin'in yuklu ve API yetkisi olan bir kullanici oldugundan emin olun.");

            // Hafif test: bir sayfa siparis (auth + plugin presence dogrulamasi)
            var url = $"{siteUrl}/api/orders?page=1&pageSize=1";
            using var client = BuildClient(token);
            using var resp = await client.GetAsync(url, ct);
            if (resp.IsSuccessStatusCode)
                return new OrderChannelTestResult(true, "NopCommerce API bilgileri dogrulandi.");

            var body = await resp.Content.ReadAsStringAsync(ct);
            return new OrderChannelTestResult(false,
                $"NopCommerce API hata kodu: {(int)resp.StatusCode} {resp.StatusCode}",
                Truncate(body, 400));
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "NopCommerce test connection hatasi (tenant={Tenant})", credentials.TenantKey);
            return new OrderChannelTestResult(false, "NopCommerce API'ye ulasilamadi: " + ex.Message);
        }
    }

    public async Task<OrderChannelFetchResult> FetchOrdersAsync(
        OrderChannelCredentials credentials, DateTimeOffset since, CancellationToken ct)
    {
        var (siteUrl, username, password, missing) = ReadCreds(credentials);
        if (missing is not null) return new OrderChannelFetchResult(false, missing, Array.Empty<RawIncomingOrder>());

        var startUtc = since.ToUniversalTime();
        try
        {
            var token = await EnsureTokenAsync(credentials.TenantKey, siteUrl!, username!, password!, ct);
            if (token is null)
                return new OrderChannelFetchResult(false,
                    "NopCommerce JWT alinamadi (login basarisiz).",
                    Array.Empty<RawIncomingOrder>());

            var url = $"{siteUrl}/api/orders" +
                      $"?createdOnFrom={Uri.EscapeDataString(startUtc.ToString("o"))}" +
                      $"&page=1&pageSize=100";

            using var client = BuildClient(token);
            using var resp = await client.GetAsync(url, ct);
            var body = await resp.Content.ReadAsStringAsync(ct);

            if (!resp.IsSuccessStatusCode)
            {
                return new OrderChannelFetchResult(false,
                    $"NopCommerce API: {(int)resp.StatusCode} {resp.StatusCode} — {Truncate(body, 200)}",
                    Array.Empty<RawIncomingOrder>());
            }

            var orders = ParseOrders(body, credentials.TenantKey);
            return new OrderChannelFetchResult(true,
                $"NopCommerce: {orders.Count} siparis cekildi (since {startUtc:yyyy-MM-dd HH:mm}).",
                orders);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "NopCommerce fetch hatasi (tenant={Tenant})", credentials.TenantKey);
            return new OrderChannelFetchResult(false,
                "NopCommerce siparis cekiminde beklenmedik hata: " + ex.Message,
                Array.Empty<RawIncomingOrder>());
        }
    }

    private async Task<string?> EnsureTokenAsync(
        string tenantKey, string siteUrl, string username, string password, CancellationToken ct)
    {
        var cacheKey = $"{tenantKey}|{siteUrl}";

        if (_tokenCache.TryGetValue(cacheKey, out var entry)
            && entry.expiresAt > DateTimeOffset.UtcNow.AddSeconds(30))
        {
            return entry.token;
        }

        var loginUrl = $"{siteUrl}/api/customers/login";
        using var client = _httpClientFactory.CreateClient("nopcommerce-auth");
        client.Timeout = TimeSpan.FromSeconds(20);

        var payload = new { Username = username, Password = password };
        using var req = new HttpRequestMessage(HttpMethod.Post, loginUrl);
        req.Content = new StringContent(
            JsonSerializer.Serialize(payload),
            System.Text.Encoding.UTF8,
            "application/json");

        using var resp = await client.SendAsync(req, ct);
        var body = await resp.Content.ReadAsStringAsync(ct);
        if (!resp.IsSuccessStatusCode)
        {
            _logger.LogWarning("NopCommerce login hata: {Code} {Body}",
                (int)resp.StatusCode, Truncate(body, 200));
            return null;
        }

        using var doc = JsonDocument.Parse(body);
        // Tipik response: { "token_type": "Bearer", "access_token": "...", "expires_in": 3600 }
        // veya: { "data": { "token": "..." } }
        string? token = null;
        int expiresIn = 3600;

        if (doc.RootElement.TryGetProperty("access_token", out var t1) && t1.ValueKind == JsonValueKind.String)
            token = t1.GetString();
        else if (doc.RootElement.TryGetProperty("token", out var t2) && t2.ValueKind == JsonValueKind.String)
            token = t2.GetString();
        else if (doc.RootElement.TryGetProperty("data", out var d) && d.ValueKind == JsonValueKind.Object
            && d.TryGetProperty("token", out var t3) && t3.ValueKind == JsonValueKind.String)
            token = t3.GetString();

        if (doc.RootElement.TryGetProperty("expires_in", out var ex) && ex.ValueKind == JsonValueKind.Number)
            expiresIn = ex.GetInt32();

        if (string.IsNullOrEmpty(token)) return null;

        var expiresAt = DateTimeOffset.UtcNow.AddSeconds(expiresIn);
        _tokenCache[cacheKey] = (token, expiresAt);
        return token;
    }

    private HttpClient BuildClient(string accessToken)
    {
        var client = _httpClientFactory.CreateClient("nopcommerce-orders");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        client.Timeout = TimeSpan.FromSeconds(30);
        return client;
    }

    private static (string? siteUrl, string? username, string? password, string? missing) ReadCreds(OrderChannelCredentials c)
    {
        var siteUrl = c.Get("siteUrl")?.Trim().TrimEnd('/');
        var username = c.Get("username")?.Trim();
        var password = c.Get("password")?.Trim();

        if (!string.IsNullOrWhiteSpace(siteUrl) && !siteUrl!.StartsWith("http", StringComparison.OrdinalIgnoreCase))
            siteUrl = "https://" + siteUrl;

        var missing = new List<string>();
        if (string.IsNullOrWhiteSpace(siteUrl)) missing.Add("Site URL");
        if (string.IsNullOrWhiteSpace(username)) missing.Add("Username");
        if (string.IsNullOrWhiteSpace(password)) missing.Add("Password");
        return missing.Count > 0
            ? (null, null, null, $"Eksik alanlar: {string.Join(", ", missing)}")
            : (siteUrl, username, password, null);
    }

    private static IReadOnlyList<RawIncomingOrder> ParseOrders(string json, string tenantKey)
    {
        using var doc = JsonDocument.Parse(json);
        // NopCommerce: {"orders":[...]} veya array direkt
        JsonElement arr;
        if (doc.RootElement.TryGetProperty("orders", out var ord) && ord.ValueKind == JsonValueKind.Array)
            arr = ord;
        else if (doc.RootElement.ValueKind == JsonValueKind.Array)
            arr = doc.RootElement;
        else
            return Array.Empty<RawIncomingOrder>();

        var list = new List<RawIncomingOrder>(arr.GetArrayLength());
        foreach (var o in arr.EnumerateArray())
        {
            string idStr = "";
            if (o.TryGetProperty("id", out var id) && id.ValueKind == JsonValueKind.Number)
                idStr = id.GetInt64().ToString();
            if (string.IsNullOrEmpty(idStr)) continue;

            // OrderNumber yoksa custom_order_number fallback, sonra id
            var orderNumber = idStr;
            if (o.TryGetProperty("custom_order_number", out var con) && con.ValueKind == JsonValueKind.String
                && !string.IsNullOrWhiteSpace(con.GetString()))
                orderNumber = con.GetString()!;
            else if (o.TryGetProperty("order_number", out var on) && on.ValueKind == JsonValueKind.String
                && !string.IsNullOrWhiteSpace(on.GetString()))
                orderNumber = on.GetString()!;

            DateTimeOffset createdAt = DateTimeOffset.UtcNow;
            if (o.TryGetProperty("created_on_utc", out var ca) && ca.ValueKind == JsonValueKind.String
                && DateTimeOffset.TryParse(ca.GetString() + "Z", out var parsed))
                createdAt = parsed;

            string currency = o.TryGetProperty("customer_currency_code", out var cur) && cur.ValueKind == JsonValueKind.String
                ? cur.GetString() ?? "TRY" : "TRY";

            decimal? totalPrice = null;
            if (o.TryGetProperty("order_total", out var tp) && tp.ValueKind == JsonValueKind.Number)
                totalPrice = tp.GetDecimal();

            // COD detection: payment_method_system_name
            bool isCod = false;
            if (o.TryGetProperty("payment_method_system_name", out var pm) && pm.ValueKind == JsonValueKind.String)
            {
                var pmStr = (pm.GetString() ?? "").ToLowerInvariant();
                isCod = pmStr.Contains("cashondelivery") || pmStr.Contains("cod") || pmStr.Contains("kapida");
            }
            decimal? collection = isCod ? totalPrice : null;

            var recipient = ReadAddress(o, "shipping_address");
            var items = ReadItems(o);
            string? note = o.TryGetProperty("order_notes", out var nt) && nt.ValueKind == JsonValueKind.Array
                ? string.Join("; ", nt.EnumerateArray()
                    .Select(n => n.TryGetProperty("note", out var nv) && nv.ValueKind == JsonValueKind.String ? nv.GetString() : null)
                    .Where(s => !string.IsNullOrWhiteSpace(s)))
                : null;

            list.Add(new RawIncomingOrder(
                SourceChannel: OrderChannelType.NopCommerce,
                ChannelExternalOrderId: $"nopcommerce:{idStr}",
                OrderNumber: orderNumber,
                CreatedAtUtc: createdAt,
                CollectionAmount: collection,
                CurrencyCode: currency,
                Recipient: recipient,
                Items: items,
                Notes: string.IsNullOrWhiteSpace(note) ? $"NopCommerce siparis ({tenantKey})" : note));
        }
        return list;
    }

    private static RawAddress ReadAddress(JsonElement order, string key)
    {
        if (!order.TryGetProperty(key, out var addr) || addr.ValueKind != JsonValueKind.Object)
            return new RawAddress("NopCommerce Musterisi", null, null, null, null, null);

        string? Get(string p) => addr.TryGetProperty(p, out var v) && v.ValueKind == JsonValueKind.String
            ? v.GetString() : null;

        var first = Get("first_name") ?? "";
        var last = Get("last_name") ?? "";
        var name = $"{first} {last}".Trim();
        if (string.IsNullOrWhiteSpace(name)) name = "NopCommerce Musterisi";

        var line = Get("address1");
        var line2 = Get("address2");
        if (!string.IsNullOrWhiteSpace(line2)) line = $"{line} {line2}".Trim();

        return new RawAddress(
            FullName: name,
            Phone: Get("phone_number") ?? Get("phone"),
            Email: Get("email"),
            City: Get("city"),
            District: Get("county") ?? Get("state_province_name"),
            AddressLine: line,
            PostalCode: Get("zip_postal_code"));
    }

    private static IReadOnlyList<RawOrderItem> ReadItems(JsonElement order)
    {
        if (!order.TryGetProperty("order_items", out var items) || items.ValueKind != JsonValueKind.Array)
            return Array.Empty<RawOrderItem>();

        var result = new List<RawOrderItem>(items.GetArrayLength());
        foreach (var l in items.EnumerateArray())
        {
            string? GetStr(string p) => l.TryGetProperty(p, out var v) && v.ValueKind == JsonValueKind.String
                ? v.GetString() : null;

            int qty = l.TryGetProperty("quantity", out var q) && q.ValueKind == JsonValueKind.Number
                ? q.GetInt32() : 1;

            decimal? price = null;
            if (l.TryGetProperty("unit_price_incl_tax", out var p) && p.ValueKind == JsonValueKind.Number)
                price = p.GetDecimal();
            else if (l.TryGetProperty("unit_price_excl_tax", out var p2) && p2.ValueKind == JsonValueKind.Number)
                price = p2.GetDecimal();

            // Product adi nested olabilir
            string? productName = null;
            if (l.TryGetProperty("product", out var pr) && pr.ValueKind == JsonValueKind.Object
                && pr.TryGetProperty("name", out var pn) && pn.ValueKind == JsonValueKind.String)
                productName = pn.GetString();
            productName ??= GetStr("product_name");

            string? sku = null;
            if (l.TryGetProperty("product", out var pr2) && pr2.ValueKind == JsonValueKind.Object
                && pr2.TryGetProperty("sku", out var ps) && ps.ValueKind == JsonValueKind.String)
                sku = ps.GetString();

            result.Add(new RawOrderItem(
                ProductName: productName ?? "NopCommerce urun",
                Quantity: qty,
                UnitPrice: price,
                Sku: sku));
        }
        return result;
    }

    private static string Truncate(string s, int max) =>
        s.Length <= max ? s : s.Substring(0, max) + "...";
}
