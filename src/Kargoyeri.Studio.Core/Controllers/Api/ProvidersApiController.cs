using Kargoyeri.Application.Services;
using Kargoyeri.Contracts.Enums;
using Kargoyeri.Studio.Core.Infrastructure;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Kargoyeri.Studio.Core.Controllers.Api;

[ApiController]
[Route("api/v1/providers")]
[Produces("application/json")]
[AllowAnonymous]
[EnableRateLimiting("api-tenant")] // P2-#1
public sealed class ProvidersApiController : ControllerBase
{
    private readonly ProviderCatalogService _catalogService;
    private readonly StudioProviderStatusService _statusService;
    private readonly CustomerService _customerService;
    private readonly IStudioWorkspaceContext _workspaceContext;

    public ProvidersApiController(
        ProviderCatalogService catalogService,
        StudioProviderStatusService statusService,
        CustomerService customerService,
        IStudioWorkspaceContext workspaceContext)
    {
        _catalogService = catalogService;
        _statusService = statusService;
        _customerService = customerService;
        _workspaceContext = workspaceContext;
    }

    /// <summary>Tum provider'larin katalog listesini dondurur.</summary>
    [HttpGet]
    public IActionResult List() => Ok(_catalogService.List());

    /// <summary>Workspace bazinda provider durumlarini dondurur.</summary>
    [HttpGet("status")]
    public async Task<IActionResult> Status(
        [FromQuery] string workspace,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(workspace))
            return BadRequest(new { error = "workspace parametresi zorunludur." });

        var statuses = new List<object>();
        foreach (var profile in _catalogService.List())
        {
            if (!Enum.TryParse<CargoProviderTypeDto>(profile.ProviderCode, true, out var provider))
                continue;

            var s = await _statusService.GetAsync(workspace, provider, cancellationToken);
            statuses.Add(new
            {
                provider = profile.ProviderCode,
                configured = s.Configured,
                canCreateShipment = s.CanCreateShipment,
                liveTransportImplemented = s.LiveTransportImplemented,
                simulationEnabled = s.SimulationEnabled,
                integrationMode = s.IntegrationMode,
                missingSettings = s.MissingSettings
            });
        }

        return Ok(statuses);
    }
}
