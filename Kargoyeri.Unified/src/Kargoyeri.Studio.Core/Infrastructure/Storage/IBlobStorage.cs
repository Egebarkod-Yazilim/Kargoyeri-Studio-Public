namespace Kargoyeri.Studio.Core.Infrastructure.Storage;

/// <summary>
/// P5-#8 — Plug-in storage backend abstraction.
///
/// Mevcut sistemde tenant metrics, label PDFs, audit exports, signup pending vb.
/// hep ContentRoot altinda dosya olarak yaziyor. Bu yaklasim multi-region/multi-instance
/// senaryolarda problem (her instance'in local FS'i farkli).
///
/// IBlobStorage soyutlamasi:
///   - LocalFileBlobStorage  : default, ContentRoot/blobs/ altinda yazar (dev + tek-instance prod)
///   - S3BlobStorage         : AWS S3 / DigitalOcean Spaces / MinIO uyumlu (production multi-region)
///   - AzureBlobStorage      : Azure Blob Storage (Azure deployment'i icin)
///
/// Backend secimi `Studio:Storage:Provider` config keyi ile yapilir:
///   - "Local"  -> LocalFileBlobStorage (default)
///   - "S3"     -> S3BlobStorage (Studio:Storage:S3:* gerekli)
///   - "Azure"  -> AzureBlobStorage (Studio:Storage:Azure:ConnectionString gerekli)
/// </summary>
public interface IBlobStorage
{
    /// <summary>
    /// Bir blob'u yazar. Mevcut blob varsa uzerine yazar.
    /// </summary>
    Task PutAsync(string container, string key, ReadOnlyMemory<byte> data, string? contentType = null, CancellationToken ct = default);

    /// <summary>Stream tabanli yazma — buyuk dosyalar icin.</summary>
    Task PutStreamAsync(string container, string key, Stream stream, string? contentType = null, CancellationToken ct = default);

    /// <summary>Blob'u byte array olarak okur. Yoksa null doner.</summary>
    Task<byte[]?> GetAsync(string container, string key, CancellationToken ct = default);

    /// <summary>Blob'u stream olarak okur. Yoksa null doner. Caller dispose eder.</summary>
    Task<Stream?> GetStreamAsync(string container, string key, CancellationToken ct = default);

    /// <summary>Blob var mi?</summary>
    Task<bool> ExistsAsync(string container, string key, CancellationToken ct = default);

    /// <summary>Blob'u siler. Yoksa false doner.</summary>
    Task<bool> DeleteAsync(string container, string key, CancellationToken ct = default);

    /// <summary>Container'daki anahtarlari listeler (prefix ile filtrelenebilir).</summary>
    IAsyncEnumerable<BlobInfo> ListAsync(string container, string? prefix = null, CancellationToken ct = default);

    /// <summary>Pre-signed URL uretir (sadece public erisim destekleyen backend'lerde S3/Azure).</summary>
    Task<string?> GetPresignedUrlAsync(string container, string key, TimeSpan validFor, CancellationToken ct = default);
}

public sealed record BlobInfo(string Key, long SizeBytes, DateTimeOffset LastModifiedUtc);
