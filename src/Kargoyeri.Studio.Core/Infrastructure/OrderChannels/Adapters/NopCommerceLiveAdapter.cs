using System.Collections.Concurrent;
using System.Globalization;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;

namespace Kargoyeri.Studio.Core.Infrastructure.OrderChannels.Adapters;

/// <summary>
/// NopCommerce Inroen Product API.
/// Swagger: POST /api/auth/login, GET /api/orders, GET /api/orders/{id}
/// Auth: Bearer accessToken veya X-Api-Key.
/// </summary>
public sealed class NopCommerceLiveAdapter : IOrderChannelAdapter
{
    private const int PageSize = 50;
    private const int MaxPages = 20;

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<NopCommerceLiveAdapter> _logger;
    private static readonly ConcurrentDictionary<string, (string token, DateTimeOffset expiresAt)> TokenCache = new();

    public OrderChannelType Type => OrderChannelType.NopCommerce;

    public NopCommerceLiveAdapter(IHttpClientFactory httpClientFactory, ILogger<NopCommerceLiveAdapter> logger)
    {
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    public async Task<OrderChannelTestResult> TestConnectionAsync(OrderChannelCredentials credentials, CancellationToken ct)
    {
        var creds = ReadCreds(credentials);
        if (creds.Error is not null)
            return new OrderChannelTestResult(false, creds.Error);

        try
        {
            var auth = await AuthorizeAsync(credentials.TenantKey, creds, ct);
            if (auth.Error is not null)
                return new OrderChannelTestResult(false, auth.Error);

            var url = $"{creds.SiteUrl}/api/orders?pageIndex=0&pageSize=1";
            using var client = BuildClient(auth);
            using var resp = await client.GetAsync(url, ct);
            var body = await resp.Content.ReadAsStringAsync(ct);
            if (!resp.IsSuccessStatusCode)
                return new OrderChannelTestResult(false, $"Nop API hata kodu: {(int)resp.StatusCode}", Truncate(body, 400));

            var total = ReadTotal(body);
            return new OrderChannelTestResult(true, total is null
                ? "NopCommerce API bilgileri dogrulandi."
                : $"NopCommerce API dogrulandi. Kayitli siparis: {total}.");
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "NopCommerce test connection hatasi (tenant={Tenant})", credentials.TenantKey);
            return new OrderChannelTestResult(false, "NopCommerce API'ye ulasilamadi: " + ex.Message);
        }
    }

    public async Task<OrderChannelFetchResult> FetchOrdersAsync(OrderChannelCredentials credentials, DateTimeOffset since, CancellationToken ct)
    {
        var creds = ReadCreds(credentials);
        if (creds.Error is not null)
            return new OrderChannelFetchResult(false, creds.Error, Array.Empty<RawIncomingOrder>());

        var startUtc = since.ToUniversalTime();
        try
        {
            var auth = await AuthorizeAsync(credentials.TenantKey, creds, ct);
            if (auth.Error is not null)
                return new OrderChannelFetchResult(false, auth.Error, Array.Empty<RawIncomingOrder>());

            var collected = new List<RawIncomingOrder>();
            var scanned = 0;
            int? totalPages = null;
            for (var page = 0; page < MaxPages; page++)
            {
                if (totalPages is not null && page >= totalPages)
                    break;

                var url = $"{creds.SiteUrl}/api/orders?pageIndex={page}&pageSize={PageSize}";
                if (creds.OrderStatusId is not null)
                    url += "&orderStatusId=" + creds.OrderStatusId.Value.ToString(CultureInfo.InvariantCulture);

                using var client = BuildClient(auth);
                using var resp = await client.GetAsync(url, ct);
                var body = await resp.Content.ReadAsStringAsync(ct);
                if (!resp.IsSuccessStatusCode)
                {
                    return new OrderChannelFetchResult(false,
                        $"NopCommerce API: {(int)resp.StatusCode} — {Truncate(body, 200)}",
                        Array.Empty<RawIncomingOrder>());
                }

                var pageOrders = ParseOrders(body, credentials.TenantKey, out var pages);
                totalPages ??= pages;
                scanned += pageOrders.Count;
                if (pageOrders.Count == 0)
                    break;

                var newest = pageOrders.Max(o => o.CreatedAtUtc);
                collected.AddRange(pageOrders.Where(o => o.CreatedAtUtc >= startUtc));
                if (newest < startUtc)
                    break;
            }

            var matched = collected
                .GroupBy(o => o.ChannelExternalOrderId, StringComparer.OrdinalIgnoreCase)
                .Select(g => g.First())
                .ToArray();
            return new OrderChannelFetchResult(true,
                $"NopCommerce: {matched.Length} siparis (taranan {scanned}, since {startUtc:yyyy-MM-dd HH:mm}).",
                matched);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "NopCommerce fetch hatasi (tenant={Tenant})", credentials.TenantKey);
            return new OrderChannelFetchResult(false, "NopCommerce siparis cekiminde beklenmedik hata: " + ex.Message, Array.Empty<RawIncomingOrder>());
        }
    }

    private async Task<(string? Token, string? ApiKey, string? Error)> AuthorizeAsync(string tenantKey, NopCreds creds, CancellationToken ct)
    {
        if (!string.IsNullOrWhiteSpace(creds.Email) && !string.IsNullOrWhiteSpace(creds.Password))
        {
            var token = await EnsureTokenAsync(tenantKey, creds.SiteUrl, creds.Email, creds.Password, ct);
            if (token is null)
                return (null, null, "NopCommerce giris basarisiz. E-posta ve sifreyi kontrol edin (POST /api/auth/login).");
            return (token, creds.ApiKey, null);
        }

        if (!string.IsNullOrWhiteSpace(creds.ApiKey))
            return (null, creds.ApiKey, null);

        return (null, null, "NopCommerce icin e-posta+sifre veya API anahtari gerekir.");
    }

    private async Task<string?> EnsureTokenAsync(string tenantKey, string siteUrl, string email, string password, CancellationToken ct)
    {
        var cacheKey = $"{tenantKey}|{siteUrl}|{email}";
        if (TokenCache.TryGetValue(cacheKey, out var entry) && entry.expiresAt > DateTimeOffset.UtcNow.AddSeconds(30))
            return entry.token;

        using var client = _httpClientFactory.CreateClient("nopcommerce-auth");
        client.Timeout = TimeSpan.FromSeconds(20);
        using var req = new HttpRequestMessage(HttpMethod.Post, $"{siteUrl}/api/auth/login");
        req.Content = new StringContent(
            JsonSerializer.Serialize(new { email, password }),
            Encoding.UTF8,
            "application/json");

        using var resp = await client.SendAsync(req, ct);
        var body = await resp.Content.ReadAsStringAsync(ct);
        if (!resp.IsSuccessStatusCode)
        {
            _logger.LogWarning("NopCommerce login hata: {Code} {Body}", (int)resp.StatusCode, Truncate(body, 200));
            return null;
        }

        using var doc = JsonDocument.Parse(body);
        var token = ReadString(doc.RootElement, "accessToken");
        var expiresIn = ReadInt(doc.RootElement, "expiresIn") ?? 3600;
        if (string.IsNullOrWhiteSpace(token))
            return null;

        TokenCache[cacheKey] = (token, DateTimeOffset.UtcNow.AddSeconds(Math.Max(60, expiresIn)));
        return token;
    }

    private HttpClient BuildClient((string? Token, string? ApiKey, string? Error) auth)
    {
        var client = _httpClientFactory.CreateClient("nopcommerce-orders");
        client.DefaultRequestHeaders.Accept.Clear();
        client.DefaultRequestHeaders.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
        client.Timeout = TimeSpan.FromSeconds(30);
        if (!string.IsNullOrWhiteSpace(auth.Token))
            client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", auth.Token);
        if (!string.IsNullOrWhiteSpace(auth.ApiKey))
            client.DefaultRequestHeaders.TryAddWithoutValidation("X-Api-Key", auth.ApiKey);
        return client;
    }

    private static NopCreds ReadCreds(OrderChannelCredentials c)
    {
        var siteUrl = (c.Get("siteUrl") ?? "https://test.egebarkod.com").Trim().TrimEnd('/');
        if (!siteUrl.StartsWith("http", StringComparison.OrdinalIgnoreCase))
            siteUrl = "https://" + siteUrl;

        var email = First(c.Get("username"), c.Get("email"));
        var password = c.Get("password")?.Trim();
        var apiKey = First(c.Get("apiKey"), c.Get("api_key"));
        int? orderStatusId = int.TryParse(c.Get("orderStatusId"), out var statusId) ? statusId : null;

        if (string.IsNullOrWhiteSpace(email) && string.IsNullOrWhiteSpace(apiKey))
            return new NopCreds(null!, null, null, null, null, "Eksik alanlar: E-posta veya API anahtari");
        if (string.IsNullOrWhiteSpace(password) && string.IsNullOrWhiteSpace(apiKey))
            return new NopCreds(null!, null, null, null, null, "Eksik alanlar: Sifre");

        return new NopCreds(siteUrl, email, password, apiKey, orderStatusId, null);
    }

    private static int? ReadTotal(string json)
    {
        try
        {
            using var doc = JsonDocument.Parse(json);
            return ReadInt(doc.RootElement, "totalCount");
        }
        catch
        {
            return null;
        }
    }

    private static IReadOnlyList<RawIncomingOrder> ParseOrders(string json, string tenantKey, out int? totalPages)
    {
        totalPages = null;
        using var doc = JsonDocument.Parse(json);
        var root = doc.RootElement;
        totalPages = ReadInt(root, "totalPages");
        if (!TryGet(root, "orders", out var arr) || arr.ValueKind != JsonValueKind.Array)
            return Array.Empty<RawIncomingOrder>();

        var list = new List<RawIncomingOrder>(arr.GetArrayLength());
        foreach (var order in arr.EnumerateArray())
        {
            var id = ReadInt(order, "id");
            if (id is null)
                continue;

            var orderNumber = ReadString(order, "customOrderNumber") ?? id.Value.ToString(CultureInfo.InvariantCulture);
            var createdAt = ReadDate(order, "createdOnUtc") ?? DateTimeOffset.UtcNow;
            var currency = ReadString(order, "customerCurrencyCode") ?? "TRY";
            var total = ReadDecimal(order, "orderTotal");
            var payment = ReadString(order, "paymentMethodSystemName") ?? string.Empty;
            var isCod = payment.Contains("cashondelivery", StringComparison.OrdinalIgnoreCase)
                || payment.Contains("kapida", StringComparison.OrdinalIgnoreCase)
                || payment.Contains(".cod", StringComparison.OrdinalIgnoreCase);
            var recipient = ReadAddress(order);
            var items = ReadItems(order);
            var note = ReadNotes(order);
            var status = ReadString(order, "orderStatus");
            var shipping = ReadString(order, "shippingMethod");
            var summary = string.Join(" | ", new[] { status, shipping, note }.Where(s => !string.IsNullOrWhiteSpace(s)));

            list.Add(new RawIncomingOrder(
                SourceChannel: OrderChannelType.NopCommerce,
                ChannelExternalOrderId: "nopcommerce:" + id.Value.ToString(CultureInfo.InvariantCulture),
                OrderNumber: orderNumber,
                CreatedAtUtc: createdAt,
                CollectionAmount: isCod ? total : null,
                CurrencyCode: currency,
                Recipient: recipient,
                Items: items,
                Notes: string.IsNullOrWhiteSpace(summary) ? $"NopCommerce siparis ({tenantKey})" : summary,
                RawMetadata: new Dictionary<string, string>
                {
                    ["nop.orderStatusId"] = ReadInt(order, "orderStatusId")?.ToString(CultureInfo.InvariantCulture) ?? "",
                    ["nop.paymentStatus"] = ReadString(order, "paymentStatus") ?? "",
                    ["nop.shippingStatus"] = ReadString(order, "shippingStatus") ?? "",
                    ["nop.shippingMethod"] = shipping ?? ""
                }));
        }

        return list;
    }

    private static RawAddress ReadAddress(JsonElement order)
    {
        JsonElement addr = default;
        var hasShipping = TryGet(order, "shippingAddress", out addr) && addr.ValueKind == JsonValueKind.Object;
        if (!hasShipping && (!TryGet(order, "billingAddress", out addr) || addr.ValueKind != JsonValueKind.Object))
        {
            return new RawAddress(
                ReadString(order, "customerFullName") ?? "NopCommerce Musterisi",
                ReadString(order, "customerPhone"),
                ReadString(order, "customerEmail"),
                null, null, null);
        }

        var name = $"{ReadString(addr, "firstName")} {ReadString(addr, "lastName")}".Trim();
        if (string.IsNullOrWhiteSpace(name))
            name = ReadString(order, "customerFullName") ?? "NopCommerce Musterisi";

        var line = ReadString(addr, "address1");
        var line2 = ReadString(addr, "address2");
        if (!string.IsNullOrWhiteSpace(line2))
            line = $"{line} {line2}".Trim();

        return new RawAddress(
            name,
            ReadString(addr, "phoneNumber") ?? ReadString(order, "customerPhone"),
            ReadString(addr, "email") ?? ReadString(order, "customerEmail"),
            ReadString(addr, "city"),
            ReadString(addr, "county") ?? ReadString(addr, "stateProvinceName"),
            line,
            ReadString(addr, "zipPostalCode"));
    }

    private static IReadOnlyList<RawOrderItem> ReadItems(JsonElement order)
    {
        if (!TryGet(order, "items", out var items) || items.ValueKind != JsonValueKind.Array)
            return Array.Empty<RawOrderItem>();

        var result = new List<RawOrderItem>(items.GetArrayLength());
        foreach (var line in items.EnumerateArray())
        {
            result.Add(new RawOrderItem(
                ReadString(line, "productName") ?? "NopCommerce urun",
                ReadInt(line, "quantity") ?? 1,
                ReadDecimal(line, "unitPriceInclTax") ?? ReadDecimal(line, "unitPriceExclTax"),
                ReadString(line, "sku") ?? ReadString(line, "stockCode")));
        }
        return result;
    }

    private static string? ReadNotes(JsonElement order)
    {
        if (!TryGet(order, "notes", out var notes) || notes.ValueKind != JsonValueKind.Array)
            return ReadString(order, "checkoutAttributeDescription");

        var text = string.Join("; ", notes.EnumerateArray()
            .Select(n => ReadString(n, "note"))
            .Where(s => !string.IsNullOrWhiteSpace(s)));
        var checkout = ReadString(order, "checkoutAttributeDescription");
        if (string.IsNullOrWhiteSpace(text))
            return checkout;
        return string.IsNullOrWhiteSpace(checkout) ? text : text + " | " + checkout;
    }

    private static bool TryGet(JsonElement element, string camelName, out JsonElement value)
    {
        if (element.ValueKind == JsonValueKind.Object && element.TryGetProperty(camelName, out value))
            return true;
        var pascal = char.ToUpperInvariant(camelName[0]) + camelName[1..];
        if (element.ValueKind == JsonValueKind.Object && element.TryGetProperty(pascal, out value))
            return true;
        value = default;
        return false;
    }

    private static string? ReadString(JsonElement element, string name)
    {
        if (!TryGet(element, name, out var value))
            return null;
        return value.ValueKind == JsonValueKind.String ? value.GetString() : value.ToString();
    }

    private static int? ReadInt(JsonElement element, string name)
    {
        if (!TryGet(element, name, out var value))
            return null;
        if (value.ValueKind == JsonValueKind.Number && value.TryGetInt32(out var number))
            return number;
        return int.TryParse(value.ToString(), out var parsed) ? parsed : null;
    }

    private static decimal? ReadDecimal(JsonElement element, string name)
    {
        if (!TryGet(element, name, out var value))
            return null;
        if (value.ValueKind == JsonValueKind.Number && value.TryGetDecimal(out var number))
            return number;
        return decimal.TryParse(value.ToString(), NumberStyles.Number, CultureInfo.InvariantCulture, out var parsed) ? parsed : null;
    }

    private static DateTimeOffset? ReadDate(JsonElement element, string name)
    {
        var text = ReadString(element, name);
        return DateTimeOffset.TryParse(text, CultureInfo.InvariantCulture, DateTimeStyles.AssumeUniversal, out var parsed)
            ? parsed.ToUniversalTime()
            : null;
    }

    private static string? First(params string?[] values) =>
        values.Select(v => v?.Trim()).FirstOrDefault(v => !string.IsNullOrWhiteSpace(v));

    private static string Truncate(string s, int max) =>
        s.Length <= max ? s : s[..max] + "...";

    private sealed record NopCreds(string SiteUrl, string? Email, string? Password, string? ApiKey, int? OrderStatusId, string? Error);
}
