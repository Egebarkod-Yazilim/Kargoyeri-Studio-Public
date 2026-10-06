using Kargoyeri.Application.Services;
using Kargoyeri.Domain.Enums;
using Kargoyeri.Studio.Core.Infrastructure;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Kargoyeri.Studio.Core.Controllers;

/// <summary>
/// P5-#6 — Provider canli sertifikasyon paneli.
///
/// Adminin secili tenant + provider icin uctan uca sertifikasyon
/// (auth/create/refresh/cancel) calistirmasini saglar. Sonuclar
/// ContentRoot/provider-cert/ altinda JSON olarak saklanir.
/// </summary>
[Authorize(Roles = StudioRoles.SuperAdmin)]
[Route("admin/cert")]
public sealed class ProviderCertificationController : Controller
{
    private readonly ProviderCertificationRunner _runner;
    private readonly CustomerService _customers;

    public ProviderCertificationController(ProviderCertificationRunner runner, CustomerService customers)
    {
        _runner = runner;
        _customers = customers;
    }

    [HttpGet("")]
    public async Task<IActionResult> Index(CancellationToken ct)
    {
        ViewData["Title"] = "Provider Sertifikasyon";
        var tenants = await _customers.ListAllAsync(ct);
        ViewData["Tenants"] = tenants.ToList();
        ViewData["Providers"] = Enum.GetValues<CargoProviderType>().ToList();
        ViewData["Recent"] = _runner.ListRecent();
        return View();
    }

    [HttpPost("run")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Run(string tenantKey, string provider, bool autoCancel, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(tenantKey) || !Enum.TryParse<CargoProviderType>(provider, true, out var providerType))
        {
            return BadRequest("Tenant veya provider gecersiz.");
        }

        var report = await _runner.RunAsync(
            tenantKey,
            providerType,
            new CertificationOptions { AutoCancelTestShipment = autoCancel },
            ct);

        return Json(report);
    }

    [HttpGet("recent")]
    public IActionResult Recent(int days = 7) => Json(_runner.ListRecent(days));
}
