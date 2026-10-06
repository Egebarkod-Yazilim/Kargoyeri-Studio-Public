using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;

namespace Kargoyeri.Studio.Core.Infrastructure;

/// <summary>
/// P5-#4 — Self-service signup icin bekleyen kayit havuzu.
/// E-posta dogrulanana kadar tenant gercekten olusturulmaz.
/// JSON dosyasi tabanli (basit, bolgeler-arasi senk degil — P5-#8'de S3 backend ile esitlenecek).
/// </summary>
public sealed class PendingSignupStore
{
    private readonly string _file;
    private readonly ConcurrentDictionary<string, PendingSignup> _store = new(StringComparer.OrdinalIgnoreCase);
    private readonly SemaphoreSlim _ioGate = new(1, 1);

    public PendingSignupStore(IWebHostEnvironment env)
    {
        var dir = Path.Combine(env.ContentRootPath, "pending-signups");
        System.IO.Directory.CreateDirectory(dir);
        _file = Path.Combine(dir, "pending.json");
        Rehydrate();
    }

    public PendingSignup Create(string email, string companyName, string fullName, string? phone, string? vkn,
        IEnumerable<string>? selectedChannels = null, IEnumerable<string>? selectedExtraSources = null)
    {
        var token = GenerateToken();
        var rec = new PendingSignup
        {
            Token = token,
            Email = email.Trim().ToLowerInvariant(),
            CompanyName = companyName.Trim(),
            FullName = fullName.Trim(),
            Phone = phone?.Trim(),
            Vkn = vkn?.Trim(),
            SelectedChannels = (selectedChannels ?? Array.Empty<string>())
                .Where(c => !string.IsNullOrWhiteSpace(c))
                .Select(c => c.Trim().ToLowerInvariant())
                .Distinct()
                .ToList(),
            SelectedExtraSources = (selectedExtraSources ?? Array.Empty<string>())
                .Where(c => !string.IsNullOrWhiteSpace(c))
                .Select(c => c.Trim().ToLowerInvariant())
                .Distinct()
                .ToList(),
            CreatedAtUtc = DateTimeOffset.UtcNow,
            ExpiresAtUtc = DateTimeOffset.UtcNow.AddHours(24),
            Confirmed = false
        };
        _store[token] = rec;
        _ = SaveAsync();
        return rec;
    }

    public PendingSignup? Find(string token) =>
        _store.TryGetValue(token, out var rec) ? rec : null;

    public bool Confirm(string token)
    {
        if (!_store.TryGetValue(token, out var rec)) return false;
        if (rec.Confirmed) return true;
        if (rec.ExpiresAtUtc < DateTimeOffset.UtcNow) return false;
        rec.Confirmed = true;
        rec.ConfirmedAtUtc = DateTimeOffset.UtcNow;
        _ = SaveAsync();
        return true;
    }

    public void Remove(string token)
    {
        if (_store.TryRemove(token, out _)) _ = SaveAsync();
    }

    public IReadOnlyList<PendingSignup> List() => _store.Values.OrderByDescending(s => s.CreatedAtUtc).ToList();

    private void Rehydrate()
    {
        if (!File.Exists(_file)) return;
        try
        {
            var json = File.ReadAllText(_file);
            var list = JsonSerializer.Deserialize<List<PendingSignup>>(json) ?? new();
            foreach (var r in list) _store[r.Token] = r;
        }
        catch { /* corrupt — ignore */ }
    }

    private async Task SaveAsync()
    {
        await _ioGate.WaitAsync();
        try
        {
            var json = JsonSerializer.Serialize(_store.Values.ToList(), new JsonSerializerOptions { WriteIndented = true });
            await File.WriteAllTextAsync(_file, json);
        }
        catch { /* best-effort */ }
        finally { _ioGate.Release(); }
    }

    private static string GenerateToken()
    {
        var bytes = RandomNumberGenerator.GetBytes(32);
        return Convert.ToHexString(bytes);
    }
}

public sealed class PendingSignup
{
    public string Token { get; set; } = string.Empty;
    public string Email { get; set; } = string.Empty;
    public string CompanyName { get; set; } = string.Empty;
    public string FullName { get; set; } = string.Empty;
    public string? Phone { get; set; }
    public string? Vkn { get; set; }
    public DateTimeOffset CreatedAtUtc { get; set; }
    public DateTimeOffset ExpiresAtUtc { get; set; }
    public bool Confirmed { get; set; }
    public DateTimeOffset? ConfirmedAtUtc { get; set; }
    public List<string> SelectedChannels { get; set; } = new();
    public List<string> SelectedExtraSources { get; set; } = new();
}
