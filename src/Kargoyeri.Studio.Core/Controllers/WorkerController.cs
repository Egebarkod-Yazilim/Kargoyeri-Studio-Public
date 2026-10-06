using Kargoyeri.Application.Services;
using Kargoyeri.Contracts.Enums;
using Kargoyeri.Studio.Core.Infrastructure;
using Kargoyeri.Studio.Core.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Kargoyeri.Studio.Core.Controllers;

[Authorize(Roles = StudioRoles.SuperAdmin)]
public sealed class WorkerController : Controller
{
    private readonly WorkerStatusTracker _workerTracker;
    private readonly CustomerService _customerService;
    private readonly ProviderCatalogService _providerCatalogService;
    private readonly CargoOrchestrator _orchestrator;
    private readonly NotificationService _notificationService;
    private readonly IStudioWorkspaceContext _workspaceContext;

    public WorkerController(
        WorkerStatusTracker workerTracker,
        CustomerService customerService,
        ProviderCatalogService providerCatalogService,
        CargoOrchestrator orchestrator,
        NotificationService notificationService,
        IStudioWorkspaceContext workspaceContext)
    {
        _workerTracker = workerTracker;
        _customerService = customerService;
        _providerCatalogService = providerCatalogService;
        _orchestrator = orchestrator;
        _notificationService = notificationService;
        _workspaceContext = workspaceContext;
    }

    [HttpGet]
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        var workspace = await _workspaceContext.GetOrCreateAsync(_customerService, cancellationToken);
        var profile = await _customerService.GetProfileAsync(workspace.TenantKey, cancellationToken);
        var allowedProviders = profile?.AllowedProviders.ToHashSet() ?? new HashSet<CargoProviderTypeDto>();

        var providerCount = _providerCatalogService.List()
            .Count(x => Enum.TryParse<CargoProviderTypeDto>(x.ProviderCode, true, out var provider) && allowedProviders.Contains(provider));

        var shipments = (await _orchestrator.ListShipmentsAsync(workspace.TenantKey, cancellationToken))
            .Where(x => allowedProviders.Contains(x.Provider))
            .OrderByDescending(x => x.UpdatedAtUtc)
            .ToArray();
        var shipmentReferences = shipments
            .Select(x => x.ShipmentReference)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var notifications = (await _notificationService.ListAsync(workspace.TenantKey, null, cancellationToken))
            .Where(x => shipmentReferences.Contains(x.ShipmentReference))
            .OrderByDescending(x => x.CreatedAtUtc)
            .ToArray();

        return View(new WorkerDetailViewModel
        {
            WorkspaceCode = workspace.TenantKey,
            WorkspaceName = workspace.Name,
            WorkerStatus = _workerTracker.Last,
            ReadyProviderCount = providerCount,
            ShipmentCount = shipments.Length,
            NotificationCount = notifications.Length,
            RecentShipments = shipments.Take(8).ToArray(),
            RecentNotifications = notifications.Take(8).ToArray()
        });
    }
}
