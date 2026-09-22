using Kargoyeri.Application.Abstractions.Persistence;
using Kargoyeri.Domain.Entities;
using Kargoyeri.Domain.Enums;
using Kargoyeri.Studio.Core.Infrastructure;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;

namespace Kargoyeri.Studio.Core.Services;

public sealed class WorkflowAutomationService : BackgroundService
{
    private static readonly TimeSpan Interval = TimeSpan.FromMinutes(10);

    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<WorkflowAutomationService> _logger;

    public WorkflowAutomationService(
        IServiceScopeFactory scopeFactory,
        ILogger<WorkflowAutomationService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                await RunOnceAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                break;
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "WorkflowAutomationService calisirken beklenmeyen hata olustu.");
            }

            await Task.Delay(Interval, stoppingToken);
        }
    }

    private async Task RunOnceAsync(CancellationToken cancellationToken)
    {
        await using var scope = _scopeFactory.CreateAsyncScope();
        var customerRepository = scope.ServiceProvider.GetRequiredService<ICustomerRepository>();
        var shipmentRepository = scope.ServiceProvider.GetRequiredService<IShipmentRepository>();
        var ruleStore = scope.ServiceProvider.GetRequiredService<WorkflowRuleStore>();
        var executionStore = scope.ServiceProvider.GetRequiredService<WorkflowExecutionStore>();
        var dispatcher = scope.ServiceProvider.GetRequiredService<WorkflowNotificationDispatcher>();

        var tenants = await customerRepository.ListAsync(cancellationToken);
        foreach (var tenant in tenants.Where(x => x.IsActive))
        {
            cancellationToken.ThrowIfCancellationRequested();

            var rules = (await ruleStore.GetAsync(tenant.TenantKey, cancellationToken))
                .Where(x => x.IsEnabled)
                .ToArray();

            if (rules.Length == 0)
            {
                continue;
            }

            var shipments = await shipmentRepository.ListAsync(tenant.TenantKey, cancellationToken);
            foreach (var shipment in shipments)
            {
                foreach (var rule in rules)
                {
                    if (!IsMatch(shipment, rule))
                    {
                        continue;
                    }

                    if (rule.OnlyOncePerShipment && executionStore.HasFired(tenant.TenantKey, rule.Id, shipment.ShipmentReference))
                    {
                        continue;
                    }

                    var sent = await dispatcher.DispatchAsync(tenant, shipment, rule, cancellationToken);
                    if (sent > 0)
                    {
                        await executionStore.MarkFiredAsync(tenant.TenantKey, rule.Id, shipment.ShipmentReference, cancellationToken);
                        _logger.LogInformation(
                            "Workflow tetiklendi. tenant={Tenant} shipment={Shipment} rule={Rule} channel={Channel} sent={Sent}",
                            tenant.TenantKey, shipment.ShipmentReference, rule.Name, rule.Channel, sent);
                    }
                }
            }
        }
    }

    private static bool IsMatch(CargoShipment shipment, Models.WorkflowRuleDefinition rule)
    {
        if (shipment.Status != (ShipmentStatus)rule.Status)
        {
            return false;
        }

        var elapsedHours = (DateTimeOffset.UtcNow - shipment.UpdatedAtUtc).TotalHours;
        return elapsedHours >= rule.ThresholdHours;
    }
}
