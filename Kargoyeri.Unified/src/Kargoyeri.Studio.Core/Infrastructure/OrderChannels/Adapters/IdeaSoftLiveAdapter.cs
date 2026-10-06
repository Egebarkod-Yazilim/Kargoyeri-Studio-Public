using System.Collections.Concurrent;
using System.Net.Http.Headers;
using System.Text.Json;

namespace Kargoyeri.Studio.Core.Infrastructure.OrderChannels.Adapters;

/// <summary>
/// IdeaSoft REST API (gercek/canli).
/// Docs:     https://developer.ideasoft.com.tr
/// Endpoint: GET (storeUrl)/api/orders?sinceId=... + dateMin=YYYY-MM-DD
/// Auth:     OAuth2 Client Credentials -> POST /oauth/v2/token (grantType=clientCredentials)
///           -> Bearer (accessToken)
/// Rate:     60 req/min (token bucket)
/// Not:      Token TTL ~3600s; cache'lenir (per-tenant in-memory)
/// </summary>
public sealed class IdeaSoftLiveAdapter : IOrderChannelAdapter
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<IdeaSoftLiveAdapter> _logger;

    // (tenantKey, storeUrl) -> (token, expiresAtUtc)
    private static readonly ConcurrentDictionary<string, (string token, DateTimeOffset expiresAt)> _tokenCache = new();

    public OrderChannelType Type => OrderChannelType.IdeaSoft;

    public IdeaSoftLiveAdapter(IHttpClientFactory httpClientFactory, ILogger<IdeaSoftLiveAdapter> logger)
    {
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    public async Task<OrderChannelTestResult> TestConnectionAsync(
        OrderChannelCredentials credentials, CancellationToken ct)
    {
        var (storeUrl, clientId, clientSecret, missing) = ReadCreds(credentials);
        if (missing is not null) return new OrderChannelTestResult(false, missing);

        try
        {
            var token = await EnsureTokenAsync(credentials.TenantKey, storeUrl!, clientId!, clientSecret!, ct);
            if (token is null)
                return new OrderChannelTestResult(false, "IdeaSoft OAuth token alinamadi (clientId/secret hatali olabilir).");

            // Hafif test: 1 sayfa, 1 boyut
            var url = $"{storeUrl}/api/orders?limit=1";
            using var client = BuildClient(token);
            using var resp = await client.GetAsync(url, ct);
            if (resp.IsSuccessStatusCode)
            {
                return new OrderChannelTestResult(true, "IdeaSoft API bilgileri dogrulandi.");
            }
            var body = await resp.Content.ReadAsStringAsync(ct);
            return new OrderChannelTestResult(false,
                $"IdeaSoft API hata kodu: {(int)resp.StatusCode} {resp.StatusCode}",
                Truncate(body, 400));
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "IdeaSoft test connection hatasi (tenant={Tenant})", credentials.TenantKey);
            return new OrderChannelTestResult(false, "IdeaSoft API'ye ulasilamadi: " + ex.Message);
        }
    }

    public async Task<OrderChannelFetchResult> FetchOrdersAsync(
        OrderChannelCredentials credentials, DateTimeOffset since, CancellationToken ct)
    {
        var (storeUrl, clientId, clientSecret, missing) = ReadCreds(credentials);
        if (missing is not null) return new OrderChannelFetchResult(false, missing, Array.Empty<RawIncomingOrder>());

        var startUtc = since.ToUniversalTime();
        try
        {
            var token = await EnsureTokenAsync(credentials.TenantKey, storeUrl!, clientId!, clientSecret!, ct);
            if (token is null)
                return new OrderChannelFetchResult(false,
                    "IdeaSoft OAuth token alinamadi (clientId/secret hatali olabilir).",
                    Array.Empty<RawIncomingOrder>());

            // IdeaSoft "date_min" YYYY-MM-DD formatinda calisir; saatlik granularite icin
            // sonra in-memory filtre uygulariz.
            var url = $"{storeUrl}/api/orders" +
                      $"?date_min={Uri.EscapeDataString(startUtc.ToString("yyyy-MM-dd"))}" +
                      $"&limit=100&page=1&sort=-id";

            using var client = BuildClient(token);
            using var resp = await client.GetAsync(url, ct);
            var body = await resp.Content.ReadAsStringAsync(ct);

            if (!resp.IsSuccessStatusCode)
            {
                return new OrderChannelFetchResult(false,
                    $"IdeaSoft API: {(int)resp.StatusCode} {resp.StatusCode} — {Truncate(body, 200)}",
                    Array.Empty<RawIncomingOrder>());
            }

            var orders = ParseOrders(body, credentials.TenantKey, startUtc);
            return new OrderChannelFetchResult(true,
                $"IdeaSoft: {orders.Count} siparis cekildi (since {startUtc:yyyy-MM-dd}).",
                orders);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "IdeaSoft fetch hatasi (tenant={Tenant})", credentials.TenantKey);
            return new OrderChannelFetchResult(false,
                "IdeaSoft siparis cekiminde beklenmedik hata: " + ex.Message,
                Array.Empty<RawIncomingOrder>());
        }
    }

    private async Task<string?> EnsureTokenAsync(string tenantKey, string storeUrl, string clientId, string clientSecret, CancellationToken ct)
    {
        var cacheKey = $"{tenantKey}|{storeUrl}";

        if (_tokenCache.TryGetValue(cacheKey, out var entry)
            && entry.expiresAt > DateTimeOffset.UtcNow.AddSeconds(30))
        {
            return entry.token;
        }

        // Token al
        var tokenUrl = $"{storeUrl}/oauth/v2/token";
        using var client = _httpClientFactory.CreateClient("ideasoft-oauth");
        client.Timeout = TimeSpan.FromSeconds(20);

        using var req = new HttpRequestMessage(HttpMethod.Post, tokenUrl);
        req.Content = new FormUrlEncodedContent(new Dictionary<string, string>
        {
            ["grant_type"] = "client_credentials",
            ["client_id"] = clientId,
            ["client_secret"] = clientSecret
        });

        using var resp = await client.SendAsync(req, ct);
        var body = await resp.Content.ReadAsStringAsync(ct);
        if (!resp.IsSuccessStatusCode)
        {
            _logger.LogWarning("IdeaSoft token endpoint hata: {Code} {Body}",
                (int)resp.StatusCode, Truncate(body, 200));
            return null;
        }

        using var doc = JsonDocument.Parse(body);
        if (!doc.RootElement.TryGetProperty("access_token", out var tokenEl)
            || tokenEl.ValueKind != JsonValueKind.String) return null;

        var accessToken = tokenEl.GetString()!;
        var expiresIn = doc.RootElement.TryGetProperty("expires_in", out var ex)
                        && ex.ValueKind == JsonValueKind.Number
            ? ex.GetInt32() : 3600;

        var expiresAt = DateTimeOffset.UtcNow.AddSeconds(expiresIn);
        _tokenCache[cacheKey] = (accessToken, expiresAt);
        return accessToken;
    }

    private HttpClient BuildClient(string accessToken)
    {
        var client = _httpClientFactory.CreateClient("ideasoft-orders");
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        client.Timeout = TimeSpan.FromSeconds(30);
        return client;
    }

    private static (string? storeUrl, string? clientId, string? clientSecret, string? missing) ReadCreds(OrderChannelCredentials c)
    {
        var storeUrl = c.Get("storeUrl")?.Trim().TrimEnd('/');
        var clientId = c.Get("clientId")?.Trim();
        var clientSecret = c.Get("clientSecret")?.Trim();

        if (!string.IsNullOrWhiteSpace(storeUrl) && !storeUrl!.StartsWith("http", StringComparison.OrdinalIgnoreCase))
            storeUrl = "https://" + storeUrl;

        var missing = new List<string>();
        if (string.IsNullOrWhiteSpace(storeUrl)) missing.Add("Store URL");
        if (string.IsNullOrWhiteSpace(clientId)) missing.Add("Client ID");
        if (string.IsNullOrWhiteSpace(clientSecret)) missing.Add("Client Secret");
        return missing.Count > 0
            ? (null, null, null, $"Eksik alanlar: {string.Join(", ", missing)}")
            : (storeUrl, clientId, clientSecret, null);
    }

    private static IReadOnlyList<RawIncomingOrder> ParseOrders(string json, string tenantKey, DateTimeOffset filterSince)
    {
        using var doc = JsonDocument.Parse(json);
        // IdeaSoft response sekli: ya {"orders": [...]} ya da direkt array
        JsonElement arr;
        if (doc.RootElement.ValueKind == JsonValueKind.Array)
            arr = doc.RootElement;
        else if (doc.RootElement.TryGetProperty("orders", out var ord) && ord.ValueKind == JsonValueKind.Array)
            arr = ord;
        else
            return Array.Empty<RawIncomingOrder>();

        var list = new List<RawIncomingOrder>(arr.GetArrayLength());
        foreach (var o in arr.EnumerateArray())
        {
            var idStr = "";
            if (o.TryGetProperty("id", out var id))
            {
                idStr = id.ValueKind == JsonValueKind.Number ? id.GetInt64().ToString()
                      : id.ValueKind == JsonValueKind.String ? (id.GetString() ?? "") : "";
            }
            if (string.IsNullOrEmpty(idStr)) continue;

            var orderNumber = o.TryGetProperty("orderNumber", out var nm) && nm.ValueKind == JsonValueKind.String
                ? nm.GetString() ?? idStr
                : (o.TryGetProperty("order_number", out var nm2) && nm2.ValueKind == JsonValueKind.String
                    ? nm2.GetString() ?? idStr
                    : idStr);

            DateTimeOffset createdAt = DateTimeOffset.UtcNow;
            if (o.TryGetProperty("orderDate", out var od) && od.ValueKind == JsonValueKind.String
                && DateTimeOffset.TryParse(od.GetString(), out var parsed))
                createdAt = parsed;
            else if (o.TryGetProperty("created_at", out var ca) && ca.ValueKind == JsonValueKind.String
                && DateTimeOffset.TryParse(ca.GetString(), out var parsed2))
                createdAt = parsed2;

            // Saatlik granularite — date_min gun bazinda; ek filtre
            if (createdAt < filterSince) continue;

            string currency = o.TryGetProperty("currency", out var cur) && cur.ValueKind == JsonValueKind.String
                ? cur.GetString() ?? "TRY" : "TRY";

            decimal? totalPrice = null;
            if (o.TryGetProperty("totalAmount", out var tp) && tp.ValueKind == JsonValueKind.Number)
                totalPrice = tp.GetDecimal();
            else if (o.TryGetProperty("total", out var tp2))
            {
                if (tp2.ValueKind == JsonValueKind.Number) totalPrice = tp2.GetDecimal();
                else if (tp2.ValueKind == JsonValueKind.String
                    && decimal.TryParse(tp2.GetString(),
                        System.Globalization.NumberStyles.Any,
                        System.Globalization.CultureInfo.InvariantCulture, out var tv))
                    totalPrice = tv;
            }

            bool isCod = false;
            if (o.TryGetProperty("paymentType", out var pt) && pt.ValueKind == JsonValueKind.String)
            {
                var ptStr = (pt.GetString() ?? "").ToLowerInvariant();
                isCod = ptStr.Contains("kapida") || ptStr.Contains("cod") || ptStr.Contains("nakit");
            }
            decimal? collection = isCod ? totalPrice : null;

            var recipient = ReadAddress(o);
            var items = ReadItems(o);
            string? note = o.TryGetProperty("note", out var nt) && nt.ValueKind == JsonValueKind.String
                ? nt.GetString() : null;

            list.Add(new RawIncomingOrder(
                SourceChannel: OrderChannelType.IdeaSoft,
                ChannelExternalOrderId: $"ideasoft:{idStr}",
                OrderNumber: orderNumber,
                CreatedAtUtc: createdAt,
                CollectionAmount: collection,
                CurrencyCode: currency,
                Recipient: recipient,
                Items: items,
                Notes: string.IsNullOrWhiteSpace(note) ? $"IdeaSoft siparis ({tenantKey})" : note));
        }
        return list;
    }

    private static RawAddress ReadAddress(JsonElement order)
    {
        // IdeaSoft "shippingAddress" obje veya nested "customer.shippingAddress"
        JsonElement addr = default;
        bool hasAddr = false;
        if (order.TryGetProperty("shippingAddress", out var s1) && s1.ValueKind == JsonValueKind.Object)
        { addr = s1; hasAddr = true; }
        else if (order.TryGetProperty("shipping_address", out var s2) && s2.ValueKind == JsonValueKind.Object)
        { addr = s2; hasAddr = true; }

        if (!hasAddr) return new RawAddress("IdeaSoft Musterisi", null, null, null, null, null);

        string? Get(string p) => addr.TryGetProperty(p, out var v) && v.ValueKind == JsonValueKind.String
            ? v.GetString() : null;

        var name = Get("fullName") ?? Get("full_name");
        if (string.IsNullOrWhiteSpace(name))
        {
            var first = Get("firstname") ?? Get("first_name") ?? "";
            var last  = Get("lastname")  ?? Get("last_name")  ?? "";
            name = $"{first} {last}".Trim();
        }
        if (string.IsNullOrWhiteSpace(name)) name = "IdeaSoft Musterisi";

        string? phone = Get("phone") ?? Get("mobile") ?? Get("gsm");
        // Email order root'unda olabilir
        string? email = null;
        if (order.TryGetProperty("email", out var em) && em.ValueKind == JsonValueKind.String)
            email = em.GetString();
        else if (order.TryGetProperty("customerEmail", out var em2) && em2.ValueKind == JsonValueKind.String)
            email = em2.GetString();

        return new RawAddress(
            FullName: name,
            Phone: phone,
            Email: email,
            City: Get("city") ?? Get("province"),
            District: Get("district") ?? Get("county"),
            AddressLine: Get("address") ?? Get("addressLine") ?? Get("addressLine1"),
            PostalCode: Get("postalCode") ?? Get("zipCode"));
    }

    private static IReadOnlyList<RawOrderItem> ReadItems(JsonElement order)
    {
        JsonElement items = default;
        if (order.TryGetProperty("items", out var i1) && i1.ValueKind == JsonValueKind.Array)
            items = i1;
        else if (order.TryGetProperty("orderItems", out var i2) && i2.ValueKind == JsonValueKind.Array)
            items = i2;
        else return Array.Empty<RawOrderItem>();

        var result = new List<RawOrderItem>(items.GetArrayLength());
        foreach (var l in items.EnumerateArray())
        {
            string? GetStr(string p) => l.TryGetProperty(p, out var v) && v.ValueKind == JsonValueKind.String
                ? v.GetString() : null;

            int qty = l.TryGetProperty("quantity", out var q) && q.ValueKind == JsonValueKind.Number
                ? q.GetInt32() : 1;

            decimal? price = null;
            if (l.TryGetProperty("price", out var p) && p.ValueKind == JsonValueKind.Number)
                price = p.GetDecimal();
            else if (l.TryGetProperty("unitPrice", out var up) && up.ValueKind == JsonValueKind.Number)
                price = up.GetDecimal();

            result.Add(new RawOrderItem(
                ProductName: GetStr("productName") ?? GetStr("name") ?? "IdeaSoft urun",
                Quantity: qty,
                UnitPrice: price,
                Sku: GetStr("sku") ?? GetStr("productCode")));
        }
        return result;
    }

    private static string Truncate(string s, int max) =>
        s.Length <= max ? s : s.Substring(0, max) + "...";
}
