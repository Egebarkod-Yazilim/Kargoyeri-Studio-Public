using Kargoyeri.Application.Services;
using Kargoyeri.Studio.Core.Infrastructure;
using Kargoyeri.Studio.Core.Models;
using Microsoft.AspNetCore.Mvc;

namespace Kargoyeri.Studio.Core.Controllers;

public sealed class NotificationsController : Controller
{
    private readonly CargoOrchestrator _orchestrator;
    private readonly NotificationService _notificationService;
    private readonly CustomerService _customerService;
    private readonly IStudioWorkspaceContext _workspaceContext;

    public NotificationsController(
        CargoOrchestrator orchestrator,
        NotificationService notificationService,
        CustomerService customerService,
        IStudioWorkspaceContext workspaceContext)
    {
        _orchestrator = orchestrator;
        _notificationService = notificationService;
        _customerService = customerService;
        _workspaceContext = workspaceContext;
    }

    public async Task<IActionResult> Index(
        [FromQuery] NotificationFilterViewModel filter,
        [FromQuery] int page = 1,
        CancellationToken cancellationToken = default)
    {
        const int pageSize = 20;
        var workspace = await _workspaceContext.GetOrCreateAsync(_customerService, cancellationToken);
        var customerProfile = await _customerService.GetProfileAsync(workspace.TenantKey, cancellationToken);
        var allowedProviders = customerProfile?.AllowedProviders.ToHashSet() ?? new HashSet<Kargoyeri.Contracts.Enums.CargoProviderTypeDto>();
        var allowedShipmentReferences = (await _orchestrator.ListShipmentsAsync(workspace.TenantKey, cancellationToken))
            .Where(x => allowedProviders.Contains(x.Provider))
            .Select(x => x.ShipmentReference)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var notifications = await _notificationService.ListAsync(workspace.TenantKey, filter.ShipmentReference, cancellationToken);
        notifications = notifications.Where(x => allowedShipmentReferences.Contains(x.ShipmentReference)).ToArray();

        if (filter.Status.HasValue)
            notifications = notifications.Where(x => x.Status == filter.Status.Value).ToArray();

        if (filter.Channel.HasValue)
            notifications = notifications.Where(x => x.Channel == filter.Channel.Value).ToArray();

        var totalCount = notifications.Count;
        var safePage = Math.Max(1, page);
        var paged = notifications.Skip((safePage - 1) * pageSize).Take(pageSize).ToArray();

        return View(new NotificationCenterViewModel
        {
            WorkspaceCode = workspace.TenantKey,
            WorkspaceName = workspace.Name,
            Filter = filter,
            Notifications = paged,
            Pagination = new PaginationViewModel { Page = safePage, PageSize = pageSize, TotalCount = totalCount }
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Retry(Guid id, CancellationToken cancellationToken)
    {
        var workspace = await _workspaceContext.GetOrCreateAsync(_customerService, cancellationToken);
        var success = await _notificationService.RetryAsync(id, workspace.TenantKey, cancellationToken);

        TempData["StudioMessage"] = success
            ? "Bildirim yeniden gonderildi."
            : "Bildirim yeniden gonderilemedi. Yalnizca basarisiz bildirimler tekrar denenebilir.";

        return RedirectToAction(nameof(Index));
    }
}
