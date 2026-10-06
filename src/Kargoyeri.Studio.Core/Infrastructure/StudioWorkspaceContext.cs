using Kargoyeri.Application.Services;
using Kargoyeri.Domain.Entities;
using System.Security.Claims;

namespace Kargoyeri.Studio.Core.Infrastructure;

public interface IStudioWorkspaceContext
{
    string GetCurrentCode();
    string? GetCurrentName();
    void Set(string customerCode, string? customerName);
    Task<CustomerTenant> GetOrCreateAsync(CustomerService customerService, CancellationToken cancellationToken);

    /// <summary>P2-#5 — Admin baska bir tenant'a "view as" yapti mi?</summary>
    bool IsImpersonating();

    /// <summary>P2-#5 — Aktif impersonation icin baslangic zamani.</summary>
    DateTimeOffset? GetImpersonationStartedAt();

    /// <summary>P2-#5 — Admin baska bir tenant'i izlemeye basladigini isaretle.</summary>
    void StartImpersonation();

    /// <summary>P2-#5 — Impersonation isaretini ve workspace cookie'lerini temizler.</summary>
    void StopImpersonation();
}

internal sealed class StudioWorkspaceContext : IStudioWorkspaceContext
{
    private const string CodeCookie = "kargoyeri-studio-customer-code";
    private const string NameCookie = "kargoyeri-studio-customer-name";
    private const string ImpersonateCookie = "kargoyeri-impersonate-since";
    private readonly IHttpContextAccessor _httpContextAccessor;

    public StudioWorkspaceContext(IHttpContextAccessor httpContextAccessor)
    {
        _httpContextAccessor = httpContextAccessor;
    }

    public string GetCurrentCode()
    {
        var lockedCode = _httpContextAccessor.HttpContext?.User.FindFirstValue(StudioRoles.WorkspaceCodeClaim);
        if (!string.IsNullOrWhiteSpace(lockedCode))
        {
            return lockedCode;
        }

        var code = _httpContextAccessor.HttpContext?.Request.Cookies[CodeCookie];
        return string.IsNullOrWhiteSpace(code) ? "studio-demo" : code;
    }

    public string? GetCurrentName()
    {
        return _httpContextAccessor.HttpContext?.User.FindFirstValue(StudioRoles.WorkspaceNameClaim)
            ?? _httpContextAccessor.HttpContext?.Request.Cookies[NameCookie];
    }

    public void Set(string customerCode, string? customerName)
    {
        var response = _httpContextAccessor.HttpContext?.Response;
        if (response is null)
        {
            return;
        }

        if (_httpContextAccessor.HttpContext?.User.HasClaim(x => x.Type == StudioRoles.WorkspaceCodeClaim) == true)
        {
            return;
        }

        var options = new CookieOptions
        {
            HttpOnly = false,
            IsEssential = true,
            Expires = DateTimeOffset.UtcNow.AddDays(30)
        };

        response.Cookies.Append(CodeCookie, customerCode, options);
        response.Cookies.Append(NameCookie, customerName ?? customerCode, options);
    }

    public bool IsImpersonating() =>
        !string.IsNullOrWhiteSpace(_httpContextAccessor.HttpContext?.Request.Cookies[ImpersonateCookie]);

    public DateTimeOffset? GetImpersonationStartedAt()
    {
        var raw = _httpContextAccessor.HttpContext?.Request.Cookies[ImpersonateCookie];
        if (string.IsNullOrWhiteSpace(raw)) return null;
        if (long.TryParse(raw, out var unix))
            return DateTimeOffset.FromUnixTimeSeconds(unix);
        return null;
    }

    public void StartImpersonation()
    {
        var response = _httpContextAccessor.HttpContext?.Response;
        if (response is null) return;

        var options = new CookieOptions
        {
            HttpOnly    = true,
            IsEssential = true,
            Expires     = DateTimeOffset.UtcNow.AddHours(8) // turn-off otomatik 8 saat
        };
        response.Cookies.Append(ImpersonateCookie,
            DateTimeOffset.UtcNow.ToUnixTimeSeconds().ToString(),
            options);
    }

    public void StopImpersonation()
    {
        var response = _httpContextAccessor.HttpContext?.Response;
        if (response is null) return;
        response.Cookies.Delete(ImpersonateCookie);
        response.Cookies.Delete(CodeCookie);
        response.Cookies.Delete(NameCookie);
    }

    public async Task<CustomerTenant> GetOrCreateAsync(CustomerService customerService, CancellationToken cancellationToken)
    {
        var code = GetCurrentCode();
        var name = GetCurrentName() ?? "Kargoyeri Studio Demo";

        var profile = await customerService.GetProfileAsync(code, cancellationToken);
        if (profile is null)
        {
            profile = await customerService.UpsertAsync(code, new Kargoyeri.Contracts.Dtos.UpsertCustomerRequest
            {
                Name = name,
                IsActive = true,
                AllowedProviders = new List<Kargoyeri.Contracts.Enums.CargoProviderTypeDto>(),
                NotificationTargets = new List<Kargoyeri.Contracts.Dtos.NotificationTargetDto>(),
                Metadata = new Dictionary<string, string>()
            }, cancellationToken);
        }

        Set(profile.TenantKey, profile.Name);
        return new CustomerTenant
        {
            TenantKey = profile.TenantKey,
            Name = profile.Name,
            IsActive = profile.IsActive
        };
    }
}
