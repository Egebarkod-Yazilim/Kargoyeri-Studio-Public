using Microsoft.Extensions.Configuration;

namespace Kargoyeri.Studio.Core.Infrastructure.EInvoice;

/// <summary>
/// P5-#3 — GIB e-Arsiv portal entegrasyonu (skeleton).
///
/// Not: GIB'in resmi e-Arsiv portali otomasyon API'si DESTEKLEMEZ. Mukellef sayisi az
/// kuruluslar icin GIB Internet Vergi Dairesi'nde manuel duzenleme yapilir.
/// Aylik fatura sayisi 5'i asanlar **ozel entegrator** (Korgun/Uyumsoft/Logo/Mikro/Foriba) kullanmalidir.
///
/// Bu sinif GIB Portal entegrasyonunun **mantiken eksik** oldugunu isaretlemek icin var.
/// SubmitAsync hep "manuel issue" sonucu doner.
/// </summary>
public sealed class GibPortalEArchiveGateway : IEArchiveGateway
{
    private readonly IConfiguration _config;
    private readonly ILogger<GibPortalEArchiveGateway> _logger;

    public string Name => "gib";

    public GibPortalEArchiveGateway(IConfiguration config, ILogger<GibPortalEArchiveGateway> logger)
    {
        _config = config;
        _logger = logger;
    }

    public Task<EArchiveSubmitResult> SubmitAsync(EArchiveDocument document, CancellationToken ct)
    {
        _logger.LogInformation("GIB Portal e-Arsiv: {DocNo} icin manuel duzenleme gerekiyor.", document.DocumentNumber);
        return Task.FromResult(new EArchiveSubmitResult(
            Success: false,
            ProviderDocumentId: null,
            Uuid: null,
            PortalUrl: "https://earsivportal.efatura.gov.tr",
            PdfDownloadUrl: null,
            ErrorMessage: "GIB Portal otomasyon API'si yok. Fatura GIB portalinda manuel duzenlenmeli; bu cikti yalnizca veri-on-bilgi olarak kullanilmalidir."));
    }

    public Task<Stream?> DownloadPdfAsync(string providerDocumentId, CancellationToken ct) =>
        Task.FromResult<Stream?>(null);

    public Task<EArchiveCancelResult> CancelAsync(string providerDocumentId, string reason, CancellationToken ct) =>
        Task.FromResult(new EArchiveCancelResult(false, "GIB portal cancel manuel yapilir."));
}
