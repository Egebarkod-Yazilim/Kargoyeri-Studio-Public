using Kargoyeri.Application.Services;
using Kargoyeri.Contracts.Enums;
using Kargoyeri.Studio.Core.Infrastructure;

namespace Kargoyeri.Studio.Core.Services;

/// <summary>
/// P2-#3 — Failed (basarisiz) webhook bildirimlerini exponential backoff ile
/// otomatik olarak yeniden dener. NotificationService.RetryAsync'i cagirir.
/// Schedule (OutboundWebhookRetryQueue.BackoffSchedule):
///   1m, 5m, 15m, 1h, 6h, 24h — 6 deneme sonra terkedilir.
/// </summary>
internal sealed class OutboundWebhookRetryService : BackgroundService
{
    private static readonly TimeSpan PollInterval = TimeSpan.FromSeconds(60);

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly OutboundWebhookRetryQueue _queue;
    private readonly ILogger<OutboundWebhookRetryService> _logger;

    public OutboundWebhookRetryService(
        IServiceScopeFactory scopeFactory,
        OutboundWebhookRetryQueue queue,
        ILogger<OutboundWebhookRetryService> logger)
    {
        _scopeFactory = scopeFactory;
        _queue        = queue;
        _logger       = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("OutboundWebhookRetryService baslatildi. Schedule: {Schedule}",
            string.Join(", ", OutboundWebhookRetryQueue.BackoffSchedule.Select(t => t.ToString("g"))));

        try { await Task.Delay(TimeSpan.FromSeconds(45), stoppingToken); }
        catch (OperationCanceledException) { return; }

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await RunCycleAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested) { break; }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Webhook retry dongusu hata. 5 dakika sonra yeniden.");
                try { await Task.Delay(TimeSpan.FromMinutes(5), stoppingToken); }
                catch (OperationCanceledException) { break; }
                continue;
            }

            try { await Task.Delay(PollInterval, stoppingToken); }
            catch (OperationCanceledException) { break; }
        }

        _logger.LogInformation("OutboundWebhookRetryService durduruldu.");
    }

    private async Task RunCycleAsync(CancellationToken ct)
    {
        using var scope = _scopeFactory.CreateScope();
        var customerSvc     = scope.ServiceProvider.GetRequiredService<CustomerService>();
        var notificationSvc = scope.ServiceProvider.GetRequiredService<NotificationService>();

        var tenants = await customerSvc.ListAllAsync(ct);

        // 1) Yeni Failed bildirimleri kuyruga al.
        foreach (var tenant in tenants)
        {
            ct.ThrowIfCancellationRequested();
            var notifications = await notificationSvc.ListAsync(tenant.TenantKey, shipmentReference: null, ct);
            foreach (var n in notifications)
            {
                if (n.Status != NotificationDeliveryStatusDto.Failed) continue;
                if (_queue.TryGet(n.Id) is not null) continue;
                _queue.TrackFailure(n.Id, tenant.TenantKey, n.CreatedAtUtc, n.ErrorMessage);
            }
        }

        // 2) Suresi gelmis denemeleri tetikle.
        var due = _queue.ListDue(DateTimeOffset.UtcNow);
        if (due.Count == 0) return;

        _logger.LogInformation("Webhook retry: {Count} bildirim icin yeniden deneme.", due.Count);

        foreach (var state in due)
        {
            ct.ThrowIfCancellationRequested();
            try
            {
                var ok = await notificationSvc.RetryAsync(state.NotificationId, state.TenantKey, ct);
                _queue.RecordAttempt(state.NotificationId, success: ok, error: ok ? null : "Retry returned false (status not Failed or tenant mismatch)");
            }
            catch (Exception ex)
            {
                _logger.LogWarning(ex, "Webhook retry hata: notificationId={Id} tenant={Tenant}",
                    state.NotificationId, state.TenantKey);
                _queue.RecordAttempt(state.NotificationId, success: false, error: ex.Message);
            }
        }
    }
}
