using Kargoyeri.Application.Services;
using Kargoyeri.Studio.Core.Infrastructure;
using Kargoyeri.Studio.Core.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Kargoyeri.Studio.Core.Controllers;

/// <summary>
/// Provider Saglik Panosu (P1-#8) — son 24 saat / 1 saat icindeki probe sonuclarinin
/// success rate, p95 latency ve son hata bilgisini tenant bazinda gosterir.
///
/// Veri kaynagi: ProviderHealthMonitor — ProviderHealthProbeService tarafindan
/// her N dakikada bir doldurulur.
/// </summary>
[Authorize]
[Route("provider-health")]
public sealed class ProviderHealthController : Controller
{
    private readonly ProviderHealthMonitor _monitor;
    private readonly CustomerService _customerService;
    private readonly IStudioWorkspaceContext _workspace;

    public ProviderHealthController(
        ProviderHealthMonitor monitor,
        CustomerService customerService,
        IStudioWorkspaceContext workspace)
    {
        _monitor         = monitor;
        _customerService = customerService;
        _workspace       = workspace;
    }

    [HttpGet("")]
    public async Task<IActionResult> Index([FromQuery] string? window, CancellationToken ct)
    {
        var span = ParseWindow(window);
        var tenantKey = _workspace.GetCurrentCode();
        var profile = await _customerService.GetProfileAsync(tenantKey, ct);

        var providers = profile?.AllowedProviders.ToArray()
                        ?? Array.Empty<Kargoyeri.Contracts.Enums.CargoProviderTypeDto>();

        var summaries = _monitor.SummarizeTenant(tenantKey, providers, span);

        return View(new ProviderHealthPageViewModel
        {
            TenantKey  = tenantKey,
            TenantName = profile?.Name ?? tenantKey,
            Window     = span,
            WindowText = (window ?? "24h").ToLowerInvariant(),
            Summaries  = summaries
        });
    }

    private static TimeSpan ParseWindow(string? window) => (window ?? "24h").ToLowerInvariant() switch
    {
        "1h"  => TimeSpan.FromHours(1),
        "6h"  => TimeSpan.FromHours(6),
        "7d"  => TimeSpan.FromDays(7),
        _     => TimeSpan.FromHours(24),
    };
}
