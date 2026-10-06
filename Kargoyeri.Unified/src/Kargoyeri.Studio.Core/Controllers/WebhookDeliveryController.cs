using Kargoyeri.Application.Services;
using Kargoyeri.Contracts.Enums;
using Kargoyeri.Studio.Core.Infrastructure;
using Kargoyeri.Studio.Core.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Kargoyeri.Studio.Core.Controllers;

/// <summary>
/// P2-#3 — Outbound Webhook Delivery Dashboard.
/// Workspace bazinda son N webhook bildirimini gosterir; her satirda
/// otomatik retry kuyrugundaki deneme sayisi + sonraki deneme zamani gorunur.
/// </summary>
[Authorize]
[Route("webhook-delivery")]
public sealed class WebhookDeliveryController : Controller
{
    private const int MaxRecent = 100;

    private readonly NotificationService _notificationService;
    private readonly OutboundWebhookRetryQueue _retryQueue;
    private readonly CustomerService _customerService;
    private readonly IStudioWorkspaceContext _workspace;

    public WebhookDeliveryController(
        NotificationService notificationService,
        OutboundWebhookRetryQueue retryQueue,
        CustomerService customerService,
        IStudioWorkspaceContext workspace)
    {
        _notificationService = notificationService;
        _retryQueue          = retryQueue;
        _customerService     = customerService;
        _workspace           = workspace;
    }

    [HttpGet("")]
    public async Task<IActionResult> Index(CancellationToken ct)
    {
        var workspace = await _workspace.GetOrCreateAsync(_customerService, ct);
        var notifications = await _notificationService.ListAsync(workspace.TenantKey, shipmentReference: null, ct);

        var rows = notifications
            .Where(n => n.Channel == NotificationChannelDto.Webhook)
            .OrderByDescending(n => n.CreatedAtUtc)
            .Take(MaxRecent)
            .Select(n =>
            {
                var s = _retryQueue.TryGet(n.Id);
                return new WebhookDeliveryRow
                {
                    NotificationId    = n.Id,
                    TenantKey         = n.CustomerCode,
                    ShipmentReference = n.ShipmentReference,
                    Channel           = n.Channel,
                    Status            = n.Status,
                    EventType         = n.EventType,
                    Address           = n.Address,
                    CreatedAtUtc      = n.CreatedAtUtc,
                    ErrorMessage      = n.ErrorMessage,
                    Attempts          = s?.Attempts ?? 0,
                    NextAttemptUtc    = s is null || s.Abandoned ? null : s.NextAttemptUtc,
                    Abandoned         = s?.Abandoned ?? false
                };
            })
            .ToArray();

        var webhookOnly = notifications.Where(n => n.Channel == NotificationChannelDto.Webhook).ToArray();
        var failed      = webhookOnly.Where(n => n.Status == NotificationDeliveryStatusDto.Failed).ToList();

        // Retry pending = failed && state mevcut && abandoned değil
        var pending = failed.Count(n =>
        {
            var s = _retryQueue.TryGet(n.Id);
            return s is not null && !s.Abandoned;
        });
        var abandoned = failed.Count(n =>
        {
            var s = _retryQueue.TryGet(n.Id);
            return s is not null && s.Abandoned;
        });

        return View(new WebhookDeliveryPageViewModel
        {
            TenantKey         = workspace.TenantKey,
            TenantName        = workspace.Name,
            TotalCount        = webhookOnly.Length,
            DeliveredCount    = webhookOnly.Count(n => n.Status == NotificationDeliveryStatusDto.Delivered),
            FailedCount       = failed.Count,
            QueuedCount       = webhookOnly.Count(n => n.Status == NotificationDeliveryStatusDto.Queued),
            RetryPendingCount = pending,
            AbandonedCount    = abandoned,
            Recent            = rows
        });
    }

    /// <summary>Manuel "simdi yeniden dene" — retry zamanlamasini sifirlar.</summary>
    [HttpPost("retry-now")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> RetryNow(Guid id, CancellationToken ct)
    {
        var workspace = await _workspace.GetOrCreateAsync(_customerService, ct);
        var ok = await _notificationService.RetryAsync(id, workspace.TenantKey, ct);
        _retryQueue.RecordAttempt(id, success: ok, error: ok ? null : "Manual retry returned false");

        TempData["StudioMessage"] = ok
            ? "Webhook yeniden gonderildi."
            : "Yeniden gonderim basarisiz oldu. Yalnizca Failed durumundaki bildirimler retry edilebilir.";
        return RedirectToAction(nameof(Index));
    }
}
