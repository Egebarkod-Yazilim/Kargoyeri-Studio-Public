using Kargoyeri.Application.Services;
using Kargoyeri.Contracts.Enums;
using Kargoyeri.Studio.Core.Infrastructure;
using Kargoyeri.Studio.Core.Models;
using Microsoft.AspNetCore.Mvc;

namespace Kargoyeri.Studio.Core.Controllers;

public sealed class MobileController : Controller
{
    private readonly CustomerService _customerService;
    private readonly IStudioWorkspaceContext _workspaceContext;
    private readonly CargoOrchestrator _orchestrator;
    private readonly ProviderHealthMonitor _providerHealthMonitor;

    public MobileController(
        CustomerService customerService,
        IStudioWorkspaceContext workspaceContext,
        CargoOrchestrator orchestrator,
        ProviderHealthMonitor providerHealthMonitor)
    {
        _customerService = customerService;
        _workspaceContext = workspaceContext;
        _orchestrator = orchestrator;
        _providerHealthMonitor = providerHealthMonitor;
    }

    [HttpGet("/mobile")]
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        var workspace = await _workspaceContext.GetOrCreateAsync(_customerService, cancellationToken);
        var profile = await _customerService.GetProfileAsync(workspace.TenantKey, cancellationToken);
        var shipments = await _orchestrator.ListShipmentsAsync(workspace.TenantKey, cancellationToken);
        var deliveredToday = shipments.Count(x =>
            x.Status == ShipmentStatusDto.Delivered &&
            x.UpdatedAtUtc.ToLocalTime().Date == DateTimeOffset.Now.Date);
        var activeShipments = shipments.Count(x => x.Status is ShipmentStatusDto.InTransit or ShipmentStatusDto.ProviderAccepted or ShipmentStatusDto.LabelReady);
        var alertingShipments = shipments.Count(x =>
            x.Status == ShipmentStatusDto.InTransit &&
            (DateTimeOffset.UtcNow - x.UpdatedAtUtc).TotalHours >= 48);

        var providers = profile?.AllowedProviders ?? new List<CargoProviderTypeDto>();
        var providerHealth = _providerHealthMonitor
            .SummarizeTenant(workspace.TenantKey, providers, TimeSpan.FromHours(24))
            .OrderByDescending(x => x.ErrorRate)
            .Take(3)
            .Select(x => new ProviderHealthSnapshotViewModel
            {
                Provider = x.Provider,
                ProviderName = x.Provider.ToString(),
                ProbeCount = x.ProbeCount,
                SuccessCount = x.SuccessCount,
                FailureCount = x.FailureCount,
                SuccessRate = x.SuccessRate,
                ErrorRate = x.ErrorRate,
                P95LatencyMs = x.P95LatencyMs,
                LastCheckedAtUtc = x.LastCheckedAtUtc,
                LastError = x.LastError
            })
            .ToArray();

        return View(new MobileOpsPageViewModel
        {
            WorkspaceCode = workspace.TenantKey,
            WorkspaceName = workspace.Name,
            ActiveShipmentCount = activeShipments,
            DeliveredTodayCount = deliveredToday,
            AlertingShipmentCount = alertingShipments,
            RecentShipments = shipments.OrderByDescending(x => x.UpdatedAtUtc).Take(8).ToArray(),
            ProviderHealth = providerHealth
        });
    }
}
