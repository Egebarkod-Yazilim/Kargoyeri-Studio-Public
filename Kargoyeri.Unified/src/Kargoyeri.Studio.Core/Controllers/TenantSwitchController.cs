using System.Security.Claims;
using Kargoyeri.Application.Services;
using Kargoyeri.Contracts.Dtos;
using Kargoyeri.Contracts.Enums;
using Kargoyeri.Studio.Core.Infrastructure;
using Kargoyeri.Studio.Core.Infrastructure.OrderChannels;
using Kargoyeri.Studio.Core.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace Kargoyeri.Studio.Core.Controllers;

/// <summary>
/// Admin kullanıcısının müşterileri yönetmesini ve geçiş yapmasını sağlar.
/// </summary>
[Authorize(Roles = StudioRoles.Admin)]
public sealed class TenantSwitchController : Controller
{
    private readonly CustomerService _customerService;
    private readonly IStudioWorkspaceContext _workspaceContext;
    private readonly IStudioActivityLogService _activityLog;
    private readonly ILogger<TenantSwitchController> _logger;

    public TenantSwitchController(
        CustomerService customerService,
        IStudioWorkspaceContext workspaceContext,
        IStudioActivityLogService activityLog,
        ILogger<TenantSwitchController> logger)
    {
        _customerService  = customerService;
        _workspaceContext = workspaceContext;
        _activityLog      = activityLog;
        _logger           = logger;
    }

    [HttpGet]
    public async Task<IActionResult> Index([FromQuery] string? search, CancellationToken cancellationToken)
    {
        return View(await BuildViewModelAsync(search, cancellationToken));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Switch([FromForm] string tenantKey, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(tenantKey))
            return RedirectToAction(nameof(Index));

        var profile = await _customerService.GetProfileAsync(tenantKey.Trim(), cancellationToken);
        if (profile is null)
        {
            TempData["StudioMessage"] = $"'{tenantKey}' kodlu musteri bulunamadi.";
            return RedirectToAction(nameof(Index));
        }

        _workspaceContext.Set(profile.TenantKey, profile.Name);
        _workspaceContext.StartImpersonation();

        // P2-#5 — Admin impersonation audit kaydi (semantic + Serilog warning).
        var adminUser = User.FindFirstValue(StudioRoles.UsernameClaim)
                     ?? User.FindFirstValue(ClaimTypes.Name)
                     ?? "unknown";
        var ip = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "";
        _activityLog.Append(new StudioActivityLogEntry(
            Id:            Guid.NewGuid(),
            TenantKey:     profile.TenantKey,
            Username:      adminUser,
            Role:          "Admin",
            Controller:    "TenantSwitch",
            Action:        "Impersonate",
            HttpMethod:    "POST",
            Path:          $"/TenantSwitch/Switch?target={profile.TenantKey}",
            IpAddress:     ip,
            StatusCode:    302,
            OccurredAtUtc: DateTimeOffset.UtcNow));
        _logger.LogWarning("IMPERSONATION: admin={Admin} ip={Ip} began viewing tenant={Tenant} ({Name})",
            adminUser, ip, profile.TenantKey, profile.Name);

        TempData["StudioMessage"] = $"'{profile.Name}' musterisini admin olarak izlemeye basladiniz. Topta uyari banner'i gorulecek.";
        return RedirectToAction("Index", "Home");
    }

    /// <summary>P2-#5 — Aktif impersonation'i sonlandirir, audit kaydi yazar.</summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public IActionResult StopImpersonation(CancellationToken cancellationToken)
    {
        var startedAt = _workspaceContext.GetImpersonationStartedAt();
        var targetKey = _workspaceContext.GetCurrentCode();

        _workspaceContext.StopImpersonation();

        var adminUser = User.FindFirstValue(StudioRoles.UsernameClaim)
                     ?? User.FindFirstValue(ClaimTypes.Name)
                     ?? "unknown";
        var ip = HttpContext.Connection.RemoteIpAddress?.ToString() ?? "";
        _activityLog.Append(new StudioActivityLogEntry(
            Id:            Guid.NewGuid(),
            TenantKey:     targetKey,
            Username:      adminUser,
            Role:          "Admin",
            Controller:    "TenantSwitch",
            Action:        "StopImpersonation",
            HttpMethod:    "POST",
            Path:          $"/TenantSwitch/StopImpersonation?target={targetKey}",
            IpAddress:     ip,
            StatusCode:    302,
            OccurredAtUtc: DateTimeOffset.UtcNow));
        var duration = startedAt is null ? "?" : (DateTimeOffset.UtcNow - startedAt.Value).ToString(@"hh\:mm\:ss");
        _logger.LogWarning("IMPERSONATION END: admin={Admin} ip={Ip} stopped viewing tenant={Tenant} duration={Duration}",
            adminUser, ip, targetKey, duration);

        TempData["StudioMessage"] = "Impersonation sonlandirildi. Admin gorunumune dondunuz.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AddCustomer(TenantSwitchViewModel pageModel, CancellationToken cancellationToken)
    {
        var input = pageModel.NewCustomer;

        if (string.IsNullOrWhiteSpace(input.TenantKey) || string.IsNullOrWhiteSpace(input.Name))
        {
            StudioFlash.Error(TempData, "Musteri kodu ve adi zorunludur.");
            return RedirectToAction(nameof(Index));
        }

        var targets = new List<NotificationTargetDto>
        {
            new() { Channel = NotificationChannelDto.Internal, Address = input.TenantKey.Trim(), IsEnabled = true }
        };
        if (!string.IsNullOrWhiteSpace(input.WebhookUrl))
            targets.Add(new() { Channel = NotificationChannelDto.Webhook, Address = input.WebhookUrl.Trim(), IsEnabled = true });
        if (!string.IsNullOrWhiteSpace(input.Email))
            targets.Add(new() { Channel = NotificationChannelDto.Email, Address = input.Email.Trim(), IsEnabled = true });
        if (!string.IsNullOrWhiteSpace(input.Phone))
            targets.Add(new() { Channel = NotificationChannelDto.Sms, Address = input.Phone.Trim(), IsEnabled = true });

        var metadata = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (!string.IsNullOrWhiteSpace(input.Vkn)) metadata["customer.vkn"] = input.Vkn.Trim();
        if (!string.IsNullOrWhiteSpace(input.Phone)) metadata["customer.phone"] = input.Phone.Trim();

        // Wizard 3. adımında seçilen pazaryerleri / kaynakları enabled flag olarak işaretle
        var allCodes = (input.SelectedChannels ?? new())
            .Concat(input.SelectedExtraSources ?? new())
            .Where(c => !string.IsNullOrWhiteSpace(c))
            .Distinct(StringComparer.OrdinalIgnoreCase);
        foreach (var code in allCodes)
        {
            var descriptor = OrderChannelCatalog.FindByCode(code);
            if (descriptor is null) continue;
            OrderChannelMetadata.Apply(metadata, descriptor.Type,
                enabled: true, defaultProvider: null,
                fieldValues: new Dictionary<string, string>());
        }

        await _customerService.UpsertAsync(input.TenantKey.Trim(), new UpsertCustomerRequest
        {
            TenantKey = input.TenantKey.Trim(),
            Name = input.Name.Trim(),
            IsActive = true,
            AllowedProviders = new List<CargoProviderTypeDto>(),
            NotificationTargets = targets,
            Metadata = metadata
        }, cancellationToken);

        StudioFlash.Success(TempData, $"'{input.Name}' musterisi olusturuldu.");
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ToggleActive([FromForm] string tenantKey, CancellationToken cancellationToken)
    {
        var profile = await _customerService.GetProfileAsync(tenantKey.Trim(), cancellationToken);
        if (profile is null)
        {
            TempData["StudioMessage"] = "Musteri bulunamadi.";
            return RedirectToAction(nameof(Index));
        }

        await _customerService.UpsertAsync(tenantKey, new UpsertCustomerRequest
        {
            TenantKey = profile.TenantKey,
            Name = profile.Name,
            IsActive = !profile.IsActive,
            AllowedProviders = profile.AllowedProviders.ToList(),
            NotificationTargets = profile.NotificationTargets.ToList(),
            Metadata = profile.Metadata
        }, cancellationToken);

        TempData["StudioMessage"] = $"'{profile.Name}' musterisi {(!profile.IsActive ? "aktif" : "pasif")} yapildi.";
        return RedirectToAction(nameof(Index));
    }

    // ── Kullanıcı Yönetimi ───────────────────────────────────────────────────────

    [HttpGet]
    public async Task<IActionResult> UserList([FromQuery] string tenantKey, CancellationToken cancellationToken)
    {
        var userSvc = HttpContext.RequestServices.GetRequiredService<TenantUserService>();
        var users   = await userSvc.GetUsersAsync(tenantKey.Trim(), cancellationToken);
        return PartialView("_UserList", (tenantKey, users));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> AddUser(
        [FromForm] string tenantKey,
        [FromForm] string username,
        [FromForm] string displayName,
        [FromForm] string password,
        [FromForm] string? role,
        [FromForm] string? email,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password))
        {
            TempData["StudioMessage"] = "Kullanici adi ve sifre zorunludur.";
            return RedirectToAction(nameof(Index));
        }

        var userSvc = HttpContext.RequestServices.GetRequiredService<TenantUserService>();
        await userSvc.AddOrUpdateUserAsync(
            tenantKey.Trim(),
            username.Trim(),
            displayName?.Trim() ?? username.Trim(),
            password,
            cancellationToken,
            TenantUserRoles.NormalizeOrDefault(role),
            email);
        TempData["StudioMessage"] = $"'{username}' kullanicisi eklendi/guncellendi ({TenantUserRoles.NormalizeOrDefault(role)}).";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SetUserRole(
        [FromForm] string tenantKey,
        [FromForm] string username,
        [FromForm] string role,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(tenantKey) || string.IsNullOrWhiteSpace(username))
        {
            StudioFlash.Error(TempData, "Musteri kodu ve kullanici adi zorunludur.");
            return RedirectToAction(nameof(Index));
        }

        var userSvc = HttpContext.RequestServices.GetRequiredService<TenantUserService>();
        var normalized = TenantUserRoles.NormalizeOrDefault(role);
        await userSvc.SetRoleAsync(tenantKey.Trim(), username.Trim(), normalized, cancellationToken);
        StudioFlash.Success(TempData, $"'{username}' kullanicisinin rolu '{normalized}' olarak guncellendi.");
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> ToggleUser(
        [FromForm] string tenantKey,
        [FromForm] string username,
        [FromForm] bool isActive,
        CancellationToken cancellationToken)
    {
        var userSvc = HttpContext.RequestServices.GetRequiredService<TenantUserService>();
        await userSvc.SetActiveAsync(tenantKey.Trim(), username.Trim(), isActive, cancellationToken);
        TempData["StudioMessage"] = $"'{username}' kullanicisi {(isActive ? "aktif" : "pasif")} yapildi.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteUser(
        [FromForm] string tenantKey,
        [FromForm] string username,
        CancellationToken cancellationToken)
    {
        var userSvc = HttpContext.RequestServices.GetRequiredService<TenantUserService>();
        await userSvc.DeleteUserAsync(tenantKey.Trim(), username.Trim(), cancellationToken);
        TempData["StudioMessage"] = $"'{username}' kullanicisi silindi.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> IssueResetToken(
        [FromForm] string tenantKey,
        [FromForm] string username,
        CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(tenantKey) || string.IsNullOrWhiteSpace(username))
        {
            StudioFlash.Error(TempData, "Musteri kodu ve kullanici adi zorunludur.");
            return RedirectToAction(nameof(Index));
        }

        var userSvc = HttpContext.RequestServices.GetRequiredService<TenantUserService>();
        var emailer = HttpContext.RequestServices.GetRequiredService<IStudioEmailSender>();

        try
        {
            var token = await userSvc.IssueResetTokenAsync(tenantKey.Trim(), username.Trim(), cancellationToken);

            // Kullanicinin email'i tanimliysa otomatik gonder; aksi halde admin'e goster.
            var users   = await userSvc.GetUsersAsync(tenantKey.Trim(), cancellationToken);
            var user    = users.FirstOrDefault(u => string.Equals(u.Username, username.Trim(), StringComparison.OrdinalIgnoreCase));
            var emailed = false;

            if (user is not null
                && !string.IsNullOrWhiteSpace(user.Email)
                && emailer.IsConfigured)
            {
                var subject = $"Kargoyeri Studio — Sifre Sifirlama Token'iniz";
                var body    =
                    $"Merhaba {user.DisplayName},\r\n\r\n" +
                    $"Hesabiniz icin sifre sifirlama talebi olusturuldu.\r\n\r\n" +
                    $"  Musteri Kodu : {tenantKey}\r\n" +
                    $"  Kullanici    : {user.Username}\r\n" +
                    $"  Token        : {token}\r\n" +
                    $"  Gecerlilik   : 24 saat\r\n\r\n" +
                    $"Giris ekranindaki 'Sifremi Unuttum' baglantisini kullanarak yeni sifrenizi belirleyebilirsiniz.\r\n\r\n" +
                    $"Bu istegi siz baslatmadiysaniz token'i kullanmayin ve yoneticinize haber verin.\r\n\r\n" +
                    $"— Kargoyeri Studio";

                emailed = await emailer.SendAsync(user.Email!, subject, body, cancellationToken);
            }

            if (emailed)
            {
                StudioFlash.Success(TempData,
                    $"'{username}' icin sifre sifirlama token'i {user!.Email} adresine gonderildi (24 saat gecerli).");
            }
            else
            {
                StudioFlash.Warning(TempData,
                    $"'{username}' icin token: {token} (24 saat gecerli). " +
                    (emailer.IsConfigured
                        ? "Kullanicinin email'i tanimsiz — manuel iletmeniz gerekiyor."
                        : "SMTP yapilandirilmamis — manuel iletmeniz gerekiyor.") +
                    " Giris ekranindan 'Sifremi unuttum' baglantisini kullanarak sifre belirleyebilir.");
            }
        }
        catch (InvalidOperationException ex)
        {
            StudioFlash.Error(TempData, ex.Message);
        }
        return RedirectToAction(nameof(Index));
    }

    // ── Kargo Firma Yetkileri ────────────────────────────────────────────────────

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SetAllowedProviders(
        [FromForm] string tenantKey,
        [FromForm] List<CargoProviderTypeDto>? providers,
        CancellationToken cancellationToken)
    {
        var profile = await _customerService.GetProfileAsync(tenantKey.Trim(), cancellationToken);
        if (profile is null)
        {
            TempData["StudioMessage"] = "Musteri bulunamadi.";
            return RedirectToAction(nameof(Index));
        }

        var allowed = (providers ?? new List<CargoProviderTypeDto>())
            .Distinct()
            .ToList();

        await _customerService.UpsertAsync(profile.TenantKey, new UpsertCustomerRequest
        {
            TenantKey           = profile.TenantKey,
            Name                = profile.Name,
            IsActive            = profile.IsActive,
            AllowedProviders    = allowed,
            NotificationTargets = profile.NotificationTargets.ToList(),
            Metadata            = profile.Metadata
        }, cancellationToken);

        StudioFlash.Success(TempData, $"'{profile.Name}' icin {allowed.Count} kargo firmasi yetkisi kaydedildi.");
        return RedirectToAction(nameof(Index));
    }

    // ── Inbound Webhook Secret ───────────────────────────────────────────────────

    /// <summary>
    /// Kargo firmalarinin bizim sistemimize callback POST ettiginde header ile gonderecegi
    /// shared secret'i uretir veya doner. Saklanir: Metadata["webhook.inbound.secret"].
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RotateInboundSecret([FromForm] string tenantKey, CancellationToken cancellationToken)
    {
        var profile = await _customerService.GetProfileAsync(tenantKey.Trim(), cancellationToken);
        if (profile is null) return NotFound();

        var secret = Convert.ToHexString(System.Security.Cryptography.RandomNumberGenerator.GetBytes(24));
        var metadata = new Dictionary<string, string>(profile.Metadata, StringComparer.OrdinalIgnoreCase)
        {
            ["webhook.inbound.secret"] = secret
        };

        await _customerService.UpsertAsync(tenantKey, new UpsertCustomerRequest
        {
            TenantKey           = profile.TenantKey,
            Name                = profile.Name,
            IsActive            = profile.IsActive,
            AllowedProviders    = profile.AllowedProviders.ToList(),
            NotificationTargets = profile.NotificationTargets.ToList(),
            Metadata            = metadata
        }, cancellationToken);

        StudioFlash.Success(TempData,
            $"Inbound webhook secret yenilendi: {secret} — Bu degeri kargo firmalariyla paylasin. " +
            $"Callback URL: /api/webhook/{{provider}}/{profile.TenantKey} — Header: X-Kargoyeri-Inbound-Secret.");
        return RedirectToAction(nameof(Index));
    }

    // ── Lisans Yönetimi ──────────────────────────────────────────────────────────

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SetLicense(LicenseInput input, CancellationToken cancellationToken)
    {
        var profile = await _customerService.GetProfileAsync(input.TenantKey.Trim(), cancellationToken);
        if (profile is null)
            return NotFound();

        if (!DateTimeOffset.TryParse(input.ExpiresAt, out var expiresAt))
        {
            TempData["StudioMessage"] = "Gecersiz tarih formati. gg.aa.yyyy veya yyyy-aa-gg kullanin.";
            return RedirectToAction(nameof(Index));
        }

        var code = string.IsNullOrWhiteSpace(input.LicenseCode)
            ? LicenseHelper.GenerateCode()
            : input.LicenseCode.Trim();

        var metadata = LicenseHelper.Apply(profile.Metadata, code, expiresAt, input.Plan ?? "custom");

        await _customerService.UpsertAsync(input.TenantKey, new UpsertCustomerRequest
        {
            TenantKey          = profile.TenantKey,
            Name               = profile.Name,
            IsActive           = profile.IsActive,
            AllowedProviders   = profile.AllowedProviders.ToList(),
            NotificationTargets = profile.NotificationTargets.ToList(),
            Metadata           = metadata
        }, cancellationToken);

        StudioFlash.Success(TempData, $"Lisans guncellendi. Erisim kodu: {code}");
        return RedirectToAction(nameof(Index));
    }

    /// <summary>
    /// Mevcut lisans kodunu yeniden uretir — suresi ve plani korur, ama kod degisir.
    /// Sizdirilma suphesi olan lisanslari gecersiz kilmak icin kullanilir.
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RotateLicense([FromForm] string tenantKey, CancellationToken cancellationToken)
    {
        var profile = await _customerService.GetProfileAsync(tenantKey.Trim(), cancellationToken);
        if (profile is null) return NotFound();

        var current = LicenseHelper.Read(profile.Metadata);
        if (!current.HasLicense || current.ExpiresAt is null)
        {
            StudioFlash.Warning(TempData, "Bu musterinin aktif lisansi yok. Once 'Lisans Ayarla' ile tanimlayin.");
            return RedirectToAction(nameof(Index));
        }

        var newCode  = LicenseHelper.GenerateCode();
        var metadata = LicenseHelper.Apply(profile.Metadata, newCode, current.ExpiresAt.Value, current.Plan ?? "custom");

        await _customerService.UpsertAsync(tenantKey, new UpsertCustomerRequest
        {
            TenantKey           = profile.TenantKey,
            Name                = profile.Name,
            IsActive            = profile.IsActive,
            AllowedProviders    = profile.AllowedProviders.ToList(),
            NotificationTargets = profile.NotificationTargets.ToList(),
            Metadata            = metadata
        }, cancellationToken);

        StudioFlash.Success(TempData, $"Lisans kodu yenilendi. Yeni erisim kodu: {newCode} — eski kod artik gecersiz.");
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RevokeLicense([FromForm] string tenantKey, CancellationToken cancellationToken)
    {
        var profile = await _customerService.GetProfileAsync(tenantKey.Trim(), cancellationToken);
        if (profile is null) return NotFound();

        var metadata = new Dictionary<string, string>(profile.Metadata, StringComparer.OrdinalIgnoreCase);
        metadata.Remove("license.code");
        metadata.Remove("license.expiresAt");
        metadata.Remove("license.plan");
        metadata.Remove("license.createdAt");

        await _customerService.UpsertAsync(tenantKey, new UpsertCustomerRequest
        {
            TenantKey           = profile.TenantKey,
            Name                = profile.Name,
            IsActive            = profile.IsActive,
            AllowedProviders    = profile.AllowedProviders.ToList(),
            NotificationTargets = profile.NotificationTargets.ToList(),
            Metadata            = metadata
        }, cancellationToken);

        StudioFlash.Warning(TempData, "Lisans iptal edildi.");
        return RedirectToAction(nameof(Index));
    }

    private async Task<TenantSwitchViewModel> BuildViewModelAsync(string? search, CancellationToken ct)
    {
        var current = _workspaceContext.GetCurrentCode();
        var all     = await _customerService.ListAllAsync(ct);
        var query   = search?.Trim();

        if (!string.IsNullOrWhiteSpace(query))
        {
            all = all
                .Where(p =>
                    p.TenantKey.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                    p.Name.Contains(query, StringComparison.OrdinalIgnoreCase) ||
                    p.NotificationTargets.Any(t =>
                        t.Address.Contains(query, StringComparison.OrdinalIgnoreCase)))
                .ToArray();
        }

        return new TenantSwitchViewModel
        {
            CurrentTenantKey = current,
            Search           = query,
            Tenants = all.Select(p => new TenantListItemViewModel
            {
                TenantKey      = p.TenantKey,
                Name           = p.Name,
                IsActive       = p.IsActive,
                IsCurrent      = string.Equals(p.TenantKey, current, StringComparison.OrdinalIgnoreCase),
                ProviderCount  = p.AllowedProviders.Count,
                WebhookAddress = p.NotificationTargets.FirstOrDefault(x => x.Channel == NotificationChannelDto.Webhook)?.Address,
                EmailAddress   = p.NotificationTargets.FirstOrDefault(x => x.Channel == NotificationChannelDto.Email)?.Address,
                UpdatedAtUtc   = p.UpdatedAtUtc,
                License   = LicenseHelper.Read(p.Metadata),
                UserCount = TenantUserService.CountUsers(p.Metadata),
                AllowedProviders = p.AllowedProviders.ToArray()
            }).ToArray()
        };
    }
}
