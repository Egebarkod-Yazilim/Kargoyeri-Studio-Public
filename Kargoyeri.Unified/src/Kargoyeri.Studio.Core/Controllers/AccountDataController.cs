using System.Security.Claims;
using Kargoyeri.Application.Services;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Kargoyeri.Contracts.Dtos;
using Kargoyeri.Studio.Core.Infrastructure;
using Kargoyeri.Studio.Core.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Kargoyeri.Studio.Core.Controllers;

/// <summary>
/// KVKK md. 7 — Tenant kullanicisinin kendi verilerinin silinmesini talep etmesi
/// icin self-service akis (P1-#2).
///
/// /account/data            → mevcut talep durumu + form
/// /account/data/request    → yeni talep olustur (POST)
/// /account/data/cancel     → bekleyen talebi iptal et (POST)
///
/// Talep sonrasi:
///   1) tenant Metadata'ya kvkk.deletion.* alanlari yazilir
///   2) tenant pasif yapilir (IsActive = false) — login engellenir
///   3) 30 gun bekleme suresi sonra (DataRetentionService — P1-#3) hard-delete
/// </summary>
[Authorize]
[Route("account/data")]
public sealed class AccountDataController : Controller
{
    private readonly CustomerService _customerService;
    private readonly IStudioWorkspaceContext _workspace;
    private readonly AuditExportPackageService _auditExportPackageService;
    private readonly ILogger<AccountDataController> _logger;

    public AccountDataController(
        CustomerService customerService,
        IStudioWorkspaceContext workspace,
        AuditExportPackageService auditExportPackageService,
        ILogger<AccountDataController> logger)
    {
        _customerService = customerService;
        _workspace       = workspace;
        _auditExportPackageService = auditExportPackageService;
        _logger          = logger;
    }

    [HttpGet("")]
    public async Task<IActionResult> Index(CancellationToken ct)
    {
        var vm = await BuildViewModelAsync(ct);
        return View(vm);
    }

    [HttpPost("request")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RequestDeletion(
        [FromForm] string? reason,
        [FromForm] bool confirmCheckbox,
        CancellationToken ct)
    {
        var tenantKey = _workspace.GetCurrentCode();
        var profile = await _customerService.GetProfileAsync(tenantKey, ct);
        if (profile is null)
        {
            StudioFlash.Error(TempData, "Hesap bulunamadi.");
            return RedirectToAction(nameof(Index));
        }

        if (!confirmCheckbox)
        {
            StudioFlash.Error(TempData, "Talebi onaylamak icin onay kutusunu isaretlemeniz gerekir.");
            return RedirectToAction(nameof(Index));
        }

        var existing = KvkkDeletionRequest.Read(profile.Metadata);
        if (existing.IsPending || existing.IsApproved)
        {
            StudioFlash.Warning(TempData, "Bu hesap icin zaten devam eden bir silme talebi var.");
            return RedirectToAction(nameof(Index));
        }

        var requestedBy = User.FindFirst(StudioRoles.UsernameClaim)?.Value
                          ?? User.FindFirst(ClaimTypes.Name)?.Value
                          ?? "(unknown)";

        var metadata = KvkkDeletionRequest.ApplyRequest(profile.Metadata, requestedBy, reason);

        // Tenant pasif yapilir — bu noktadan sonra login engellenir.
        await _customerService.UpsertAsync(tenantKey, new UpsertCustomerRequest
        {
            TenantKey           = profile.TenantKey,
            Name                = profile.Name,
            IsActive            = false,
            AllowedProviders    = profile.AllowedProviders.ToList(),
            NotificationTargets = profile.NotificationTargets.ToList(),
            Metadata            = metadata
        }, ct);

        _logger.LogWarning(
            "KVKK silme talebi olusturuldu: tenant={TenantKey} requestedBy={User} reason={Reason}",
            tenantKey, requestedBy, (reason ?? "").Trim());

        StudioFlash.Success(TempData,
            $"Silme talebiniz alindi. Hesabiniz {KvkkDeletionRequest.GracePeriodDays} gun icinde kalici olarak silinecek. " +
            "Bu sure icinde admin'e basvurarak iptal edebilirsiniz. Cikis yapiyoruz...");

        // Logout — kullanici artik bu hesaba erisemez.
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        return RedirectToAction("Login", "Access");
    }

    [HttpPost("cancel")]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = StudioRoles.Admin)]
    public async Task<IActionResult> CancelDeletion([FromForm] string tenantKey, CancellationToken ct)
    {
        var profile = await _customerService.GetProfileAsync(tenantKey.Trim(), ct);
        if (profile is null) return NotFound();

        var state = KvkkDeletionRequest.Read(profile.Metadata);
        if (!state.HasRequest || state.Status == KvkkDeletionRequest.StatusCancelled)
        {
            StudioFlash.Warning(TempData, "Bu hesap icin aktif bir silme talebi yok.");
            return RedirectToAction("Index", "TenantSwitch");
        }

        var metadata = KvkkDeletionRequest.ApplyCancellation(profile.Metadata);

        // Hesap geri aktif edilir.
        await _customerService.UpsertAsync(tenantKey, new UpsertCustomerRequest
        {
            TenantKey           = profile.TenantKey,
            Name                = profile.Name,
            IsActive            = true,
            AllowedProviders    = profile.AllowedProviders.ToList(),
            NotificationTargets = profile.NotificationTargets.ToList(),
            Metadata            = metadata
        }, ct);

        _logger.LogWarning(
            "KVKK silme talebi IPTAL edildi: tenant={TenantKey} cancelledBy={User}",
            tenantKey, User.Identity?.Name);

        StudioFlash.Success(TempData, $"'{profile.Name}' icin silme talebi iptal edildi ve hesap yeniden aktiflestirildi.");
        return RedirectToAction("Index", "TenantSwitch");
    }

    [HttpPost("approve")]
    [ValidateAntiForgeryToken]
    [Authorize(Roles = StudioRoles.Admin)]
    public async Task<IActionResult> ApproveDeletion([FromForm] string tenantKey, CancellationToken ct)
    {
        var profile = await _customerService.GetProfileAsync(tenantKey.Trim(), ct);
        if (profile is null) return NotFound();

        var state = KvkkDeletionRequest.Read(profile.Metadata);
        if (!state.IsPending)
        {
            StudioFlash.Warning(TempData, "Bu hesap icin onay bekleyen talep yok.");
            return RedirectToAction("Index", "TenantSwitch");
        }

        var approver = User.FindFirst(StudioRoles.UsernameClaim)?.Value
                       ?? User.Identity?.Name
                       ?? "(admin)";

        var metadata = KvkkDeletionRequest.ApplyApproval(profile.Metadata, approver);

        await _customerService.UpsertAsync(tenantKey, new UpsertCustomerRequest
        {
            TenantKey           = profile.TenantKey,
            Name                = profile.Name,
            IsActive            = false,
            AllowedProviders    = profile.AllowedProviders.ToList(),
            NotificationTargets = profile.NotificationTargets.ToList(),
            Metadata            = metadata
        }, ct);

        _logger.LogWarning(
            "KVKK silme talebi ONAYLANDI: tenant={TenantKey} approvedBy={Approver} hardDeleteAt={Hd}",
            tenantKey, approver, state.HardDeleteAt);

        StudioFlash.Success(TempData,
            $"'{profile.Name}' icin silme talebi onaylandi. Hard-delete tarihi: " +
            $"{state.HardDeleteAt:dd.MM.yyyy} (otomatik anonimlestirme bu tarihte calisacak).");
        return RedirectToAction("Index", "TenantSwitch");
    }

    [HttpGet("export")]
    public async Task<IActionResult> ExportMyData(
        [FromQuery] string? from,
        [FromQuery] string? to,
        CancellationToken ct)
    {
        var tenantKey = _workspace.GetCurrentCode();
        var fromUtc = DateTime.TryParse(from, out var parsedFrom)
            ? parsedFrom.Date
            : DateTime.UtcNow.Date.AddDays(-30);
        var toUtc = DateTime.TryParse(to, out var parsedTo)
            ? parsedTo.Date.AddDays(1).AddTicks(-1)
            : DateTime.UtcNow.Date.AddDays(1).AddTicks(-1);

        var package = await _auditExportPackageService.BuildAsync(tenantKey, fromUtc, toUtc, shipmentReference: null, ct);
        return File(package.Content, "application/zip", package.FileName);
    }

    private async Task<AccountDataViewModel> BuildViewModelAsync(CancellationToken ct)
    {
        var tenantKey = _workspace.GetCurrentCode();
        var profile = await _customerService.GetProfileAsync(tenantKey, ct);

        var state = profile is null
            ? new KvkkDeletionRequest.State(false, null, null, null, null, null, null, null)
            : KvkkDeletionRequest.Read(profile.Metadata);

        return new AccountDataViewModel
        {
            TenantKey      = tenantKey,
            TenantName     = profile?.Name ?? tenantKey,
            IsActive       = profile?.IsActive ?? false,
            ProviderCount  = profile?.AllowedProviders.Count ?? 0,
            DeletionState  = state,
            GracePeriodDays = KvkkDeletionRequest.GracePeriodDays
        };
    }
}
