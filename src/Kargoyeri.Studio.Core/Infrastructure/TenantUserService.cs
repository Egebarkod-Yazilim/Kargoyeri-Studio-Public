using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Kargoyeri.Application.Services;
using Kargoyeri.Contracts.Dtos;
using BCryptNet = BCrypt.Net.BCrypt;

namespace Kargoyeri.Studio.Core.Infrastructure;

/// <summary>
/// Müşteri başına kullanıcıları CustomerTenant.Metadata["users"] anahtarında
/// JSON olarak saklar. Böylece mevcut storage (JSON/SQL) ile persist olur.
/// </summary>
public sealed class TenantUserService
{
    private const string MetaKey = "users";

    private readonly CustomerService _customerService;

    public TenantUserService(CustomerService customerService)
    {
        _customerService = customerService;
    }

    // ── Okuma ──────────────────────────────────────────────────────────────────

    public List<TenantUser> GetUsers(string tenantKey)
    {
        // Sync wrapper — sadece liste gösterimi için kullanılır
        var profile = _customerService.GetProfileAsync(tenantKey, CancellationToken.None).GetAwaiter().GetResult();
        return Deserialize(profile?.Metadata);
    }

    public async Task<List<TenantUser>> GetUsersAsync(string tenantKey, CancellationToken ct)
    {
        var profile = await _customerService.GetProfileAsync(tenantKey, ct);
        return Deserialize(profile?.Metadata);
    }

    // ── Doğrulama ──────────────────────────────────────────────────────────────

    /// <summary>
    /// Kullaniciyi dogrular. Eski SHA256 hash ile eslesirse otomatik olarak BCrypt'e upgrade eder.
    /// </summary>
    public async Task<TenantUser?> ValidateAsync(string tenantKey, string username, string password, CancellationToken ct = default)
    {
        var profile = await _customerService.GetProfileAsync(tenantKey, ct);
        if (profile is null) return null;

        var users = Deserialize(profile.Metadata);
        var idx   = users.FindIndex(u => string.Equals(u.Username, username, StringComparison.OrdinalIgnoreCase));
        if (idx < 0) return null;

        var user = users[idx];
        if (!user.IsActive) return null;

        if (!VerifyPassword(password, user.PasswordHash))
            return null;

        // Legacy SHA256 hash tespit edildiyse BCrypt'e upgrade et
        if (IsLegacyHash(user.PasswordHash))
        {
            users[idx] = user with { PasswordHash = HashPassword(password) };
            await SaveAsync(profile, users, ct);
        }

        return user;
    }

    [Obsolete("ValidateAsync kullanin — sync wrapper DB cagri yaparak sync-over-async bloklar.")]
    public TenantUser? Validate(string tenantKey, string username, string password) =>
        ValidateAsync(tenantKey, username, password).GetAwaiter().GetResult();

    // ── Yazma ──────────────────────────────────────────────────────────────────

    public async Task AddOrUpdateUserAsync(
        string tenantKey,
        string username,
        string displayName,
        string password,
        CancellationToken ct,
        string? role = null,
        string? email = null)
    {
        var profile = await _customerService.GetProfileAsync(tenantKey, ct)
            ?? throw new InvalidOperationException($"Tenant bulunamadi: {tenantKey}");

        var users = Deserialize(profile.Metadata);
        var idx   = users.FindIndex(u => string.Equals(u.Username, username, StringComparison.OrdinalIgnoreCase));

        var hashed         = HashPassword(password);
        var normalizedRole = TenantUserRoles.NormalizeOrDefault(role);
        var normalizedEmail = string.IsNullOrWhiteSpace(email) ? null : email.Trim();

        if (idx >= 0)
            users[idx] = users[idx] with { DisplayName = displayName, PasswordHash = hashed, Role = normalizedRole, Email = normalizedEmail ?? users[idx].Email };
        else
            users.Add(new TenantUser(username, displayName, hashed, IsActive: true, CreatedAtUtc: DateTimeOffset.UtcNow, Role: normalizedRole, Email: normalizedEmail));

        await SaveAsync(profile, users, ct);
    }

    public async Task SetRoleAsync(string tenantKey, string username, string role, CancellationToken ct)
    {
        var profile = await _customerService.GetProfileAsync(tenantKey, ct)
            ?? throw new InvalidOperationException($"Tenant bulunamadi: {tenantKey}");

        var users = Deserialize(profile.Metadata);
        var idx   = users.FindIndex(u => string.Equals(u.Username, username, StringComparison.OrdinalIgnoreCase));
        if (idx < 0) return;

        users[idx] = users[idx] with { Role = TenantUserRoles.NormalizeOrDefault(role) };
        await SaveAsync(profile, users, ct);
    }

    public async Task SetActiveAsync(string tenantKey, string username, bool isActive, CancellationToken ct)
    {
        var profile = await _customerService.GetProfileAsync(tenantKey, ct)
            ?? throw new InvalidOperationException($"Tenant bulunamadi: {tenantKey}");

        var users = Deserialize(profile.Metadata);
        var idx   = users.FindIndex(u => string.Equals(u.Username, username, StringComparison.OrdinalIgnoreCase));
        if (idx < 0) return;

        users[idx] = users[idx] with { IsActive = isActive };
        await SaveAsync(profile, users, ct);
    }

    public async Task DeleteUserAsync(string tenantKey, string username, CancellationToken ct)
    {
        var profile = await _customerService.GetProfileAsync(tenantKey, ct)
            ?? throw new InvalidOperationException($"Tenant bulunamadi: {tenantKey}");

        var users = Deserialize(profile.Metadata);
        users.RemoveAll(u => string.Equals(u.Username, username, StringComparison.OrdinalIgnoreCase));
        await SaveAsync(profile, users, ct);
    }

    // ── Sifre sifirlama ────────────────────────────────────────────────────────

    private const int ResetTokenLength = 8;
    private static readonly TimeSpan ResetTokenLifetime = TimeSpan.FromHours(24);
    private const string ResetTokenAlphabet = "ABCDEFGHJKLMNPQRSTUVWXYZ23456789";

    /// <summary>
    /// Admin tarafindan tetiklenir. Token uretip hash'ini saklar; duz token geri dondurulur.
    /// Admin bunu kullaniciya iletir (sifreyi sifirlamak icin kullanilacak).
    /// </summary>
    public async Task<string> IssueResetTokenAsync(string tenantKey, string username, CancellationToken ct)
    {
        var profile = await _customerService.GetProfileAsync(tenantKey, ct)
            ?? throw new InvalidOperationException($"Tenant bulunamadi: {tenantKey}");

        var users = Deserialize(profile.Metadata);
        var idx   = users.FindIndex(u => string.Equals(u.Username, username, StringComparison.OrdinalIgnoreCase));
        if (idx < 0) throw new InvalidOperationException($"Kullanici bulunamadi: {username}");

        var token     = GenerateResetToken();
        var tokenHash = HashPassword(token);
        var expires   = DateTimeOffset.UtcNow.Add(ResetTokenLifetime);

        users[idx] = users[idx] with
        {
            PasswordResetTokenHash = tokenHash,
            PasswordResetExpiresAt = expires
        };

        await SaveAsync(profile, users, ct);
        return token;
    }

    /// <summary>
    /// Kullanici sifresini yeni degeri ile degistirir. Token gecerli ve suresi dolmamis olmalidir.
    /// </summary>
    public async Task<bool> ResetPasswordAsync(string tenantKey, string username, string token, string newPassword, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(token) || string.IsNullOrWhiteSpace(newPassword))
            return false;

        var profile = await _customerService.GetProfileAsync(tenantKey, ct);
        if (profile is null) return false;

        var users = Deserialize(profile.Metadata);
        var idx   = users.FindIndex(u => string.Equals(u.Username, username, StringComparison.OrdinalIgnoreCase));
        if (idx < 0) return false;

        var user = users[idx];
        if (!user.IsActive) return false;
        if (string.IsNullOrWhiteSpace(user.PasswordResetTokenHash)) return false;
        if (user.PasswordResetExpiresAt is null || user.PasswordResetExpiresAt.Value <= DateTimeOffset.UtcNow)
            return false;

        bool ok;
        try { ok = BCryptNet.Verify(token, user.PasswordResetTokenHash); }
        catch { ok = false; }
        if (!ok) return false;

        users[idx] = user with
        {
            PasswordHash           = HashPassword(newPassword),
            PasswordResetTokenHash = null,
            PasswordResetExpiresAt = null
        };
        await SaveAsync(profile, users, ct);
        return true;
    }

    // ── TOTP (2FA) ─────────────────────────────────────────────────────────────

    /// <summary>
    /// Kullanici icin yeni bir TOTP secret uretir ama henuz aktif etmez.
    /// Kullanici authenticator app ile kodu girip dogrulayana kadar TotpEnabled=false.
    /// otpauth URI dondurulur — UI tarafinda QR koduna donusturulur.
    /// </summary>
    public async Task<(string Secret, string ProvisioningUri)> BeginTotpEnrollmentAsync(
        string tenantKey, string username, string issuer, CancellationToken ct)
    {
        var profile = await _customerService.GetProfileAsync(tenantKey, ct)
            ?? throw new InvalidOperationException($"Tenant bulunamadi: {tenantKey}");

        var users = Deserialize(profile.Metadata);
        var idx   = users.FindIndex(u => string.Equals(u.Username, username, StringComparison.OrdinalIgnoreCase));
        if (idx < 0) throw new InvalidOperationException($"Kullanici bulunamadi: {username}");

        var secret = TotpAuthenticator.GenerateSecret();
        users[idx] = users[idx] with { TotpSecret = secret, TotpEnabled = false };
        await SaveAsync(profile, users, ct);

        var uri = TotpAuthenticator.BuildProvisioningUri(secret, $"{username}@{tenantKey}", issuer);
        return (secret, uri);
    }

    /// <summary>
    /// Kullanicinin enrollment sirasinda girdigi kodu dogrular ve TotpEnabled=true yapar.
    /// </summary>
    public async Task<bool> ConfirmTotpEnrollmentAsync(
        string tenantKey, string username, string code, CancellationToken ct)
    {
        var profile = await _customerService.GetProfileAsync(tenantKey, ct);
        if (profile is null) return false;

        var users = Deserialize(profile.Metadata);
        var idx   = users.FindIndex(u => string.Equals(u.Username, username, StringComparison.OrdinalIgnoreCase));
        if (idx < 0) return false;

        var user = users[idx];
        if (string.IsNullOrWhiteSpace(user.TotpSecret)) return false;
        if (!TotpAuthenticator.Verify(user.TotpSecret, code)) return false;

        users[idx] = user with { TotpEnabled = true };
        await SaveAsync(profile, users, ct);
        return true;
    }

    /// <summary>Login sirasinda 2.faktor dogrulamasi yapar.</summary>
    public bool VerifyTotp(TenantUser user, string code) =>
        user.TotpEnabled
        && !string.IsNullOrWhiteSpace(user.TotpSecret)
        && TotpAuthenticator.Verify(user.TotpSecret, code);

    /// <summary>2FA'yi kapatir ve secret'i temizler.</summary>
    public async Task DisableTotpAsync(string tenantKey, string username, CancellationToken ct)
    {
        var profile = await _customerService.GetProfileAsync(tenantKey, ct);
        if (profile is null) return;

        var users = Deserialize(profile.Metadata);
        var idx   = users.FindIndex(u => string.Equals(u.Username, username, StringComparison.OrdinalIgnoreCase));
        if (idx < 0) return;

        users[idx] = users[idx] with { TotpSecret = null, TotpEnabled = false };
        await SaveAsync(profile, users, ct);
    }

    private static string GenerateResetToken()
    {
        Span<char> buffer = stackalloc char[ResetTokenLength];
        for (int i = 0; i < ResetTokenLength; i++)
            buffer[i] = ResetTokenAlphabet[RandomNumberGenerator.GetInt32(ResetTokenAlphabet.Length)];
        return new string(buffer);
    }

    // ── Özel yardımcılar ───────────────────────────────────────────────────────

    private async Task SaveAsync(CustomerProfileDto profile, List<TenantUser> users, CancellationToken ct)
    {
        var metadata = new Dictionary<string, string>(profile.Metadata, StringComparer.OrdinalIgnoreCase)
        {
            [MetaKey] = JsonSerializer.Serialize(users)
        };

        await _customerService.UpsertAsync(profile.TenantKey, new UpsertCustomerRequest
        {
            TenantKey           = profile.TenantKey,
            Name                = profile.Name,
            IsActive            = profile.IsActive,
            AllowedProviders    = profile.AllowedProviders.ToList(),
            NotificationTargets = profile.NotificationTargets.ToList(),
            Metadata            = metadata
        }, ct);
    }

    private static List<TenantUser> Deserialize(Dictionary<string, string>? metadata)
    {
        if (metadata is null || !metadata.TryGetValue(MetaKey, out var json) || string.IsNullOrWhiteSpace(json))
            return new List<TenantUser>();

        try
        {
            return JsonSerializer.Deserialize<List<TenantUser>>(json) ?? new();
        }
        catch
        {
            return new();
        }
    }

    /// <summary>
    /// Async DB çağrısı yapmadan metadata'dan kullanıcı sayısını döner.
    /// BuildViewModelAsync gibi zaten yüklenmiş profile sahip yerlerde kullanılır.
    /// </summary>
    public static int CountUsers(Dictionary<string, string>? metadata)
    {
        if (metadata is null || !metadata.TryGetValue("users", out var json) || string.IsNullOrWhiteSpace(json))
            return 0;
        try { return JsonSerializer.Deserialize<List<TenantUser>>(json)?.Count ?? 0; }
        catch { return 0; }
    }

    // ── Password hashing ───────────────────────────────────────────────────────
    // BCrypt (work factor 11). Eski kayitlar SHA256 hex formatinda; verify fallback
    // ile dogrulanir ve ilk basarili login'de BCrypt'e upgrade edilir.

    private const int BCryptWorkFactor = 11;

    public static string HashPassword(string password) =>
        BCryptNet.HashPassword(password, BCryptWorkFactor);

    private static bool VerifyPassword(string input, string stored)
    {
        if (string.IsNullOrWhiteSpace(stored))
            return false;

        if (IsLegacyHash(stored))
        {
            var legacy = Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes("KY:" + input)));
            return CryptographicOperations.FixedTimeEquals(
                Encoding.ASCII.GetBytes(legacy),
                Encoding.ASCII.GetBytes(stored));
        }

        try { return BCryptNet.Verify(input, stored); }
        catch { return false; }
    }

    /// <summary>Hash formati SHA256 (eski) mi BCrypt (yeni) mi.</summary>
    private static bool IsLegacyHash(string hash) =>
        hash.Length == 64 && hash.All(Uri.IsHexDigit);
}

/// <summary>
/// Tenant kullanicisi icin granular rol. Admin rolu tenant disidir (StudioRoles.Admin).
/// Tenant ici roller:
///   ReadOnly → sadece okuma (liste/detay)
///   Operator → olusturma + duzenleme (varsayilan)
///   Manager  → Operator + kullanici yonetimi + lisans okuma (tenant ici yonetici)
/// </summary>
public static class TenantUserRoles
{
    public const string ReadOnly = "ReadOnly";
    public const string Operator = "Operator";
    public const string Manager  = "Manager";

    public static bool IsValid(string? role) =>
        role is ReadOnly or Operator or Manager;

    public static string NormalizeOrDefault(string? role) =>
        IsValid(role) ? role! : Operator;

    public static IReadOnlyList<string> All => new[] { ReadOnly, Operator, Manager };
}

public sealed record TenantUser(
    string          Username,
    string          DisplayName,
    string          PasswordHash,
    bool            IsActive,
    DateTimeOffset  CreatedAtUtc,
    string?         PasswordResetTokenHash = null,
    DateTimeOffset? PasswordResetExpiresAt = null,
    string          Role                   = "Operator",
    string?         TotpSecret             = null,
    bool            TotpEnabled            = false,
    string?         Email                  = null);
