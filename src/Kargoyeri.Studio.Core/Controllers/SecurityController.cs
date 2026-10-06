using System.Security.Claims;
using Kargoyeri.Studio.Core.Infrastructure;
using Kargoyeri.Studio.Core.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Kargoyeri.Studio.Core.Controllers;

/// <summary>
/// Kullanicinin kendi hesabi uzerinde guvenlik ayarlarini yonetmesini saglar
/// (TOTP enrollment / disable). Admin'ler de kullanabilir, ama Admin rolu icin
/// tenantKey/username claim'leri olmadigi icin sadece kendi tenant kullanicilari icindir.
/// </summary>
[Authorize]
public sealed class SecurityController : Controller
{
    private readonly TenantUserService _userService;

    public SecurityController(TenantUserService userService)
    {
        _userService = userService;
    }

    [HttpGet]
    public async Task<IActionResult> Totp(CancellationToken cancellationToken)
    {
        var ctx = ResolveContext();
        if (ctx is null) return Forbid();

        var users = await _userService.GetUsersAsync(ctx.Value.TenantKey, cancellationToken);
        var me    = users.FirstOrDefault(u => string.Equals(u.Username, ctx.Value.Username, StringComparison.OrdinalIgnoreCase));

        return View(new TotpEnrollmentViewModel
        {
            TenantKey      = ctx.Value.TenantKey,
            Username       = ctx.Value.Username,
            AlreadyEnabled = me?.TotpEnabled ?? false
        });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Begin(CancellationToken cancellationToken)
    {
        var ctx = ResolveContext();
        if (ctx is null) return Forbid();

        var (secret, uri) = await _userService.BeginTotpEnrollmentAsync(
            ctx.Value.TenantKey,
            ctx.Value.Username,
            issuer: $"Kargoyeri ({ctx.Value.TenantName})",
            ct: cancellationToken);

        return View("Totp", new TotpEnrollmentViewModel
        {
            TenantKey       = ctx.Value.TenantKey,
            Username        = ctx.Value.Username,
            AlreadyEnabled  = false,
            Secret          = secret,
            ProvisioningUri = uri,
            Message         = "Authenticator uygulamaniza QR kodu okutun veya secret'i manuel girin, ardindan asagidaki kutuya kodu girip dogrulayin."
        });
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Confirm(TotpEnrollmentViewModel model, CancellationToken cancellationToken)
    {
        var ctx = ResolveContext();
        if (ctx is null) return Forbid();

        if (!ModelState.IsValid)
        {
            model.ErrorMessage = "Gecersiz kod formati.";
            return View("Totp", model);
        }

        var ok = await _userService.ConfirmTotpEnrollmentAsync(
            ctx.Value.TenantKey, ctx.Value.Username, model.Code, cancellationToken);

        if (!ok)
        {
            model.ErrorMessage = "Kod hatali. Authenticator uygulamanizda gosterilen guncel kodu girin.";
            return View("Totp", model);
        }

        StudioFlash.Success(TempData, "Iki adimli dogrulama aktif edildi. Bir sonraki giriste kod sorulacak.");
        return RedirectToAction(nameof(Totp));
    }

    [HttpPost, ValidateAntiForgeryToken]
    public async Task<IActionResult> Disable(CancellationToken cancellationToken)
    {
        var ctx = ResolveContext();
        if (ctx is null) return Forbid();

        await _userService.DisableTotpAsync(ctx.Value.TenantKey, ctx.Value.Username, cancellationToken);
        StudioFlash.Warning(TempData, "Iki adimli dogrulama kapatildi.");
        return RedirectToAction(nameof(Totp));
    }

    private (string TenantKey, string TenantName, string Username)? ResolveContext()
    {
        var tenantKey = User.FindFirst(StudioRoles.WorkspaceCodeClaim)?.Value;
        var username  = User.FindFirst(StudioRoles.UsernameClaim)?.Value;
        var name      = User.FindFirst(StudioRoles.WorkspaceNameClaim)?.Value
                        ?? User.FindFirst(ClaimTypes.Name)?.Value
                        ?? string.Empty;

        if (string.IsNullOrWhiteSpace(tenantKey) || string.IsNullOrWhiteSpace(username))
            return null;
        return (tenantKey, name, username);
    }
}
