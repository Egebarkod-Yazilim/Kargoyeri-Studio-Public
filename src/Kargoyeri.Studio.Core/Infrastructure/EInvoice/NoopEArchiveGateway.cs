using System.Text.Json;

namespace Kargoyeri.Studio.Core.Infrastructure.EInvoice;

/// <summary>
/// P5-#3 — Dev/test ortami: gercek saglayici cagrilmaz, dosyaya log atar.
/// </summary>
public sealed class NoopEArchiveGateway : IEArchiveGateway
{
    public string Name => "noop";
    private readonly string _logDir;

    public NoopEArchiveGateway(IWebHostEnvironment env)
    {
        _logDir = Path.Combine(env.ContentRootPath, "earchive-noop");
        System.IO.Directory.CreateDirectory(_logDir);
    }

    public async Task<EArchiveSubmitResult> SubmitAsync(EArchiveDocument document, CancellationToken ct)
    {
        var providerId = $"NOOP-{DateTime.UtcNow:yyyyMMddHHmmss}-{Guid.NewGuid():N}";
        var uuid = Guid.NewGuid().ToString();
        var file = Path.Combine(_logDir, $"{providerId}.json");
        await File.WriteAllTextAsync(file, JsonSerializer.Serialize(document, new JsonSerializerOptions { WriteIndented = true }), ct);
        return new EArchiveSubmitResult(
            Success: true,
            ProviderDocumentId: providerId,
            Uuid: uuid,
            PortalUrl: $"file://{file}",
            PdfDownloadUrl: null,
            ErrorMessage: null);
    }

    public Task<Stream?> DownloadPdfAsync(string providerDocumentId, CancellationToken ct) =>
        Task.FromResult<Stream?>(null);

    public Task<EArchiveCancelResult> CancelAsync(string providerDocumentId, string reason, CancellationToken ct) =>
        Task.FromResult(new EArchiveCancelResult(true, null));
}
