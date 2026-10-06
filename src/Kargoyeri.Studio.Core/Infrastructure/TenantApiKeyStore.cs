using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Kargoyeri.Studio.Core.Infrastructure;

/// <summary>
/// Tenant bazli API anahtarlarini tenant Metadata["api.keys"] JSON dizisinde saklar (P1-#4).
///
/// Format:
///   tenant.Metadata["api.keys"] = JSON Array of:
///     { "id": guid, "name": "Test Anahtari", "hash": "sha256-hex", "prefix": "kgy_xxx",
///       "createdAt": iso, "lastUsedAt": iso?, "isRevoked": bool }
///
/// Plain anahtar yalnizca olusturuldugu anda kullaniciya gosterilir; sonra sadece hash saklanir.
/// </summary>
public static class TenantApiKeyStore
{
    public const string MetadataKey = "api.keys";

    /// <summary>Plain anahtarin gorunen prefix uzunlugu (UI'da listelerken).</summary>
    private const int PreviewPrefixLen = 8;

    public sealed record ApiKeyRecord(
        string Id,
        string Name,
        string Hash,
        string Prefix,
        DateTimeOffset CreatedAt,
        DateTimeOffset? LastUsedAt,
        bool IsRevoked);

    public static IReadOnlyList<ApiKeyRecord> Read(IReadOnlyDictionary<string, string> metadata)
    {
        if (!metadata.TryGetValue(MetadataKey, out var json) || string.IsNullOrWhiteSpace(json))
            return Array.Empty<ApiKeyRecord>();

        try
        {
            var list = JsonSerializer.Deserialize<List<ApiKeyRecord>>(json);
            return list ?? new List<ApiKeyRecord>();
        }
        catch (JsonException)
        {
            return Array.Empty<ApiKeyRecord>();
        }
    }

    private static string Serialize(IEnumerable<ApiKeyRecord> records)
        => JsonSerializer.Serialize(records.ToList());

    /// <summary>Yeni anahtar uretir — donen GenerateResult plain anahtari icerir (yalniz bu donus iliskiyi gorur).</summary>
    public sealed record GenerateResult(Dictionary<string, string> Metadata, ApiKeyRecord Record, string PlainKey);

    public static GenerateResult Generate(IReadOnlyDictionary<string, string> metadata, string name)
    {
        var random = RandomNumberGenerator.GetBytes(32);
        var plain  = "kgy_" + Convert.ToBase64String(random)
            .Replace("+", "")
            .Replace("/", "")
            .Replace("=", "")
            .Substring(0, 36);

        var hash   = HashKey(plain);
        var prefix = plain.Substring(0, Math.Min(PreviewPrefixLen, plain.Length));

        var record = new ApiKeyRecord(
            Id:         Guid.NewGuid().ToString("N"),
            Name:       string.IsNullOrWhiteSpace(name) ? "Adsiz" : name.Trim(),
            Hash:       hash,
            Prefix:     prefix,
            CreatedAt:  DateTimeOffset.UtcNow,
            LastUsedAt: null,
            IsRevoked:  false);

        var existing = Read(metadata).ToList();
        existing.Add(record);

        var copy = new Dictionary<string, string>(metadata, StringComparer.OrdinalIgnoreCase)
        {
            [MetadataKey] = Serialize(existing)
        };

        return new GenerateResult(copy, record, plain);
    }

    public static Dictionary<string, string> Revoke(IReadOnlyDictionary<string, string> metadata, string keyId)
    {
        var list = Read(metadata).Select(r =>
            string.Equals(r.Id, keyId, StringComparison.OrdinalIgnoreCase)
                ? r with { IsRevoked = true }
                : r).ToList();

        var copy = new Dictionary<string, string>(metadata, StringComparer.OrdinalIgnoreCase)
        {
            [MetadataKey] = Serialize(list)
        };
        return copy;
    }

    public static Dictionary<string, string> Delete(IReadOnlyDictionary<string, string> metadata, string keyId)
    {
        var list = Read(metadata)
            .Where(r => !string.Equals(r.Id, keyId, StringComparison.OrdinalIgnoreCase))
            .ToList();

        var copy = new Dictionary<string, string>(metadata, StringComparer.OrdinalIgnoreCase)
        {
            [MetadataKey] = Serialize(list)
        };
        return copy;
    }

    /// <summary>Plain anahtarin hash'inin verilen tenant icindeki revoke edilmemis bir kayit ile eslesip eslesmedigini kontrol eder.</summary>
    public static ApiKeyRecord? TryMatch(IReadOnlyDictionary<string, string> metadata, string plainKey)
    {
        if (string.IsNullOrWhiteSpace(plainKey)) return null;
        var hash = HashKey(plainKey);
        return Read(metadata).FirstOrDefault(r =>
            !r.IsRevoked &&
            CryptographicOperations.FixedTimeEquals(
                Encoding.ASCII.GetBytes(r.Hash),
                Encoding.ASCII.GetBytes(hash)));
    }

    public static string HashKey(string plain)
    {
        using var sha = SHA256.Create();
        var bytes = sha.ComputeHash(Encoding.UTF8.GetBytes(plain));
        return Convert.ToHexString(bytes);
    }

    /// <summary>"LastUsedAt" alanini guncelle — middleware her dogrulamada cagirir.</summary>
    public static Dictionary<string, string> TouchLastUsed(IReadOnlyDictionary<string, string> metadata, string keyId)
    {
        var list = Read(metadata).Select(r =>
            string.Equals(r.Id, keyId, StringComparison.OrdinalIgnoreCase)
                ? r with { LastUsedAt = DateTimeOffset.UtcNow }
                : r).ToList();

        var copy = new Dictionary<string, string>(metadata, StringComparer.OrdinalIgnoreCase)
        {
            [MetadataKey] = Serialize(list)
        };
        return copy;
    }
}
