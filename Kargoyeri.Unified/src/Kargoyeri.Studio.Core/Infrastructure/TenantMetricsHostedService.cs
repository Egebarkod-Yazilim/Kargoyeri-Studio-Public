using Microsoft.Extensions.Hosting;

namespace Kargoyeri.Studio.Core.Infrastructure;

/// <summary>
/// P4-#1 — Her gun yerel saat 00:30'da bir onceki gunun snapshot'ini diske yazar.
/// Ilk acilis: hemen bugunun (kismi) snapshot'ini yazar.
/// </summary>
public sealed class TenantMetricsHostedService : BackgroundService
{
    private readonly IServiceProvider _services;
    private readonly ILogger<TenantMetricsHostedService> _logger;

    public TenantMetricsHostedService(IServiceProvider services, ILogger<TenantMetricsHostedService> logger)
    {
        _services = services;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // Initial snapshot (delay a bit so the app is fully started)
        try
        {
            await Task.Delay(TimeSpan.FromSeconds(30), stoppingToken);
            await RunOnceAsync(DateOnly.FromDateTime(DateTime.UtcNow), stoppingToken);
        }
        catch (OperationCanceledException) { return; }
        catch (Exception ex) { _logger.LogWarning(ex, "Initial tenant metrics snapshot failed."); }

        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var now = DateTime.UtcNow;
                var nextRun = now.Date.AddDays(1).AddMinutes(30); // 00:30 UTC
                var delay = nextRun - now;
                if (delay < TimeSpan.FromMinutes(1)) delay = TimeSpan.FromMinutes(1);
                await Task.Delay(delay, stoppingToken);

                // Snapshot dunkun (kapanmis) gunu de, bugunu de yaz
                var yesterday = DateOnly.FromDateTime(DateTime.UtcNow.AddDays(-1));
                var today = DateOnly.FromDateTime(DateTime.UtcNow);
                await RunOnceAsync(yesterday, stoppingToken);
                await RunOnceAsync(today, stoppingToken);
            }
            catch (OperationCanceledException) { break; }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Tenant metrics scheduled run failed; will retry in 1h.");
                await Task.Delay(TimeSpan.FromHours(1), stoppingToken);
            }
        }
    }

    private async Task RunOnceAsync(DateOnly date, CancellationToken ct)
    {
        using var scope = _services.CreateScope();
        var agg = scope.ServiceProvider.GetRequiredService<TenantMetricsAggregator>();
        var snap = await agg.ComputeForDateAsync(date, ct);
        await agg.PersistAsync(date, snap, ct);
    }
}
