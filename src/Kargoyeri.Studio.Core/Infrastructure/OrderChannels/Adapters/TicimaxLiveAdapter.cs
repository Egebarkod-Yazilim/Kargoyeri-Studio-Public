using System.Net.Http.Headers;
using System.Text.Json;

namespace Kargoyeri.Studio.Core.Infrastructure.OrderChannels.Adapters;

/// <summary>
/// Ticimax REST API (gercek/canli).
/// Docs:     https://ticimax.com.tr/destek-merkezi (API Documentation)
/// Endpoint: GET {siteUrl}/Servis/SiparisServis.svc/JSON/SiparisListesi
///           POST body: { ApiKey, SiparisFiltre: { BaslangicTarihi, BitisTarihi, ... } }
/// Auth:     API Key (panel'den olusturulur) — body parametresi olarak gonderilir
/// Format:   Ticimax JSON RPC tarzi — POST + JSON envelope
/// Rate:     Genelde host-bagimli (kendi sunucusunda calistigi icin)
/// </summary>
public sealed class TicimaxLiveAdapter : IOrderChannelAdapter
{
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<TicimaxLiveAdapter> _logger;

    public OrderChannelType Type => OrderChannelType.Ticimax;

    public TicimaxLiveAdapter(IHttpClientFactory httpClientFactory, ILogger<TicimaxLiveAdapter> logger)
    {
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    public async Task<OrderChannelTestResult> TestConnectionAsync(
        OrderChannelCredentials credentials, CancellationToken ct)
    {
        var (siteUrl, apiKey, missing) = ReadCreds(credentials);
        if (missing is not null) return new OrderChannelTestResult(false, missing);

        try
        {
            // Hafif test: son 1 gunluk listele, 1 sayfa
            var since = DateTimeOffset.UtcNow.AddDays(-1);
            var (success, body) = await PostFetchAsync(siteUrl!, apiKey!, since, page: 1, size: 1, ct);
            if (success)
                return new OrderChannelTestResult(true, "Ticimax API bilgileri dogrulandi.");
            return new OrderChannelTestResult(false,
                "Ticimax API erisilemedi — API Key ve URL kontrol edin.",
                Truncate(body, 400));
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Ticimax test connection hatasi (tenant={Tenant})", credentials.TenantKey);
            return new OrderChannelTestResult(false, "Ticimax API'ye ulasilamadi: " + ex.Message);
        }
    }

    public async Task<OrderChannelFetchResult> FetchOrdersAsync(
        OrderChannelCredentials credentials, DateTimeOffset since, CancellationToken ct)
    {
        var (siteUrl, apiKey, missing) = ReadCreds(credentials);
        if (missing is not null) return new OrderChannelFetchResult(false, missing, Array.Empty<RawIncomingOrder>());

        var startUtc = since.ToUniversalTime();
        try
        {
            var (success, body) = await PostFetchAsync(siteUrl!, apiKey!, startUtc, page: 1, size: 100, ct);
            if (!success)
            {
                return new OrderChannelFetchResult(false,
                    $"Ticimax API hata: {Truncate(body, 200)}",
                    Array.Empty<RawIncomingOrder>());
            }

            var orders = ParseOrders(body, credentials.TenantKey, startUtc);
            return new OrderChannelFetchResult(true,
                $"Ticimax: {orders.Count} siparis cekildi (since {startUtc:yyyy-MM-dd HH:mm}).",
                orders);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Ticimax fetch hatasi (tenant={Tenant})", credentials.TenantKey);
            return new OrderChannelFetchResult(false,
                "Ticimax siparis cekiminde beklenmedik hata: " + ex.Message,
                Array.Empty<RawIncomingOrder>());
        }
    }

    private async Task<(bool success, string body)> PostFetchAsync(
        string siteUrl, string apiKey, DateTimeOffset since, int page, int size, CancellationToken ct)
    {
        var url = $"{siteUrl}/Servis/SiparisServis.svc/JSON/SiparisListesi";
        // Ticimax filtresi: tarih TR formati ("dd.MM.yyyy HH:mm")
        var payload = new
        {
            UyeKodu = apiKey,
            SiparisFiltre = new
            {
                BaslangicTarihi = since.ToString("dd.MM.yyyy HH:mm"),
                BitisTarihi = DateTimeOffset.UtcNow.ToString("dd.MM.yyyy HH:mm"),
                BaslangicIndex = (page - 1) * size,
                YuklenecekKayitSayisi = size,
                SiparisDurumu = -1,    // -1 = tumu
                OdemeDurumu = -1,
                SiralamaTipi = 0       // 0 = tarihe gore desc
            }
        };

        using var client = _httpClientFactory.CreateClient("ticimax-orders");
        client.Timeout = TimeSpan.FromSeconds(30);

        using var req = new HttpRequestMessage(HttpMethod.Post, url);
        req.Content = new StringContent(
            JsonSerializer.Serialize(payload),
            System.Text.Encoding.UTF8,
            "application/json");

        using var resp = await client.SendAsync(req, ct);
        var body = await resp.Content.ReadAsStringAsync(ct);
        return (resp.IsSuccessStatusCode, body);
    }

    private static (string? siteUrl, string? apiKey, string? missing) ReadCreds(OrderChannelCredentials c)
    {
        var siteUrl = c.Get("siteUrl")?.Trim().TrimEnd('/');
        var apiKey = c.Get("apiKey")?.Trim();

        if (!string.IsNullOrWhiteSpace(siteUrl) && !siteUrl!.StartsWith("http", StringComparison.OrdinalIgnoreCase))
            siteUrl = "https://" + siteUrl;

        var missing = new List<string>();
        if (string.IsNullOrWhiteSpace(siteUrl)) missing.Add("Site URL");
        if (string.IsNullOrWhiteSpace(apiKey)) missing.Add("API Key (UyeKodu)");
        return missing.Count > 0
            ? (null, null, $"Eksik alanlar: {string.Join(", ", missing)}")
            : (siteUrl, apiKey, null);
    }

    private static IReadOnlyList<RawIncomingOrder> ParseOrders(string json, string tenantKey, DateTimeOffset filterSince)
    {
        using var doc = JsonDocument.Parse(json);
        // Ticimax tipik response: {"d":{"SiparisListesi":[...]}} veya direkt {"SiparisListesi":[...]}
        JsonElement arr;
        if (doc.RootElement.TryGetProperty("d", out var d)
            && d.ValueKind == JsonValueKind.Object
            && d.TryGetProperty("SiparisListesi", out var list1)
            && list1.ValueKind == JsonValueKind.Array)
        {
            arr = list1;
        }
        else if (doc.RootElement.TryGetProperty("SiparisListesi", out var list2)
            && list2.ValueKind == JsonValueKind.Array)
        {
            arr = list2;
        }
        else if (doc.RootElement.ValueKind == JsonValueKind.Array)
        {
            arr = doc.RootElement;
        }
        else
        {
            return Array.Empty<RawIncomingOrder>();
        }

        var list = new List<RawIncomingOrder>(arr.GetArrayLength());
        foreach (var o in arr.EnumerateArray())
        {
            // Ticimax ID = "ID" (int) veya "SiparisID"
            string idStr = "";
            if (o.TryGetProperty("ID", out var id1) && id1.ValueKind == JsonValueKind.Number)
                idStr = id1.GetInt64().ToString();
            else if (o.TryGetProperty("SiparisID", out var id2) && id2.ValueKind == JsonValueKind.Number)
                idStr = id2.GetInt64().ToString();
            if (string.IsNullOrEmpty(idStr)) continue;

            var orderNumber = o.TryGetProperty("SiparisKodu", out var nm) && nm.ValueKind == JsonValueKind.String
                ? nm.GetString() ?? idStr : idStr;

            DateTimeOffset createdAt = DateTimeOffset.UtcNow;
            // Ticimax tarih: "/Date(1234567890123)/" veya "dd.MM.yyyy HH:mm:ss"
            if (o.TryGetProperty("Tarih", out var dt) && dt.ValueKind == JsonValueKind.String)
                createdAt = ParseTicimaxDate(dt.GetString() ?? "") ?? createdAt;
            else if (o.TryGetProperty("SiparisTarihi", out var dt2) && dt2.ValueKind == JsonValueKind.String)
                createdAt = ParseTicimaxDate(dt2.GetString() ?? "") ?? createdAt;

            // In-memory ek filtre (Ticimax dakika granulariteyi destekler ama emniyet icin)
            if (createdAt < filterSince) continue;

            decimal? totalPrice = null;
            if (o.TryGetProperty("ToplamTutar", out var tp) && tp.ValueKind == JsonValueKind.Number)
                totalPrice = tp.GetDecimal();
            else if (o.TryGetProperty("GenelToplam", out var tp2) && tp2.ValueKind == JsonValueKind.Number)
                totalPrice = tp2.GetDecimal();

            // COD detection: OdemeTipi "Kapida" iceriyorsa
            bool isCod = false;
            if (o.TryGetProperty("OdemeTipi", out var pt) && pt.ValueKind == JsonValueKind.String)
                isCod = (pt.GetString() ?? "").Contains("Kapida", StringComparison.OrdinalIgnoreCase);
            decimal? collection = isCod ? totalPrice : null;

            var recipient = ReadAddress(o);
            var items = ReadItems(o);
            string? note = o.TryGetProperty("Aciklama", out var nt) && nt.ValueKind == JsonValueKind.String
                ? nt.GetString() : null;

            list.Add(new RawIncomingOrder(
                SourceChannel: OrderChannelType.Ticimax,
                ChannelExternalOrderId: $"ticimax:{idStr}",
                OrderNumber: orderNumber,
                CreatedAtUtc: createdAt,
                CollectionAmount: collection,
                CurrencyCode: "TRY",
                Recipient: recipient,
                Items: items,
                Notes: string.IsNullOrWhiteSpace(note) ? $"Ticimax siparis ({tenantKey})" : note));
        }
        return list;
    }

    private static DateTimeOffset? ParseTicimaxDate(string raw)
    {
        if (string.IsNullOrWhiteSpace(raw)) return null;

        // /Date(milliseconds)/ format
        if (raw.StartsWith("/Date(", StringComparison.OrdinalIgnoreCase) && raw.EndsWith(")/"))
        {
            var inner = raw.Substring(6, raw.Length - 8);
            // Timezone offset olabilir: "/Date(1234567890123+0300)/"
            var plusIdx = inner.IndexOf('+');
            var minusIdx = inner.IndexOf('-', 1);
            if (plusIdx > 0) inner = inner.Substring(0, plusIdx);
            else if (minusIdx > 0) inner = inner.Substring(0, minusIdx);
            if (long.TryParse(inner, out var ms))
                return DateTimeOffset.FromUnixTimeMilliseconds(ms);
        }

        // ISO veya TR format
        if (DateTimeOffset.TryParseExact(raw, "dd.MM.yyyy HH:mm:ss",
            System.Globalization.CultureInfo.InvariantCulture,
            System.Globalization.DateTimeStyles.AssumeLocal, out var dt1)) return dt1.ToUniversalTime();
        if (DateTimeOffset.TryParseExact(raw, "dd.MM.yyyy HH:mm",
            System.Globalization.CultureInfo.InvariantCulture,
            System.Globalization.DateTimeStyles.AssumeLocal, out var dt2)) return dt2.ToUniversalTime();
        if (DateTimeOffset.TryParse(raw, out var dt3)) return dt3.ToUniversalTime();

        return null;
    }

    private static RawAddress ReadAddress(JsonElement order)
    {
        // Ticimax: TeslimatBilgisi nested obje
        JsonElement addr = default;
        bool hasAddr = false;
        if (order.TryGetProperty("TeslimatBilgisi", out var t1) && t1.ValueKind == JsonValueKind.Object)
        { addr = t1; hasAddr = true; }
        else if (order.TryGetProperty("Teslimat", out var t2) && t2.ValueKind == JsonValueKind.Object)
        { addr = t2; hasAddr = true; }

        if (!hasAddr) return new RawAddress("Ticimax Musterisi", null, null, null, null, null);

        string? Get(string p) => addr.TryGetProperty(p, out var v) && v.ValueKind == JsonValueKind.String
            ? v.GetString() : null;

        var name = Get("AdSoyad") ?? Get("AliciAdi");
        if (string.IsNullOrWhiteSpace(name))
        {
            var first = Get("Ad") ?? "";
            var last  = Get("Soyad") ?? "";
            name = $"{first} {last}".Trim();
        }
        if (string.IsNullOrWhiteSpace(name)) name = "Ticimax Musterisi";

        return new RawAddress(
            FullName: name,
            Phone: Get("Telefon") ?? Get("CepTelefonu") ?? Get("GSM"),
            Email: Get("Email") ?? Get("EPosta"),
            City: Get("Il") ?? Get("Sehir"),
            District: Get("Ilce"),
            AddressLine: Get("Adres") ?? Get("AcikAdres"),
            PostalCode: Get("PostaKodu"));
    }

    private static IReadOnlyList<RawOrderItem> ReadItems(JsonElement order)
    {
        JsonElement items = default;
        if (order.TryGetProperty("Urunler", out var i1) && i1.ValueKind == JsonValueKind.Array)
            items = i1;
        else if (order.TryGetProperty("SiparisUrunleri", out var i2) && i2.ValueKind == JsonValueKind.Array)
            items = i2;
        else return Array.Empty<RawOrderItem>();

        var result = new List<RawOrderItem>(items.GetArrayLength());
        foreach (var l in items.EnumerateArray())
        {
            string? GetStr(string p) => l.TryGetProperty(p, out var v) && v.ValueKind == JsonValueKind.String
                ? v.GetString() : null;

            int qty = l.TryGetProperty("Adet", out var q) && q.ValueKind == JsonValueKind.Number
                ? q.GetInt32() : 1;

            decimal? price = null;
            if (l.TryGetProperty("Fiyat", out var p) && p.ValueKind == JsonValueKind.Number)
                price = p.GetDecimal();
            else if (l.TryGetProperty("BirimFiyat", out var p2) && p2.ValueKind == JsonValueKind.Number)
                price = p2.GetDecimal();

            result.Add(new RawOrderItem(
                ProductName: GetStr("UrunAdi") ?? GetStr("Ad") ?? "Ticimax urun",
                Quantity: qty,
                UnitPrice: price,
                Sku: GetStr("StokKodu") ?? GetStr("UrunKodu")));
        }
        return result;
    }

    private static string Truncate(string s, int max) =>
        s.Length <= max ? s : s.Substring(0, max) + "...";
}
