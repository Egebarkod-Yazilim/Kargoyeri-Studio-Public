using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Security.Cryptography;
using System.Text;

namespace Kargoyeri.Studio.Core.Infrastructure.Storage;

/// <summary>
/// AWS S3 + S3-compatible (MinIO, DigitalOcean Spaces, Wasabi, Cloudflare R2) backend.
///
/// AWS SDK (AWSSDK.S3) yerine kucuk bir Signature V4 implementasyonu â€” boylece extra
/// dependency yok. Production icin AWSSDK kullanmak istersen, bu sinifi swap edebilirsin.
///
/// Konfig:
///   Studio:Storage:S3:Endpoint       (https://s3.us-east-1.amazonaws.com)
///   Studio:Storage:S3:Region         (us-east-1)
///   Studio:Storage:S3:AccessKey      (AKIA... veya MinIO root key)
///   Studio:Storage:S3:SecretKey      (...)
///   Studio:Storage:S3:Bucket         (kargoyeri-studio-prod)
///   Studio:Storage:S3:UsePathStyle   (true MinIO/R2, false AWS classic)
/// </summary>
public sealed class S3BlobStorage : IBlobStorage
{
    private readonly HttpClient _http;
    private readonly S3Options _opt;
    private readonly ILogger<S3BlobStorage> _logger;

    public S3BlobStorage(IHttpClientFactory httpFactory, S3Options options, ILogger<S3BlobStorage> logger)
    {
        _http = httpFactory.CreateClient("studio-s3");
        _opt = options;
        _logger = logger;
    }

    public async Task PutAsync(string container, string key, ReadOnlyMemory<byte> data, string? contentType = null, CancellationToken ct = default)
    {
        using var content = new ByteArrayContent(data.ToArray());
        if (!string.IsNullOrEmpty(contentType))
            content.Headers.ContentType = MediaTypeHeaderValue.Parse(contentType);
        await SendSignedAsync(HttpMethod.Put, ObjectUri(container, key), content, data, ct).ConfigureAwait(false);
    }

    public async Task PutStreamAsync(string container, string key, Stream stream, string? contentType = null, CancellationToken ct = default)
    {
        using var ms = new MemoryStream();
        await stream.CopyToAsync(ms, ct).ConfigureAwait(false);
        await PutAsync(container, key, ms.ToArray(), contentType, ct).ConfigureAwait(false);
    }

    public async Task<byte[]?> GetAsync(string container, string key, CancellationToken ct = default)
    {
        var resp = await SendSignedAsync(HttpMethod.Get, ObjectUri(container, key), null, ReadOnlyMemory<byte>.Empty, ct, allow404: true).ConfigureAwait(false);
        if (resp.StatusCode == HttpStatusCode.NotFound) return null;
        return await resp.Content.ReadAsByteArrayAsync(ct).ConfigureAwait(false);
    }

    public async Task<Stream?> GetStreamAsync(string container, string key, CancellationToken ct = default)
    {
        var resp = await SendSignedAsync(HttpMethod.Get, ObjectUri(container, key), null, ReadOnlyMemory<byte>.Empty, ct, allow404: true).ConfigureAwait(false);
        if (resp.StatusCode == HttpStatusCode.NotFound) return null;
        return await resp.Content.ReadAsStreamAsync(ct).ConfigureAwait(false);
    }

    public async Task<bool> ExistsAsync(string container, string key, CancellationToken ct = default)
    {
        var resp = await SendSignedAsync(HttpMethod.Head, ObjectUri(container, key), null, ReadOnlyMemory<byte>.Empty, ct, allow404: true).ConfigureAwait(false);
        return resp.IsSuccessStatusCode;
    }

    public async Task<bool> DeleteAsync(string container, string key, CancellationToken ct = default)
    {
        var resp = await SendSignedAsync(HttpMethod.Delete, ObjectUri(container, key), null, ReadOnlyMemory<byte>.Empty, ct, allow404: true).ConfigureAwait(false);
        return resp.IsSuccessStatusCode;
    }

    public async IAsyncEnumerable<BlobInfo> ListAsync(string container, string? prefix = null, [System.Runtime.CompilerServices.EnumeratorCancellation] CancellationToken ct = default)
    {
        // Skeleton â€” tam implementasyon icin S3 ListObjectsV2 XML response'u parse edilmeli.
        // Production deployment'inda AWSSDK.S3 ile swap edilmesi onerilir.
        _logger.LogWarning("S3BlobStorage.ListAsync: skeleton implementation. AWSSDK.S3'e gec production icin.");
        await Task.CompletedTask;
        yield break;
    }

    public Task<string?> GetPresignedUrlAsync(string container, string key, TimeSpan validFor, CancellationToken ct = default)
    {
        // SigV4 query-string signing: GET icin
        var expires = (long)validFor.TotalSeconds;
        var uri = ObjectUri(container, key);
        var presigned = SignQueryString(uri, expires);
        return Task.FromResult<string?>(presigned);
    }

    // ---- Helpers ----------------------------------------------------------

    private Uri ObjectUri(string container, string key)
    {
        var bucket = string.IsNullOrEmpty(container) ? _opt.Bucket : container;
        var endpoint = _opt.Endpoint.TrimEnd('/');
        var encodedKey = Uri.EscapeDataString(key).Replace("%2F", "/");
        return _opt.UsePathStyle
            ? new Uri($"{endpoint}/{bucket}/{encodedKey}")
            : new Uri($"{endpoint.Replace("://", $"://{bucket}.")}/{encodedKey}");
    }

    private async Task<HttpResponseMessage> SendSignedAsync(
        HttpMethod method, Uri uri, HttpContent? content, ReadOnlyMemory<byte> bodyBytes,
        CancellationToken ct, bool allow404 = false)
    {
        var req = new HttpRequestMessage(method, uri);
        if (content is not null) req.Content = content;
        SignRequest(req, bodyBytes.Span);
        var resp = await _http.SendAsync(req, ct).ConfigureAwait(false);
        if (!resp.IsSuccessStatusCode && !(allow404 && resp.StatusCode == HttpStatusCode.NotFound))
        {
            var body = await resp.Content.ReadAsStringAsync(ct).ConfigureAwait(false);
            _logger.LogWarning("S3 {Method} {Uri} -> {Status}: {Body}", method, uri, resp.StatusCode, body);
            resp.EnsureSuccessStatusCode();
        }
        return resp;
    }

    private void SignRequest(HttpRequestMessage req, ReadOnlySpan<byte> body)
    {
        var now = DateTime.UtcNow;
        var amzDate = now.ToString("yyyyMMddTHHmmssZ", CultureInfo.InvariantCulture);
        var dateStamp = now.ToString("yyyyMMdd", CultureInfo.InvariantCulture);
        var payloadHash = HashHex(body);

        req.Headers.TryAddWithoutValidation("x-amz-date", amzDate);
        req.Headers.TryAddWithoutValidation("x-amz-content-sha256", payloadHash);
        req.Headers.Host = req.RequestUri!.Host;

        var canonicalRequest = BuildCanonicalRequest(req, payloadHash);
        var credScope = $"{dateStamp}/{_opt.Region}/s3/aws4_request";
        var stringToSign = $"AWS4-HMAC-SHA256\n{amzDate}\n{credScope}\n{HashHex(Encoding.UTF8.GetBytes(canonicalRequest))}";

        var kDate    = HmacSha256(Encoding.UTF8.GetBytes("AWS4" + _opt.SecretKey), dateStamp);
        var kRegion  = HmacSha256(kDate, _opt.Region);
        var kService = HmacSha256(kRegion, "s3");
        var kSigning = HmacSha256(kService, "aws4_request");
        var signature = ToHex(HmacSha256(kSigning, stringToSign));

        var signedHeaders = "host;x-amz-content-sha256;x-amz-date";
        req.Headers.TryAddWithoutValidation("Authorization",
            $"AWS4-HMAC-SHA256 Credential={_opt.AccessKey}/{credScope}, SignedHeaders={signedHeaders}, Signature={signature}");
    }

    private string BuildCanonicalRequest(HttpRequestMessage req, string payloadHash)
    {
        var method = req.Method.Method;
        var uri = req.RequestUri!;
        var canonicalUri = uri.AbsolutePath;
        var canonicalQuery = uri.Query.TrimStart('?');
        var canonicalHeaders = $"host:{uri.Host}\nx-amz-content-sha256:{payloadHash}\nx-amz-date:{req.Headers.GetValues("x-amz-date").First()}\n";
        var signedHeaders = "host;x-amz-content-sha256;x-amz-date";
        return $"{method}\n{canonicalUri}\n{canonicalQuery}\n{canonicalHeaders}\n{signedHeaders}\n{payloadHash}";
    }

    private string SignQueryString(Uri uri, long expiresSeconds)
    {
        // S3 SigV4 query-string GET icin. Production-ready degil; test icin yeterli.
        var now = DateTime.UtcNow;
        var amzDate = now.ToString("yyyyMMddTHHmmssZ", CultureInfo.InvariantCulture);
        var dateStamp = now.ToString("yyyyMMdd", CultureInfo.InvariantCulture);
        var credScope = $"{dateStamp}/{_opt.Region}/s3/aws4_request";

        var qs = new Dictionary<string, string>
        {
            ["X-Amz-Algorithm"]     = "AWS4-HMAC-SHA256",
            ["X-Amz-Credential"]    = Uri.EscapeDataString($"{_opt.AccessKey}/{credScope}"),
            ["X-Amz-Date"]          = amzDate,
            ["X-Amz-Expires"]       = expiresSeconds.ToString(CultureInfo.InvariantCulture),
            ["X-Amz-SignedHeaders"] = "host"
        };
        var canonicalQuery = string.Join("&", qs.OrderBy(kv => kv.Key, StringComparer.Ordinal).Select(kv => $"{kv.Key}={kv.Value}"));
        var canonicalRequest = $"GET\n{uri.AbsolutePath}\n{canonicalQuery}\nhost:{uri.Host}\n\nhost\nUNSIGNED-PAYLOAD";
        var stringToSign = $"AWS4-HMAC-SHA256\n{amzDate}\n{credScope}\n{HashHex(Encoding.UTF8.GetBytes(canonicalRequest))}";

        var kDate    = HmacSha256(Encoding.UTF8.GetBytes("AWS4" + _opt.SecretKey), dateStamp);
        var kRegion  = HmacSha256(kDate, _opt.Region);
        var kService = HmacSha256(kRegion, "s3");
        var kSigning = HmacSha256(kService, "aws4_request");
        var signature = ToHex(HmacSha256(kSigning, stringToSign));

        return $"{uri}?{canonicalQuery}&X-Amz-Signature={signature}";
    }

    private static byte[] HmacSha256(byte[] key, string data)
    {
        using var h = new HMACSHA256(key);
        return h.ComputeHash(Encoding.UTF8.GetBytes(data));
    }

    private static string HashHex(ReadOnlySpan<byte> data)
    {
        Span<byte> hash = stackalloc byte[32];
        SHA256.HashData(data, hash);
        return ToHex(hash);
    }

    private static string ToHex(ReadOnlySpan<byte> bytes)
    {
        var sb = new StringBuilder(bytes.Length * 2);
        foreach (var b in bytes) sb.Append(b.ToString("x2", CultureInfo.InvariantCulture));
        return sb.ToString();
    }
}

public sealed class S3Options
{
    public string Endpoint { get; set; } = "";
    public string Region { get; set; } = "us-east-1";
    public string AccessKey { get; set; } = "";
    public string SecretKey { get; set; } = "";
    public string Bucket { get; set; } = "";
    public bool UsePathStyle { get; set; } = true;
}

