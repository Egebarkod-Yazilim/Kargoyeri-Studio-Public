using Kargoyeri.Application.Services;
using Kargoyeri.Contracts.Enums;
using Kargoyeri.Studio.Core.Infrastructure;
using Kargoyeri.Studio.Core.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Logging;

namespace Kargoyeri.Studio.Core.Controllers;

/// <summary>
/// Kurye/pickup randevu yonetimi. Operasyon ekibi musteriden gonderi alimi
/// (kurye cagrisi) icin randevu olusturur, durumu izler.
/// </summary>
[Authorize]
public sealed class PickupController : Controller
{
    private readonly PickupRequestService _pickups;
    private readonly CustomerService _customers;
    private readonly IStudioWorkspaceContext _workspace;
    private readonly IStudioEmailSender _emailSender;
    private readonly ILogger<PickupController> _logger;

    public PickupController(
        PickupRequestService pickups,
        CustomerService customers,
        IStudioWorkspaceContext workspace,
        IStudioEmailSender emailSender,
        ILogger<PickupController> logger)
    {
        _pickups = pickups;
        _customers = customers;
        _workspace = workspace;
        _emailSender = emailSender;
        _logger = logger;
    }

    // ── Liste ──────────────────────────────────────────────────────────────────

    [HttpGet]
    public async Task<IActionResult> Index(string? tenantKey, string? status, CancellationToken ct)
    {
        var (resolvedTenant, tenantName, isAdmin) = await ResolveTenantAsync(tenantKey, ct);
        if (string.IsNullOrWhiteSpace(resolvedTenant))
            return RedirectToAction("Index", "Home");

        var items = await _pickups.GetAllAsync(resolvedTenant, ct);
        if (!string.IsNullOrWhiteSpace(status))
            items = items.Where(p => string.Equals(p.Status, status, StringComparison.OrdinalIgnoreCase)).ToList();

        var tenants = isAdmin
            ? (await _customers.ListAllAsync(ct))
                .OrderBy(c => c.Name)
                .Select(c => (c.TenantKey, c.Name))
                .ToList()
            : new List<(string, string)>();

        return View(new PickupIndexViewModel
        {
            TenantKey = resolvedTenant,
            TenantName = tenantName,
            Items = items,
            Tenants = tenants,
            IsAdmin = isAdmin,
            StatusFilter = status
        });
    }

    // ── Yeni randevu formu ─────────────────────────────────────────────────────

    [HttpGet]
    [Authorize(Policy = StudioRoles.CanWrite)]
    public async Task<IActionResult> Create(string? tenantKey, CancellationToken ct)
    {
        var (resolvedTenant, tenantName, _) = await ResolveTenantAsync(tenantKey, ct);
        if (string.IsNullOrWhiteSpace(resolvedTenant))
            return RedirectToAction(nameof(Index));

        var profile = await _customers.GetProfileAsync(resolvedTenant, ct);
        var allowed = profile?.AllowedProviders.Select(a => a.ToString()).ToList()
                      ?? Enum.GetNames(typeof(CargoProviderTypeDto)).ToList();

        return View(new PickupCreateViewModel
        {
            TenantKey = resolvedTenant,
            TenantName = tenantName,
            AvailableProviders = allowed
        });
    }

    [HttpPost]
    [Authorize(Policy = StudioRoles.CanWrite)]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Create(PickupCreateViewModel model, CancellationToken ct)
    {
        if (!ModelState.IsValid)
        {
            var profile = await _customers.GetProfileAsync(model.TenantKey, ct);
            model.AvailableProviders = profile?.AllowedProviders.Select(a => a.ToString()).ToList()
                                        ?? Enum.GetNames(typeof(CargoProviderTypeDto)).ToList();
            return View(model);
        }

        var draft = new PickupRequest(
            Id:                "",
            ProviderType:      model.ProviderType.Trim(),
            PickupDate:        model.PickupDate.ToString("yyyy-MM-dd"),
            TimeWindow:        model.TimeWindow.Trim(),
            ContactName:       model.ContactName.Trim(),
            ContactPhone:      model.ContactPhone.Trim(),
            AddressLine:       model.AddressLine.Trim(),
            City:              model.City.Trim(),
            District:          model.District.Trim(),
            ParcelCount:       model.ParcelCount,
            TotalWeightKg:     model.TotalWeightKg,
            Notes:             string.IsNullOrWhiteSpace(model.Notes) ? null : model.Notes.Trim(),
            Status:            PickupStatus.Draft,
            ConfirmationCode:  null,
            CreatedBy:         User.Identity?.Name ?? "system",
            CreatedAtUtc:      DateTimeOffset.UtcNow);

        var created = await _pickups.CreateAsync(model.TenantKey, draft, draft.CreatedBy, ct);
        _logger.LogInformation("Pickup olusturuldu: tenant={Tenant} provider={Provider} id={Id}",
            model.TenantKey, created.ProviderType, created.Id);

        StudioFlash.Success(TempData, $"Kurye randevusu olusturuldu (taslak). #{created.Id[..8]}");
        return RedirectToAction(nameof(Index), new { tenantKey = model.TenantKey });
    }

    // ── Durum gecisleri ────────────────────────────────────────────────────────

    [HttpPost]
    [Authorize(Policy = StudioRoles.CanWrite)]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> MarkRequested(string tenantKey, string id, string? confirmationCode, CancellationToken ct)
    {
        var ok = await _pickups.MarkRequestedAsync(tenantKey, id, confirmationCode, ct);
        if (ok) StudioFlash.Success(TempData, "Randevu kargo firmasina iletildi olarak isaretlendi.");
        else    StudioFlash.Error(TempData, "Randevu bulunamadi.");
        return RedirectToAction(nameof(Index), new { tenantKey });
    }

    [HttpPost]
    [Authorize(Policy = StudioRoles.CanWrite)]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> MarkConfirmed(string tenantKey, string id, string? confirmationCode, CancellationToken ct)
    {
        var ok = await _pickups.MarkConfirmedAsync(tenantKey, id, confirmationCode, ct);
        if (ok) StudioFlash.Success(TempData, "Randevu konfirme edildi.");
        else    StudioFlash.Error(TempData, "Randevu bulunamadi.");
        return RedirectToAction(nameof(Index), new { tenantKey });
    }

    [HttpPost]
    [Authorize(Policy = StudioRoles.CanWrite)]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> MarkCompleted(string tenantKey, string id, CancellationToken ct)
    {
        var ok = await _pickups.MarkCompletedAsync(tenantKey, id, ct);
        if (ok) StudioFlash.Success(TempData, "Kurye geldi, paketler teslim alindi.");
        else    StudioFlash.Error(TempData, "Randevu bulunamadi.");
        return RedirectToAction(nameof(Index), new { tenantKey });
    }

    [HttpPost]
    [Authorize(Policy = StudioRoles.CanWrite)]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Cancel(string tenantKey, string id, string? reason, CancellationToken ct)
    {
        var ok = await _pickups.CancelAsync(tenantKey, id, reason, ct);
        if (ok) StudioFlash.Success(TempData, "Randevu iptal edildi.");
        else    StudioFlash.Error(TempData, "Randevu bulunamadi.");
        return RedirectToAction(nameof(Index), new { tenantKey });
    }

    [HttpPost]
    [Authorize(Policy = StudioRoles.CanWrite)]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> DeleteDraft(string tenantKey, string id, CancellationToken ct)
    {
        var ok = await _pickups.DeleteDraftAsync(tenantKey, id, ct);
        if (ok) StudioFlash.Success(TempData, "Taslak silindi.");
        else    StudioFlash.Error(TempData, "Sadece taslak durumundaki randevular silinebilir.");
        return RedirectToAction(nameof(Index), new { tenantKey });
    }

    // ── Yardimcilar ────────────────────────────────────────────────────────────

    private async Task<(string TenantKey, string TenantName, bool IsAdmin)> ResolveTenantAsync(
        string? requested, CancellationToken ct)
    {
        var isAdmin = User.IsInRole(StudioRoles.Admin);

        // Operator/tenant kullanicisi: workspace claim'inden cek
        if (!isAdmin)
        {
            var ws = await _workspace.GetOrCreateAsync(_customers, ct);
            return (ws.TenantKey, ws.Name, false);
        }

        // Admin: querystring'den, yoksa ilk aktif tenant
        if (!string.IsNullOrWhiteSpace(requested))
        {
            var profile = await _customers.GetProfileAsync(requested, ct);
            if (profile is not null)
                return (profile.TenantKey, profile.Name, true);
        }

        var firstActive = (await _customers.ListAllAsync(ct))
            .FirstOrDefault(c => c.IsActive);
        return firstActive is null
            ? (string.Empty, string.Empty, true)
            : (firstActive.TenantKey, firstActive.Name, true);
    }
}
