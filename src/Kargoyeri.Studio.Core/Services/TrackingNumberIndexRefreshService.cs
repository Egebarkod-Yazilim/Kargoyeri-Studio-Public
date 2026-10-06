using Kargoyeri.Application.Services;
using Kargoyeri.Studio.Core.Infrastructure;

namespace Kargoyeri.Studio.Core.Services;

internal sealed class TrackingNumberIndexRefreshService : BackgroundService
{
    private static readonly TimeSpan InitialDelay = TimeSpan.FromSeconds(10);
    private static readonly TimeSpan RebuildInterval = TimeSpan.FromMinutes(5);

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly TrackingNumberIndex _index;
    private readonly ILogger<TrackingNumberIndexRefreshService> _logger;

    public TrackingNumberIndexRefreshService(
        IServiceScopeFactory scopeFactory,
        TrackingNumberIndex index,
        ILogger<TrackingNumberIndexRefreshService> logger)
    {
        _scopeFactory = scopeFactory;
        _index = index;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await DelaySafelyAsync(InitialDelay, stoppingToken).ConfigureAwait(false);

        while (!stoppingToken.IsCancellationRequested)
        {
            await RebuildAsync(stoppingToken).ConfigureAwait(false);
            await DelaySafelyAsync(RebuildInterval, stoppingToken).ConfigureAwait(false);
        }
    }

    private async Task RebuildAsync(CancellationToken cancellationToken)
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var customerService = scope.ServiceProvider.GetRequiredService<CustomerService>();
            var orchestrator = scope.ServiceProvider.GetRequiredService<CargoOrchestrator>();

            await _index.RebuildAsync(customerService, orchestrator, cancellationToken).ConfigureAwait(false);
            _logger.LogInformation(
                "Tracking index rebuild tamamlandi. Kayit={Count}, rebuiltAt={RebuiltAt}",
                _index.Count,
                _index.LastRebuiltAt);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Tracking index rebuild basarisiz oldu.");
        }
    }

    private static async Task DelaySafelyAsync(TimeSpan delay, CancellationToken cancellationToken)
    {
        try
        {
            await Task.Delay(delay, cancellationToken).ConfigureAwait(false);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
    }
}
