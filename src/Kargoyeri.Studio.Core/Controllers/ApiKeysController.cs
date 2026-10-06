using Kargoyeri.Application.Services;
using Kargoyeri.Contracts.Dtos;
using Kargoyeri.Studio.Core.Infrastructure;
using Kargoyeri.Studio.Core.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Kargoyeri.Studio.Core.Controllers;

/// <summary>
/// Tenant bazli API anahtarlarinin yonetimi (P1-#4).
///
/// /apikeys           → mevcut anahtarlarin listesi + yeni olusturma formu
/// /apikeys/create    → yeni anahtar uretir (plain anahtari TempData ile bir kez gosterir)
/// /apikeys/revoke    → anahtari pasiflestirir (auth disinda kalir)
/// /apikeys/delete    → anahtari kalici siler
///
/// Yetki: tenant kullanicisi kendi tenant'inin anahtarlarini gorur.
/// Admin tum tenant'lar icin kullanabilir (tenantKey query param ile).
/// </summary>
[Authorize(Roles = StudioRoles.SuperAdmin)]
[Route("apikeys")]
public sealed class ApiKeysController : Controller
{
    private readonly CustomerService _customerService;
    private readonly IStudioWorkspaceContext _workspace;
    private readonly ILogger<ApiKeysController> _logger;

    public ApiKeysController(
        CustomerService customerService,
        IStudioWorkspaceContext workspace,
        ILogger<ApiKeysController> logger)
    {
        _customerService = customerService;
        _workspace       = workspace;
        _logger          = logger;
    }

    [HttpGet("")]
    public async Task<IActionResult> Index(CancellationToken ct)
    {
        var profile = await EnsureProfileAsync(ct);
        var tenantKey = profile.TenantKey;

        var vm = new ApiKeysPageViewModel
        {
            TenantKey  = tenantKey,
            TenantName = profile.Name,
            Keys       = TenantApiKeyStore.Read(profile.Metadata),
            NewlyCreatedPlainKey = TempData["NewApiKey"] as string,
            NewlyCreatedName     = TempData["NewApiKeyName"] as string
        };
        return View(vm);
    }

    [HttpPost("create")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create([FromForm] string? name, CancellationToken ct)
    {
        var profile = await EnsureProfileAsync(ct);
        var tenantKey = profile.TenantKey;

        var result = TenantApiKeyStore.Generate(profile.Metadata, name ?? "Adsız");

        await _customerService.UpsertAsync(tenantKey, new UpsertCustomerRequest
        {
            TenantKey           = profile.TenantKey,
            Name                = profile.Name,
            IsActive            = profile.IsActive,
            AllowedProviders    = profile.AllowedProviders.ToList(),
            NotificationTargets = profile.NotificationTargets.ToList(),
            Metadata            = result.Metadata
        }, ct);

        _logger.LogInformation("API key olusturuldu: tenant={Tenant} name={Name} keyId={Id}",
            tenantKey, result.Record.Name, result.Record.Id);

        TempData["NewApiKey"]     = result.PlainKey;
        TempData["NewApiKeyName"] = result.Record.Name;
        StudioFlash.Success(TempData,
            "Yeni API anahtarı oluşturuldu. Anahtar yalnızca bir kez gösterilir. Güvenli bir yere kaydedin.");
        return RedirectToAction(nameof(Index));
    }

    [HttpPost("revoke")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Revoke([FromForm] string keyId, CancellationToken ct)
    {
        var profile = await EnsureProfileAsync(ct);
        var tenantKey = profile.TenantKey;

        var meta = TenantApiKeyStore.Revoke(profile.Metadata, keyId);
        await _customerService.UpsertAsync(tenantKey, new UpsertCustomerRequest
        {
            TenantKey           = profile.TenantKey,
            Name                = profile.Name,
            IsActive            = profile.IsActive,
            AllowedProviders    = profile.AllowedProviders.ToList(),
            NotificationTargets = profile.NotificationTargets.ToList(),
            Metadata            = meta
        }, ct);

        _logger.LogWarning("API key REVOKE: tenant={Tenant} keyId={Id} by={User}",
            tenantKey, keyId, User.Identity?.Name);

        StudioFlash.Warning(TempData, "API anahtarı pasifleştirildi.");
        return RedirectToAction(nameof(Index));
    }

    [HttpPost("delete")]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = StudioRoles.SuperAdmin)]
    public async Task<IActionResult> Delete([FromForm] string keyId, CancellationToken ct)
    {
        var profile = await EnsureProfileAsync(ct);
        var tenantKey = profile.TenantKey;

        var meta = TenantApiKeyStore.Delete(profile.Metadata, keyId);
        await _customerService.UpsertAsync(tenantKey, new UpsertCustomerRequest
        {
            TenantKey           = profile.TenantKey,
            Name                = profile.Name,
            IsActive            = profile.IsActive,
            AllowedProviders    = profile.AllowedProviders.ToList(),
            NotificationTargets = profile.NotificationTargets.ToList(),
            Metadata            = meta
        }, ct);

        _logger.LogWarning("API key DELETE: tenant={Tenant} keyId={Id} by={User}",
            tenantKey, keyId, User.Identity?.Name);

        StudioFlash.Success(TempData, "API anahtarı kalıcı olarak silindi.");
        return RedirectToAction(nameof(Index));
    }

    private async Task<Kargoyeri.Contracts.Dtos.CustomerProfileDto> EnsureProfileAsync(CancellationToken ct)
    {
        var tenant = await _workspace.GetOrCreateAsync(_customerService, ct);
        var profile = await _customerService.GetProfileAsync(tenant.TenantKey, ct);
        return profile ?? throw new InvalidOperationException("Çalışma alanı kaydı oluşturulamadı.");
    }
}
