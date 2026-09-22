using System.Diagnostics;
using Kargoyeri.Application.Abstractions.Persistence;
using Kargoyeri.Application.Services;
using Kargoyeri.Contracts.Enums;
using Kargoyeri.Studio.Core.Infrastructure;

namespace Kargoyeri.Studio.Core.Services;

internal sealed class ProviderHealthProbeService : BackgroundService
{
    private static readonly TimeSpan InitialDelay = TimeSpan.FromSeconds(20);
    private static readonly TimeSpan ProbeInterval = TimeSpan.FromMinutes(15);
    private static readonly TimeSpan ProbeTimeout = TimeSpan.FromSeconds(8);

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ProviderHealthMonitor _monitor;
    private readonly ILogger<ProviderHealthProbeService> _logger;

    public ProviderHealthProbeService(
        IServiceScopeFactory scopeFactory,
        ProviderHealthMonitor monitor,
        ILogger<ProviderHealthProbeService> logger)
    {
        _scopeFactory = scopeFactory;
        _monitor = monitor;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        await SafeDelayAsync(InitialDelay, stoppingToken).ConfigureAwait(false);

        while (!stoppingToken.IsCancellationRequested)
        {
            await RunProbeCycleAsync(stoppingToken).ConfigureAwait(false);
            await SafeDelayAsync(ProbeInterval, stoppingToken).ConfigureAwait(false);
        }
    }

    private async Task RunProbeCycleAsync(CancellationToken cancellationToken)
    {
        try
        {
            using var scope = _scopeFactory.CreateScope();
            var customerService = scope.ServiceProvider.GetRequiredService<CustomerService>();
            var providerSettingsRepository = scope.ServiceProvider.GetRequiredService<IProviderSettingsRepository>();
            var catalog = scope.ServiceProvider.GetRequiredService<ProviderCatalogService>();

            var tenants = await customerService.ListAllAsync(cancellationToken).ConfigureAwait(false);
            foreach (var tenant in tenants.Where(x => x.IsActive))
            {
                foreach (var profile in catalog.List())
                {
                    if (!Enum.TryParse<CargoProviderTypeDto>(profile.ProviderCode, true, out var providerDto) ||
                        !tenant.AllowedProviders.Contains(providerDto))
                    {
                        continue;
                    }

                    var settings = await providerSettingsRepository.GetAsync(
                        tenant.TenantKey,
                        (Kargoyeri.Domain.Enums.CargoProviderType)providerDto,
                        cancellationToken).ConfigureAwait(false);

                    if (settings is null || !settings.IsEnabled || string.IsNullOrWhiteSpace(settings.EndpointBase))
                    {
                        continue;
                    }

                    await ProbeEndpointAsync(tenant.TenantKey, providerDto, settings.EndpointBase, cancellationToken).ConfigureAwait(false);
                }
            }
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Provider health probe dongusu tamamlanamadi.");
        }
    }

    private async Task ProbeEndpointAsync(
        string tenantKey,
        CargoProviderTypeDto provider,
        string endpoint,
        CancellationToken cancellationToken)
    {
        using var timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeoutCts.CancelAfter(ProbeTimeout);

        var sw = Stopwatch.StartNew();
        try
        {
            using var http = new HttpClient { Timeout = ProbeTimeout };
            var response = await http.GetAsync(endpoint, timeoutCts.Token).ConfigureAwait(false);
            sw.Stop();

            _monitor.Record(new ProviderHealthSample(
                tenantKey,
                provider,
                response.IsSuccessStatusCode,
                (int)sw.ElapsedMilliseconds,
                DateTimeOffset.UtcNow,
                endpoint,
                response.IsSuccessStatusCode ? null : $"HTTP {(int)response.StatusCode}",
                (int)response.StatusCode));
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            sw.Stop();
            _monitor.Record(new ProviderHealthSample(
                tenantKey,
                provider,
                false,
                (int)sw.ElapsedMilliseconds,
                DateTimeOffset.UtcNow,
                endpoint,
                $"Timeout ({ProbeTimeout.TotalSeconds:0}s)"));
        }
        catch (Exception ex)
        {
            sw.Stop();
            _monitor.Record(new ProviderHealthSample(
                tenantKey,
                provider,
                false,
                (int)sw.ElapsedMilliseconds,
                DateTimeOffset.UtcNow,
                endpoint,
                ex.Message));
        }
    }

    private static async Task SafeDelayAsync(TimeSpan delay, CancellationToken cancellationToken)
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
