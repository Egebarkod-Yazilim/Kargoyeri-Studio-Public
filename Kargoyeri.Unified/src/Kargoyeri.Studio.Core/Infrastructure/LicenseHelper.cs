namespace Kargoyeri.Studio.Core.Infrastructure;

/// <summary>
/// Müşteri lisans bilgisini Metadata dictionary'sinden okur/yazar.
/// Metadata key'leri: license.code, license.expiresAt, license.plan, license.createdAt
/// </summary>
public sealed record LicenseInfo(
    string? Code,
    DateTimeOffset? ExpiresAt,
    string? Plan,
    DateTimeOffset? CreatedAt)
{
    public static readonly LicenseInfo Empty = new(null, null, null, null);

    public bool HasLicense => !string.IsNullOrWhiteSpace(Code);
    public bool IsExpired  => ExpiresAt.HasValue && ExpiresAt.Value.UtcDateTime < DateTime.UtcNow;
    public bool IsValid    => HasLicense && !IsExpired;

    public int? DaysRemaining => ExpiresAt.HasValue
        ? Math.Max(0, (int)(ExpiresAt.Value - DateTimeOffset.UtcNow).TotalDays)
        : null;

    public string StatusLabel => !HasLicense ? "Lisans Yok"
        : IsExpired             ? "Suresi Doldu"
        : "Aktif";

    public string StatusCss => !HasLicense ? "status-pill--pending"
        : IsExpired            ? "status-pill--pending"
        : "status-pill--ready";
}

public static class LicenseHelper
{
    private const string KeyCode      = "license.code";
    private const string KeyExpiresAt = "license.expiresAt";
    private const string KeyPlan      = "license.plan";
    private const string KeyCreatedAt = "license.createdAt";

    public static LicenseInfo Read(Dictionary<string, string>? metadata)
    {
        if (metadata is null || metadata.Count == 0) return LicenseInfo.Empty;

        metadata.TryGetValue(KeyCode, out var code);
        metadata.TryGetValue(KeyPlan, out var plan);

        DateTimeOffset? expiresAt = null;
        if (metadata.TryGetValue(KeyExpiresAt, out var expiresStr)
            && DateTimeOffset.TryParse(expiresStr, out var exp))
            expiresAt = exp;

        DateTimeOffset? createdAt = null;
        if (metadata.TryGetValue(KeyCreatedAt, out var createdStr)
            && DateTimeOffset.TryParse(createdStr, out var cre))
            createdAt = cre;

        return new LicenseInfo(code, expiresAt, plan, createdAt);
    }

    public static Dictionary<string, string> Apply(
        Dictionary<string, string>? existing,
        string code,
        DateTimeOffset expiresAt,
        string plan)
    {
        var metadata = existing is null
            ? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            : new Dictionary<string, string>(existing, StringComparer.OrdinalIgnoreCase);

        metadata[KeyCode]      = code.Trim();
        metadata[KeyExpiresAt] = expiresAt.ToUniversalTime().ToString("O");
        metadata[KeyPlan]      = plan;
        metadata[KeyCreatedAt] = DateTimeOffset.UtcNow.ToString("O");

        return metadata;
    }

    /// <summary>
    /// Rastgele KY-XXXX-XXXX-XXXX formatında lisans kodu üretir.
    /// </summary>
    public static string GenerateCode() =>
        $"KY-{Seg()}-{Seg()}-{Seg()}";

    private static string Seg() =>
        Random.Shared.Next(0x1000, 0xFFFF).ToString("X4");
}
