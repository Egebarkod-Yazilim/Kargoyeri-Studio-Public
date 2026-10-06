using Kargoyeri.Application.Options;
using Kargoyeri.Application.Services;
using Kargoyeri.Studio.Core.Infrastructure;
using Microsoft.Extensions.Options;

namespace Kargoyeri.Studio.Core.Services;

internal sealed class StudioShipmentRefreshService : BackgroundService
{
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly IOptionsMonitor<ProcessingOptions> _options;
    private readonly ILogger<StudioShipmentRefreshService> _logger;
    private readonly WorkerStatusTracker _tracker;

    public StudioShipmentRefreshService(
        IServiceScopeFactory scopeFactory,
        IOptionsMonitor<ProcessingOptions> options,
        ILogger<StudioShipmentRefreshService> logger,
        WorkerStatusTracker tracker)
    {
        _scopeFactory = scopeFactory;
        _options = options;
        _logger = logger;
        _tracker = tracker;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        _logger.LogInformation("StudioShipmentRefreshService baslatildi.");

        // İlk çalışmayı biraz geciktir — uygulama tam ayağa kalksın
        await Task.Delay(TimeSpan.FromSeconds(5), stoppingToken).ConfigureAwait(false);

        while (!stoppingToken.IsCancellationRequested)
        {
            var opts = _options.CurrentValue;
            var interval = TimeSpan.FromSeconds(Math.Max(5, opts.RefreshIntervalSeconds));

            try
            {
                await RunOneCycleAsync(opts, stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Shipment refresh dongusu beklenmedik hatayla karsilasti. {Interval}s sonra yeniden denenecek.", 10);
                _tracker.RecordCycleError(ex.Message);
                await SafeDelay(TimeSpan.FromSeconds(10), stoppingToken);
                continue;
            }

            await SafeDelay(interval, stoppingToken);
        }

        _logger.LogInformation("StudioShipmentRefreshService durduruldu.");
    }

    private async Task RunOneCycleAsync(ProcessingOptions opts, CancellationToken stoppingToken)
    {
        _tracker.RecordCycleStart();

        var cycleTimeoutSeconds = opts.MaxRefreshBatchSize * opts.ProviderTimeoutSeconds + 30;

        using var cycleCts = CancellationTokenSource.CreateLinkedTokenSource(stoppingToken);
        cycleCts.CancelAfter(TimeSpan.FromSeconds(cycleTimeoutSeconds));

        using var scope = _scopeFactory.CreateScope();
        var orchestrator = scope.ServiceProvider.GetRequiredService<CargoOrchestrator>();

        var processed = await orchestrator.RefreshPendingShipmentsAsync(
            take:                   opts.MaxRefreshBatchSize,
            cancellationToken:      cycleCts.Token,
            maxRetryCount:          opts.MaxRetryCount,
            providerTimeoutSeconds: opts.ProviderTimeoutSeconds,
            batchItemDelayMs:       opts.BatchItemDelayMs);

        _tracker.RecordCycleSuccess(processed);

        if (processed > 0)
        {
            _logger.LogInformation("Refresh dongusu tamamlandi: {ProcessedCount} gonderi islendi.", processed);
        }
    }

    private static async Task SafeDelay(TimeSpan delay, CancellationToken ct)
    {
        try
        {
            await Task.Delay(delay, ct).ConfigureAwait(false);
        }
        catch (OperationCanceledException)
        {
            // Normal durdurma
        }
    }
}
