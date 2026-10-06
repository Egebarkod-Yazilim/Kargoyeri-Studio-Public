using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Microsoft.Extensions.Configuration;

namespace Kargoyeri.Studio.Core.Infrastructure.EInvoice;

/// <summary>
/// P5-#3 — Uyumsoft e-Arsiv (https://www.uyumsoft.com.tr) skeleton.
///
/// Uyumsoft genelde SOAP servis kullanir; bu skeleton REST gateway varsayar.
/// Production: Uyumsoft'un sunculari icin kendi WSDL stub'unu generate edip,
/// burayi `BasicHttpsBinding` ile sarmalamak gerekebilir.
///
/// Konfig:
/// <code>
/// "Studio:EArchive:Uyumsoft": {
///   "BaseUrl": "https://efatura.uyumsoft.com.tr/api",
///   "Username": "...",
///   "Password": "...",
///   "SellerVkn": "1234567890"
/// }
/// </code>
/// </summary>
public sealed class UyumsoftEArchiveGateway : IEArchiveGateway
{
    private readonly HttpClient _http;
    private readonly IConfiguration _config;
    private readonly ILogger<UyumsoftEArchiveGateway> _logger;

    public string Name => "uyumsoft";

    public UyumsoftEArchiveGateway(IHttpClientFactory httpFactory, IConfiguration config, ILogger<UyumsoftEArchiveGateway> logger)
    {
        _http = httpFactory.CreateClient("studio-uyumsoft");
        _config = config;
        _logger = logger;
        _http.BaseAddress = new Uri((config["Studio:EArchive:Uyumsoft:BaseUrl"] ?? "https://efatura.uyumsoft.com.tr/api").TrimEnd('/') + "/");
    }

    public async Task<EArchiveSubmitResult> SubmitAsync(EArchiveDocument document, CancellationToken ct)
    {
        var user = _config["Studio:EArchive:Uyumsoft:Username"];
        var pass = _config["Studio:EArchive:Uyumsoft:Password"];
        if (string.IsNullOrWhiteSpace(user) || string.IsNullOrWhiteSpace(pass))
            return new EArchiveSubmitResult(false, null, null, null, null, "Uyumsoft credentials eksik.");

        var payload = new
        {
            invoice = new
            {
                number = document.DocumentNumber,
                date = document.IssueDate.ToString("yyyy-MM-dd"),
                supplier = new { vkn = document.SellerVkn, name = document.SellerTitle, address = document.SellerAddress },
                customer = new { vkn = document.BuyerVkn, name = document.BuyerTitle, email = document.BuyerEmail, address = document.BuyerAddress },
                currency = document.Currency,
                items = document.Lines.Select(l => new
                {
                    name = l.Description,
                    quantity = l.Quantity,
                    unit = l.Unit,
                    unitPrice = l.UnitPrice,
                    total = l.LineTotal,
                    vatRate = l.VatRate,
                    vatAmount = l.VatAmount
                }),
                totals = new { taxableAmount = document.TotalNet, taxAmount = document.TotalVat, payableAmount = document.Total },
                note = document.Note
            }
        };

        try
        {
            using var msg = new HttpRequestMessage(HttpMethod.Post, "earsiv/create");
            var basic = Convert.ToBase64String(System.Text.Encoding.UTF8.GetBytes($"{user}:{pass}"));
            msg.Headers.Authorization = new AuthenticationHeaderValue("Basic", basic);
            msg.Content = JsonContent.Create(payload);
            using var resp = await _http.SendAsync(msg, ct);
            var body = await resp.Content.ReadAsStringAsync(ct);
            if (!resp.IsSuccessStatusCode)
            {
                _logger.LogWarning("Uyumsoft submit fail: {Code} {Body}", resp.StatusCode, body);
                return new EArchiveSubmitResult(false, null, null, null, null, body);
            }
            using var doc = JsonDocument.Parse(body);
            var root = doc.RootElement;
            return new EArchiveSubmitResult(
                Success: true,
                ProviderDocumentId: root.TryGetProperty("invoiceId", out var i) ? i.GetString() : null,
                Uuid: root.TryGetProperty("uuid", out var u) ? u.GetString() : null,
                PortalUrl: root.TryGetProperty("htmlUrl", out var h) ? h.GetString() : null,
                PdfDownloadUrl: root.TryGetProperty("pdfUrl", out var p) ? p.GetString() : null,
                ErrorMessage: null);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Uyumsoft submit exception");
            return new EArchiveSubmitResult(false, null, null, null, null, ex.Message);
        }
    }

    public Task<Stream?> DownloadPdfAsync(string providerDocumentId, CancellationToken ct) =>
        Task.FromResult<Stream?>(null); // Uyumsoft icin pdf download akisi entegrasyon-spesifik

    public Task<EArchiveCancelResult> CancelAsync(string providerDocumentId, string reason, CancellationToken ct) =>
        Task.FromResult(new EArchiveCancelResult(false, "Uyumsoft cancel API entegrasyonu yapilmadi."));
}
