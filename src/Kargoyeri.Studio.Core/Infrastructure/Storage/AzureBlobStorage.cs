namespace Kargoyeri.Studio.Core.Infrastructure.Storage;

/// <summary>
/// Azure Blob Storage backend SKELETON.
/// Production deployment'inda Azure.Storage.Blobs NuGet paketi eklenip swap edilmeli.
///
/// Bu skeleton local FS'e dusuyor — config'de "Azure" secildiginde uyari logluyor.
/// </summary>
public sealed class AzureBlobStorage : IBlobStorage
{
    private readonly LocalFileBlobStorage _fallback;
    private readonly ILogger<AzureBlobStorage> _logger;

    public AzureBlobStorage(LocalFileBlobStorage fallback, ILogger<AzureBlobStorage> logger)
    {
        _fallback = fallback;
        _logger = logger;
        _logger.LogWarning("AzureBlobStorage skeleton. Production icin Azure.Storage.Blobs NuGet paketini ekleyip bu sinifi gercek implementasyon ile degistirin.");
    }

    public Task PutAsync(string container, string key, ReadOnlyMemory<byte> data, string? contentType = null, CancellationToken ct = default)
        => _fallback.PutAsync(container, key, data, contentType, ct);

    public Task PutStreamAsync(string container, string key, Stream stream, string? contentType = null, CancellationToken ct = default)
        => _fallback.PutStreamAsync(container, key, stream, contentType, ct);

    public Task<byte[]?> GetAsync(string container, string key, CancellationToken ct = default)
        => _fallback.GetAsync(container, key, ct);

    public Task<Stream?> GetStreamAsync(string container, string key, CancellationToken ct = default)
        => _fallback.GetStreamAsync(container, key, ct);

    public Task<bool> ExistsAsync(string container, string key, CancellationToken ct = default)
        => _fallback.ExistsAsync(container, key, ct);

    public Task<bool> DeleteAsync(string container, string key, CancellationToken ct = default)
        => _fallback.DeleteAsync(container, key, ct);

    public IAsyncEnumerable<BlobInfo> ListAsync(string container, string? prefix = null, CancellationToken ct = default)
        => _fallback.ListAsync(container, prefix, ct);

    public Task<string?> GetPresignedUrlAsync(string container, string key, TimeSpan validFor, CancellationToken ct = default)
        => _fallback.GetPresignedUrlAsync(container, key, validFor, ct);
}
