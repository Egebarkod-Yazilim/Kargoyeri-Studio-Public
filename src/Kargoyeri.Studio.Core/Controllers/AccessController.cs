using System.Security.Claims;
using Kargoyeri.Application.Services;
using Kargoyeri.Studio.Core.Infrastructure;
using Kargoyeri.Studio.Core.Models;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;
using Microsoft.Extensions.Options;

namespace Kargoyeri.Studio.Core.Controllers;

[AllowAnonymous]
public sealed class AccessController : Controller
{
    private readonly StudioAccessOptions _options;
    private readonly IWebHostEnvironment _environment;
    private readonly CustomerService _customerService;

    public AccessController(
        IOptions<StudioAccessOptions> options,
        IWebHostEnvironment environment,
        CustomerService customerService)
    {
        _options = options.Value;
        _environment = environment;
        _customerService = customerService;
    }

    [HttpGet]
    public IActionResult Login()
    {
        if (User.Identity?.IsAuthenticated == true)
            return RedirectToAction("Index", "Home");

        return View(new AccessLoginAdminViewModel());
    }

    /// <summary>Admin e-posta + parola ile giris (StudioAccess:Admins listesi).</summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    [EnableRateLimiting("login")]
    public async Task<IActionResult> Login(AccessLoginAdminViewModel admin, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            admin.ErrorMessage = "E-posta ve parolayi dogru girin.";
            return View(admin);
        }

        var email = admin.Email.Trim();
        var match = _options.Admins.FirstOrDefault(a =>
            a.IsActive && string.Equals(a.Email, email, StringComparison.OrdinalIgnoreCase));

        if (match is null || !VerifyAdminPassword(match, admin.Password))
        {
            // Sabit gecikme yok; rate limiter zaten 10/5dk koruyor
            admin.ErrorMessage = "E-posta veya parola yanlis.";
            return View(admin);
        }

        var displayName = string.IsNullOrWhiteSpace(match.DisplayName) ? match.Email : match.DisplayName;
        if (_environment.IsProduction() && !IsAdminTotpConfigured(match))
        {
            admin.ErrorMessage = "Production yonetici girisi icin TOTP zorunlu. StudioAccess:Admins altinda TotpSecret ve TotpEnabled alanlarini doldurun.";
            return View(admin);
        }

        if (RequiresAdminTotp(match))
        {
            TempData[PendingAdminTotpKey] = $"{match.Email}|{displayName}";
            return RedirectToAction(nameof(LoginAdminTotp));
        }

        await SignInAdminAsync(match, displayName);
        return RedirectToAction("Index", "Home");
    }

    private Task SignInAdminAsync(StudioAdminUser admin, string displayName)
    {
        var isSuperAdmin = string.Equals(admin.Role, "SuperAdmin", StringComparison.OrdinalIgnoreCase);
        var role = isSuperAdmin ? StudioRoles.SuperAdmin : StudioRoles.Admin;
        return SignInAsync(displayName, role, workspaceCode: null, workspaceName: null, username: admin.Email);
    }

    private static bool VerifyAdminPassword(StudioAdminUser admin, string input)
    {
        if (string.IsNullOrEmpty(input)) return false;

        if (!string.IsNullOrWhiteSpace(admin.PasswordHash))
        {
            try { return BCrypt.Net.BCrypt.Verify(input, admin.PasswordHash); }
            catch { return false; }
        }

        if (!string.IsNullOrEmpty(admin.Password))
        {
            // Sabit-zamanli karsilastirma — timing attack korumasi
            var a = System.Text.Encoding.UTF8.GetBytes(admin.Password);
            var b = System.Text.Encoding.UTF8.GetBytes(input);
            return a.Length == b.Length &&
                   System.Security.Cryptography.CryptographicOperations.FixedTimeEquals(a, b);
        }

        return false;
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [EnableRateLimiting("login")]
    [NonAction]
    [ActionName("LegacyCodeLogin")]
    public async Task<IActionResult> LoginWithAccessCode(AccessLoginViewModel model, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
            return View(model);

        var accessCode = model.AccessCode.Trim();

        // ── 1. Admin kodu ────────────────────────────────────────────────────────
        if (IsAdminCode(accessCode))
        {
            if (_environment.IsProduction())
            {
                model.ErrorMessage = "Production ortaminda admin access code devre disidir. E-posta/parola ve TOTP kullanin.";
                return View(model);
            }

            await SignInAsync("Admin", StudioRoles.Admin, workspaceCode: null, workspaceName: null, username: null);
            return RedirectToAction("Index", "Home");
        }

        // ── 2. DB'deki lisans kodu (her müşteriye özel) ─────────────────────────
        var allCustomers = await _customerService.ListAllAsync(cancellationToken);
        var match = allCustomers.FirstOrDefault(p =>
            p.Metadata.TryGetValue("license.code", out var code) &&
            string.Equals(code, accessCode, StringComparison.Ordinal));

        if (match is not null)
        {
            if (!match.IsActive)
            {
                model.ErrorMessage = "Bu hesap aktif degil. Yoneticinizle iletisime gecin.";
                return View(model);
            }

            var license = LicenseHelper.Read(match.Metadata);
            if (!license.HasLicense || license.IsExpired)
            {
                model.ErrorMessage = "Lisans suresi dolmus veya tanimlanmamis. Yoneticinizle iletisime gecin.";
                return View(model);
            }

            // C — Kullanıcı sistemi: müşteri için kullanıcı varsa username/password gerek
            var pendingTenant = new PendingTenantClaim(match.TenantKey, match.Name);
            TempData[PendingTenantKey] = $"{match.TenantKey}|{match.Name}";
            return RedirectToAction(nameof(LoginUser));
        }

        // ── 3. Eski tek-operator kodu (geriye dönük uyumluluk) ──────────────────
        if (!string.IsNullOrWhiteSpace(_options.OperatorAccessCode)
            && string.Equals(accessCode, _options.OperatorAccessCode, StringComparison.Ordinal))
        {
            if (string.IsNullOrWhiteSpace(_options.LockedWorkspaceCode))
            {
                model.ErrorMessage = "Operator erisimi icin LockedWorkspaceCode tanimli olmali.";
                return View(model);
            }

            await SignInAsync(
                _options.LockedWorkspaceName ?? _options.LockedWorkspaceCode,
                StudioRoles.Operator,
                _options.LockedWorkspaceCode.Trim(),
                _options.LockedWorkspaceName?.Trim(),
                username: null);
            return RedirectToAction("Index", "Home");
        }

        // ── 4. Config'deki CustomerCodes (legacy) ───────────────────────────────
        if (_options.CustomerCodes.TryGetValue(accessCode, out var tenantKey))
        {
            var profile = await _customerService.GetProfileAsync(tenantKey, cancellationToken);
            if (profile is null || !profile.IsActive)
            {
                model.ErrorMessage = "Bu hesap aktif degil veya bulunamadi.";
                return View(model);
            }

            TempData[PendingTenantKey] = $"{profile.TenantKey}|{profile.Name}";
            return RedirectToAction(nameof(LoginUser));
        }

        model.ErrorMessage = "Gecersiz erisim kodu.";
        return View(model);
    }

    // ── Adım 2: Kullanıcı adı + şifre ────────────────────────────────────────────

    private const string PendingTenantKey = "PendingTenant";

    [HttpGet]
    [NonAction]
    public IActionResult LoginUser()
    {
        var pending = TempData.Peek(PendingTenantKey) as string;
        if (string.IsNullOrWhiteSpace(pending))
            return RedirectToAction(nameof(Login));

        var parts = pending.Split('|', 2);
        return View(new AccessLoginUserViewModel
        {
            TenantKey  = parts[0],
            TenantName = parts.Length > 1 ? parts[1] : parts[0]
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [EnableRateLimiting("login")]
    [NonAction]
    public async Task<IActionResult> LoginUser(AccessLoginUserViewModel model, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
            return View(model);

        var userService = HttpContext.RequestServices.GetRequiredService<TenantUserService>();
        var user = await userService.ValidateAsync(model.TenantKey, model.Username.Trim(), model.Password, cancellationToken);

        if (user is null)
        {
            model.ErrorMessage = "Kullanici adi veya sifre yanlis.";
            return View(model);
        }

        if (!user.IsActive)
        {
            model.ErrorMessage = "Bu kullanici hesabi pasif. Yoneticinizle iletisime gecin.";
            return View(model);
        }

        // 2FA aktif mi? Eger oyleyse ikinci faktor sayfasina yonlendir.
        if (user.TotpEnabled)
        {
            TempData[PendingTotpKey] = $"{model.TenantKey}|{model.TenantName}|{user.Username}|{user.DisplayName ?? user.Username}|{user.Role}";
            return RedirectToAction(nameof(LoginTotp));
        }

        await SignInAsync(
            user.DisplayName ?? user.Username,
            StudioRoles.Operator,
            model.TenantKey,
            model.TenantName,
            username: user.Username,
            tenantRole: user.Role);

        return RedirectToAction("Index", "Home");
    }

    // ── Adim 3: TOTP (2FA) ───────────────────────────────────────────────────────

    private const string PendingTotpKey = "PendingTotp";
    private const string PendingAdminTotpKey = "PendingAdminTotp";

    [HttpGet]
    public IActionResult LoginAdminTotp()
    {
        var pending = TempData.Peek(PendingAdminTotpKey) as string;
        if (string.IsNullOrWhiteSpace(pending))
            return RedirectToAction(nameof(Login));

        var parts = pending.Split('|', 2);
        return View("LoginTotp", new AccessLoginTotpViewModel
        {
            Username = parts[0],
            DisplayName = parts.Length > 1 ? parts[1] : parts[0],
            IsAdmin = true
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [EnableRateLimiting("login")]
    public async Task<IActionResult> LoginAdminTotp(AccessLoginTotpViewModel model, CancellationToken cancellationToken)
    {
        var pending = TempData[PendingAdminTotpKey] as string;
        if (string.IsNullOrWhiteSpace(pending))
            return RedirectToAction(nameof(Login));

        if (!ModelState.IsValid)
        {
            TempData[PendingAdminTotpKey] = pending;
            model.IsAdmin = true;
            return View("LoginTotp", model);
        }

        var parts = pending.Split('|', 2);
        var email = parts[0];
        var displayName = parts.Length > 1 ? parts[1] : parts[0];
        var admin = _options.Admins.FirstOrDefault(x =>
            x.IsActive && string.Equals(x.Email, email, StringComparison.OrdinalIgnoreCase));

        if (admin is null || !VerifyAdminTotp(admin, model.Code))
        {
            TempData[PendingAdminTotpKey] = pending;
            model.IsAdmin = true;
            model.DisplayName = displayName;
            model.Username = email;
            model.ErrorMessage = "Dogrulama kodu hatali veya suresi gecmis. Tekrar deneyin.";
            return View("LoginTotp", model);
        }

        await SignInAdminAsync(admin, displayName);
        return RedirectToAction("Index", "Home");
    }

    [HttpGet]
    [NonAction]
    public IActionResult LoginTotp()
    {
        var pending = TempData.Peek(PendingTotpKey) as string;
        if (string.IsNullOrWhiteSpace(pending))
            return RedirectToAction(nameof(Login));

        var parts = pending.Split('|');
        return View(new AccessLoginTotpViewModel
        {
            TenantKey  = parts[0],
            TenantName = parts.Length > 1 ? parts[1] : parts[0],
            Username   = parts.Length > 2 ? parts[2] : string.Empty,
            DisplayName = parts.Length > 3 ? parts[3] : (parts.Length > 2 ? parts[2] : string.Empty)
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [EnableRateLimiting("login")]
    [NonAction]
    public async Task<IActionResult> LoginTotp(AccessLoginTotpViewModel model, CancellationToken cancellationToken)
    {
        var pending = TempData[PendingTotpKey] as string;
        if (string.IsNullOrWhiteSpace(pending))
            return RedirectToAction(nameof(Login));

        if (!ModelState.IsValid)
        {
            TempData[PendingTotpKey] = pending; // tekrar denemek icin sakla
            return View(model);
        }

        var parts = pending.Split('|');
        if (parts.Length < 5)
            return RedirectToAction(nameof(Login));

        var tenantKey   = parts[0];
        var tenantName  = parts[1];
        var username    = parts[2];
        var displayName = parts[3];
        var role        = parts[4];

        var userSvc = HttpContext.RequestServices.GetRequiredService<TenantUserService>();
        var users   = await userSvc.GetUsersAsync(tenantKey, cancellationToken);
        var user    = users.FirstOrDefault(u => string.Equals(u.Username, username, StringComparison.OrdinalIgnoreCase));

        if (user is null || !user.IsActive || !userSvc.VerifyTotp(user, model.Code))
        {
            model.ErrorMessage = "Dogrulama kodu hatali veya suresi gecmis. Tekrar deneyin.";
            TempData[PendingTotpKey] = pending; // hala deneyebilsin
            return View(model);
        }

        await SignInAsync(displayName, StudioRoles.Operator, tenantKey, tenantName, username, role);
        return RedirectToAction("Index", "Home");
    }

    // ── Sifre sifirlama (token tabanli) ──────────────────────────────────────────

    [HttpGet]
    [NonAction]
    public IActionResult ResetPassword()
    {
        if (User.Identity?.IsAuthenticated == true)
            return RedirectToAction("Index", "Home");

        return View(new AccessResetPasswordViewModel());
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [EnableRateLimiting("login")]
    [NonAction]
    public async Task<IActionResult> ResetPassword(AccessResetPasswordViewModel model, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
            return View(model);

        var userService = HttpContext.RequestServices.GetRequiredService<TenantUserService>();
        var ok = await userService.ResetPasswordAsync(
            model.TenantKey.Trim(),
            model.Username.Trim(),
            model.Token.Trim(),
            model.NewPassword,
            cancellationToken);

        if (!ok)
        {
            model.ErrorMessage = "Token gecersiz, suresi dolmus ya da kullanici bulunamadi. Yoneticinizle iletisime gecin.";
            return View(model);
        }

        StudioFlash.Success(TempData, "Sifreniz guncellendi. Simdi yeni sifrenizle giris yapabilirsiniz.");
        return RedirectToAction(nameof(Login));
    }

    [Authorize]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Logout(CancellationToken cancellationToken)
    {
        await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
        return RedirectToAction(nameof(Login));
    }

    // ── Yardımcı metodlar ─────────────────────────────────────────────────────────

    private bool IsAdminCode(string accessCode)
    {
        return !string.IsNullOrWhiteSpace(_options.AdminAccessCode)
               && string.Equals(accessCode, _options.AdminAccessCode, StringComparison.Ordinal);
    }

    private static bool IsAdminTotpConfigured(StudioAdminUser admin) =>
        admin.TotpEnabled && !string.IsNullOrWhiteSpace(admin.TotpSecret);

    private static bool RequiresAdminTotp(StudioAdminUser admin) =>
        IsAdminTotpConfigured(admin);

    private static bool VerifyAdminTotp(StudioAdminUser admin, string code) =>
        IsAdminTotpConfigured(admin) && TotpAuthenticator.Verify(admin.TotpSecret, code);

    private async Task SignInAsync(
        string displayName,
        string role,
        string? workspaceCode,
        string? workspaceName,
        string? username,
        string? tenantRole = null)
    {
        var claims = new List<Claim>
        {
            new(ClaimTypes.Name, displayName),
            new(ClaimTypes.Role, role)
        };
        // Preserve compatibility with existing super-admin checks while keeping Admin separate.
        if (role == StudioRoles.SuperAdmin)
            claims.Add(new Claim(ClaimTypes.Role, StudioRoles.Admin));

        if (workspaceCode is not null)
            claims.Add(new Claim(StudioRoles.WorkspaceCodeClaim, workspaceCode));

        if (workspaceName is not null)
            claims.Add(new Claim(StudioRoles.WorkspaceNameClaim, workspaceName));

        if (username is not null)
            claims.Add(new Claim(StudioRoles.UsernameClaim, username));

        if (!string.IsNullOrWhiteSpace(tenantRole))
            claims.Add(new Claim(StudioRoles.TenantRoleClaim, tenantRole));

        var principal = new ClaimsPrincipal(new ClaimsIdentity(
            claims,
            CookieAuthenticationDefaults.AuthenticationScheme));

        await HttpContext.SignInAsync(
            CookieAuthenticationDefaults.AuthenticationScheme,
            principal,
            new AuthenticationProperties
            {
                IsPersistent = true,
                ExpiresUtc   = DateTimeOffset.UtcNow.AddHours(12)
            });
    }
}

internal record PendingTenantClaim(string TenantKey, string TenantName);
