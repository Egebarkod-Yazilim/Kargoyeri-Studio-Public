namespace Kargoyeri.Studio.Core.Infrastructure.Storage;

/// <summary>
/// Local filesystem backend. ContentRoot/blobs/{container}/{key} altinda yazar.
/// Dev + tek-instance prod icin uygundur.
/// </summary>
public sealed class LocalFileBlobStorage : IBlobStorage
{
    private readonly string _root;

    public LocalFileBlobStorage(IWebHostEnvironment env)
    {
        _root = Path.Combine(env.ContentRootPath, "blobs");
        System.IO.Directory.CreateDirectory(_root);
    }

    private string Resolve(string container, string key)
    {
        var safeContainer = Sanitize(container);
        var safeKey = key.Replace('\\', '/').TrimStart('/');
        var full = Path.Combine(_root, safeContainer, safeKey);
        var dir = Path.GetDirectoryName(full);
        if (!string.IsNullOrEmpty(dir)) System.IO.Directory.CreateDirectory(dir);
        return full;
    }

    private static string Sanitize(string s)
    {
        return string.Concat(s.Where(c => char.IsLetterOrDigit(c) || c == '-' || c == '_' || c == '.'));
    }

    public async Task PutAsync(string container, string key, ReadOnlyMemory<byte> data, string? contentType = null, CancellationToken ct = default)
    {
        var path = Resolve(container, key);
        await File.WriteAllBytesAsync(path, data.ToArray(), ct).ConfigureAwait(false);
    }

    public async Task PutStreamAsync(string container, string key, Stream stream, string? contentType = null, CancellationToken ct = default)
    {
        var path = Resolve(container, key);
        await using var fs = File.Create(path);
        await stream.CopyToAsync(fs, ct).ConfigureAwait(false);
    }

    public async Task<byte[]?> GetAsync(string container, string key, CancellationToken ct = default)
    {
        var path = Resolve(container, key);
        if (!File.Exists(path)) return null;
        return await File.ReadAllBytesAsync(path, ct).ConfigureAwait(false);
    }

    public Task<Stream?> GetStreamAsync(string container, string key, CancellationToken ct = default)
    {
        var path = Resolve(container, key);
        if (!File.Exists(path)) return Task.FromResult<Stream?>(null);
        return Task.FromResult<Stream?>(File.OpenRead(path));
    }

    public Task<bool> ExistsAsync(string container, string key, CancellationToken ct = default)
        => Task.FromResult(File.Exists(Resolve(container, key)));

    public Task<bool> DeleteAsync(string container, string key, CancellationToken ct = default)
    {
        var path = Resolve(container, key);
        if (!File.Exists(path)) return Task.FromResult(false);
        File.Delete(path);
        return Task.FromResult(true);
    }

    public async IAsyncEnumerable<BlobInfo> ListAsync(string container, string? prefix = null, [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct = default)
    {
        var containerDir = Path.Combine(_root, Sanitize(container));
        if (!System.IO.Directory.Exists(containerDir)) yield break;
        var pattern = string.IsNullOrEmpty(prefix) ? "*" : prefix + "*";
        foreach (var f in System.IO.Directory.EnumerateFiles(containerDir, "*", SearchOption.AllDirectories))
        {
            ct.ThrowIfCancellationRequested();
            var rel = Path.GetRelativePath(containerDir, f).Replace('\\', '/');
            if (!string.IsNullOrEmpty(prefix) && !rel.StartsWith(prefix, StringComparison.Ordinal)) continue;
            var fi = new FileInfo(f);
            yield return new BlobInfo(rel, fi.Length, fi.LastWriteTimeUtc);
            await Task.Yield();
        }
    }

    public Task<string?> GetPresignedUrlAsync(string container, string key, TimeSpan validFor, CancellationToken ct = default)
    {
        // Local FS pre-signed URL desteklemez.
        return Task.FromResult<string?>(null);
    }
}
