namespace Kargoyeri.Studio.Core.Infrastructure.EInvoice;

/// <summary>
/// P5-#3 — e-Arsiv / e-Fatura saglayicisi soyutlamasi.
/// Implementasyonlar: GibPortalEArchive, KorgunEArchive, UyumsoftEArchive, NoopEArchive (dev).
///
/// Yasal not: TR'de aylik 5M+ ciro veya sektorel kapsamda kayitli mukellef e-Fatura/e-Arsiv kullanmali.
/// Aksi halde matbu fatura yeterli (ancak Studio musterileri B2B agirlikli — hepsi e-Fatura'ya tabi olabilir).
/// </summary>
public interface IEArchiveGateway
{
    /// <summary>Saglayici adi (orn: "gib", "korgun", "uyumsoft", "noop").</summary>
    string Name { get; }

    /// <summary>Fatura olusturup saglayiciya gonderir; UUID + PDF/HTML link doner.</summary>
    Task<EArchiveSubmitResult> SubmitAsync(EArchiveDocument document, CancellationToken ct);

    /// <summary>Olusturulmus fatura PDF/HTML belgesini saglayicidan ceker.</summary>
    Task<Stream?> DownloadPdfAsync(string providerDocumentId, CancellationToken ct);

    /// <summary>Olusturulmus faturayi iptal eder (sure icindeyse — TR'de 8 gun).</summary>
    Task<EArchiveCancelResult> CancelAsync(string providerDocumentId, string reason, CancellationToken ct);
}

public sealed class EArchiveDocument
{
    public string DocumentNumber { get; set; } = string.Empty; // KY-202604-XXXXXXXX
    public DateOnly IssueDate { get; set; } = DateOnly.FromDateTime(DateTime.Today);
    public string SellerVkn { get; set; } = string.Empty;
    public string SellerTitle { get; set; } = string.Empty;
    public string SellerAddress { get; set; } = string.Empty;
    public string BuyerVkn { get; set; } = string.Empty; // VKN/TCKN (10/11 hane)
    public string BuyerTitle { get; set; } = string.Empty;
    public string BuyerEmail { get; set; } = string.Empty;
    public string BuyerAddress { get; set; } = string.Empty;
    public string Currency { get; set; } = "TRY";
    public List<EArchiveLine> Lines { get; set; } = new();
    public decimal TotalNet { get; set; }
    public decimal TotalVat { get; set; }
    public decimal Total { get; set; }
    public string? Note { get; set; }
}

public sealed class EArchiveLine
{
    public string Description { get; set; } = string.Empty;
    public decimal Quantity { get; set; }
    public string Unit { get; set; } = "ADET";
    public decimal UnitPrice { get; set; }
    public decimal LineTotal { get; set; }
    public decimal VatRate { get; set; }
    public decimal VatAmount { get; set; }
}

public sealed record EArchiveSubmitResult(
    bool Success,
    string? ProviderDocumentId,
    string? Uuid,
    string? PortalUrl,
    string? PdfDownloadUrl,
    string? ErrorMessage);

public sealed record EArchiveCancelResult(bool Success, string? ErrorMessage);
