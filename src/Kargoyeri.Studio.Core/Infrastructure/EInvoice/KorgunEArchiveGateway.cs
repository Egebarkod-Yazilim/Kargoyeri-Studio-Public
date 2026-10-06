using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using Microsoft.Extensions.Configuration;

namespace Kargoyeri.Studio.Core.Infrastructure.EInvoice;

/// <summary>
/// P5-#3 — Korgun e-Arsiv (https://korgunmali.com / https://earsiv.korgun.com.tr) skeleton.
///
/// Gercek API'leri Korgun'un sundugu REST endpoint'i. Bu skeleton:
///  - Bearer token auth ile JSON POST yapar
///  - Hata response'lerini logla
///  - PDF download API'sini cagirir
///
/// Konfig:
/// <code>
/// "Studio:EArchive:Korgun": {
///   "BaseUrl": "https://api.korgun.com.tr",
///   "Username": "...",
///   "Password": "...",
///   "ApiKey": "...",
///   "SellerVkn": "1234567890",
///   "SellerTitle": "Kargoyeri Yazilim Tic. A.S."
/// }
/// </code>
/// </summary>
public sealed class KorgunEArchiveGateway : IEArchiveGateway
{
    private readonly HttpClient _http;
    private readonly IConfiguration _config;
    private readonly ILogger<KorgunEArchiveGateway> _logger;

    public string Name => "korgun";

    public KorgunEArchiveGateway(IHttpClientFactory httpFactory, IConfiguration config, ILogger<KorgunEArchiveGateway> logger)
    {
        _http = httpFactory.CreateClient("studio-korgun");
        _config = config;
        _logger = logger;
        _http.BaseAddress = new Uri((config["Studio:EArchive:Korgun:BaseUrl"] ?? "https://api.korgun.com.tr").TrimEnd('/') + "/");
    }

    public async Task<EArchiveSubmitResult> SubmitAsync(EArchiveDocument document, CancellationToken ct)
    {
        var apiKey = _config["Studio:EArchive:Korgun:ApiKey"];
        if (string.IsNullOrWhiteSpace(apiKey))
        {
            return new EArchiveSubmitResult(false, null, null, null, null, "Studio:EArchive:Korgun:ApiKey eksik.");
        }

        var payload = new
        {
            documentType = "EARSIV",
            documentNumber = document.DocumentNumber,
            issueDate = document.IssueDate.ToString("yyyy-MM-dd"),
            seller = new { vkn = document.SellerVkn, title = document.SellerTitle, address = document.SellerAddress },
            buyer = new { vkn = document.BuyerVkn, title = document.BuyerTitle, email = document.BuyerEmail, address = document.BuyerAddress },
            currency = document.Currency,
            lines = document.Lines.Select(l => new
            {
                description = l.Description,
                quantity = l.Quantity,
                unit = l.Unit,
                unitPrice = l.UnitPrice,
                lineTotal = l.LineTotal,
                vatRate = l.VatRate,
                vatAmount = l.VatAmount
            }),
            totals = new { net = document.TotalNet, vat = document.TotalVat, grand = document.Total },
            note = document.Note ?? string.Empty
        };

        try
        {
            using var msg = new HttpRequestMessage(HttpMethod.Post, "earsiv/submit");
            msg.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
            msg.Content = JsonContent.Create(payload);
            using var resp = await _http.SendAsync(msg, ct);
            var body = await resp.Content.ReadAsStringAsync(ct);
            if (!resp.IsSuccessStatusCode)
            {
                _logger.LogWarning("Korgun submit fail: {Code} {Body}", resp.StatusCode, body);
                return new EArchiveSubmitResult(false, null, null, null, null, body);
            }
            using var doc = JsonDocument.Parse(body);
            var root = doc.RootElement;
            return new EArchiveSubmitResult(
                Success: true,
                ProviderDocumentId: root.TryGetProperty("documentId", out var id) ? id.GetString() : null,
                Uuid: root.TryGetProperty("uuid", out var u) ? u.GetString() : null,
                PortalUrl: root.TryGetProperty("portalUrl", out var p) ? p.GetString() : null,
                PdfDownloadUrl: root.TryGetProperty("pdfUrl", out var pd) ? pd.GetString() : null,
                ErrorMessage: null);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Korgun submit exception");
            return new EArchiveSubmitResult(false, null, null, null, null, ex.Message);
        }
    }

    public async Task<Stream?> DownloadPdfAsync(string providerDocumentId, CancellationToken ct)
    {
        var apiKey = _config["Studio:EArchive:Korgun:ApiKey"];
        try
        {
            using var msg = new HttpRequestMessage(HttpMethod.Get, $"earsiv/{Uri.EscapeDataString(providerDocumentId)}/pdf");
            msg.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
            var resp = await _http.SendAsync(msg, HttpCompletionOption.ResponseHeadersRead, ct);
            if (!resp.IsSuccessStatusCode) return null;
            return await resp.Content.ReadAsStreamAsync(ct);
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "Korgun pdf download exception");
            return null;
        }
    }

    public async Task<EArchiveCancelResult> CancelAsync(string providerDocumentId, string reason, CancellationToken ct)
    {
        var apiKey = _config["Studio:EArchive:Korgun:ApiKey"];
        try
        {
            using var msg = new HttpRequestMessage(HttpMethod.Post, $"earsiv/{Uri.EscapeDataString(providerDocumentId)}/cancel");
            msg.Headers.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
            msg.Content = JsonContent.Create(new { reason });
            using var resp = await _http.SendAsync(msg, ct);
            if (resp.IsSuccessStatusCode)
                return new EArchiveCancelResult(true, null);
            return new EArchiveCancelResult(false, await resp.Content.ReadAsStringAsync(ct));
        }
        catch (Exception ex)
        {
            return new EArchiveCancelResult(false, ex.Message);
        }
    }
}
