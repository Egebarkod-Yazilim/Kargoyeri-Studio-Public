using Kargoyeri.Studio.Core.Infrastructure;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Kargoyeri.Studio.Core.Controllers;

/// <summary>
/// P4-#2 — Admin gozetimindeki aylik fatura listesi.
/// </summary>
[Authorize(Roles = StudioRoles.SuperAdmin)]
[Route("billing")]
public class BillingController : Controller
{
    private readonly BillingService _billing;
    private readonly TenantMetricsAggregator _metrics;

    public BillingController(BillingService billing, TenantMetricsAggregator metrics)
    {
        _billing = billing;
        _metrics = metrics;
    }

    [HttpGet("")]
    public async Task<IActionResult> Index(int? year, int? month, CancellationToken ct)
    {
        var now = DateTime.UtcNow;
        var y = year ?? now.Year;
        var m = month ?? now.Month;

        var invoices = await _billing.ComputeMonthAsync(y, m, ct);

        ViewData["Title"] = $"Faturalar — {y}-{m:00}";
        ViewData["Year"] = y;
        ViewData["Month"] = m;
        ViewData["Tier"] = _billing.Tier;
        ViewData["PersistedDates"] = _metrics.ListPersistedDates();

        return View(invoices);
    }

    [HttpGet("invoice/{tenantKey}")]
    public async Task<IActionResult> Invoice(string tenantKey, int year, int month, CancellationToken ct)
    {
        var all = await _billing.ComputeMonthAsync(year, month, ct);
        var inv = all.FirstOrDefault(i => string.Equals(i.TenantKey, tenantKey, StringComparison.OrdinalIgnoreCase));
        if (inv is null) return NotFound();

        ViewData["Title"] = $"Fatura — {inv.TenantName} — {year}-{month:00}";
        ViewData["Tier"] = _billing.Tier;
        return View(inv);
    }
}
