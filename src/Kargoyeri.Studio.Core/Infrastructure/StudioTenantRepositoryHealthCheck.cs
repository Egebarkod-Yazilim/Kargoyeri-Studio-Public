using Kargoyeri.Application.Services;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Kargoyeri.Studio.Core.Infrastructure;

/// <summary>
/// Tenant deposu (JSON veya SQL) kolay bir okuma yapabiliyor mu diye kontrol eder.
/// </summary>
internal sealed class StudioTenantRepositoryHealthCheck : IHealthCheck
{
    private readonly CustomerService _customerService;

    public StudioTenantRepositoryHealthCheck(CustomerService customerService)
    {
        _customerService = customerService;
    }

    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        try
        {
            var started = DateTimeOffset.UtcNow;
            var list    = await _customerService.ListAllAsync(cancellationToken);
            var elapsed = (DateTimeOffset.UtcNow - started).TotalMilliseconds;

            var data = new Dictionary<string, object>
            {
                ["tenantCount"] = list.Count,
                ["elapsedMs"]   = Math.Round(elapsed, 1)
            };

            return elapsed > 2000
                ? HealthCheckResult.Degraded($"Tenant depo yavas yanit veriyor ({elapsed:N0} ms).", data: data)
                : HealthCheckResult.Healthy($"Tenant depo OK ({list.Count} musteri).", data);
        }
        catch (Exception ex)
        {
            return HealthCheckResult.Unhealthy($"Tenant deposu erisilemez: {ex.Message}", ex);
        }
    }
}
