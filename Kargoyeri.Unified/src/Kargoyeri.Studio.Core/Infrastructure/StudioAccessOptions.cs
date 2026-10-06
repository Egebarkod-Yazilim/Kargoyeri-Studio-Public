namespace Kargoyeri.Studio.Core.Infrastructure;

public sealed class StudioAccessOptions
{
    public string? AdminAccessCode { get; set; }

    /// <summary>
    /// Eski tek-operator kodu (geriye dönük uyumluluk — LockedWorkspaceCode ile birlikte çalışır).
    /// Yeni müşteriler için CustomerCodes kullanın.
    /// </summary>
    public string? OperatorAccessCode { get; set; }
    public string? LockedWorkspaceCode { get; set; }
    public string? LockedWorkspaceName { get; set; }

    /// <summary>
    /// Her müşteriye özel erişim kodu.
    /// Key = erişim kodu, Value = TenantKey (müşteri kodu).
    /// Müşteri bu kodla giriş yapınca otomatik olarak kendi workspace'ine kilitlenir.
    /// </summary>
    public Dictionary<string, string> CustomerCodes { get; set; } = new(StringComparer.Ordinal);

    /// <summary>
    /// Sistem yoneticileri (e-posta + parola ile giris).
    /// Production'da PasswordHash (BCrypt) kullanin; Password alani sadece dev/bootstrap icindir.
    /// </summary>
    public List<StudioAdminUser> Admins { get; set; } = new();
}

public sealed class StudioAdminUser
{
    public string Email { get; set; } = string.Empty;

    /// <summary>Plaintext parola (yalnizca bootstrap/dev icin onerilir).</summary>
    public string Password { get; set; } = string.Empty;

    /// <summary>BCrypt hash (production icin tercih edilir).</summary>
    public string PasswordHash { get; set; } = string.Empty;
    public string TotpSecret { get; set; } = string.Empty;
    public bool TotpEnabled { get; set; }

    public string DisplayName { get; set; } = string.Empty;
    public bool   IsActive    { get; set; } = true;
}
