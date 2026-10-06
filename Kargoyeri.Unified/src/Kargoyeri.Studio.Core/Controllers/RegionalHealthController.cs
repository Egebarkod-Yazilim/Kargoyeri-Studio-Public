using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Diagnostics.HealthChecks;

namespace Kargoyeri.Studio.Core.Controllers;

/// <summary>
/// P4-#4 — Multi-region failover icin probe endpoint.
///
/// Kullanim: bir yuksek-kullanilirlik load balancer (Cloudflare / Azure Front Door)
/// her region instance'a `GET /healthz/regional` isler. Donen JSON:
/// <code>
/// {
///   "region": "tr-west-1",
///   "instance": "studio-1.kargoyeri.local",
///   "version": "1.4.0",
///   "uptime_seconds": 3601,
///   "healthy": true,
///   "checks": [{ "name": "studio-storage", "status": "Healthy" }]
/// }
/// </code>
///
/// Region/instance adlari konfig'den okunur:
/// <code>"Studio:Region:Code": "tr-west-1", "Studio:Region:Instance": "studio-1"</code>
/// </summary>
[AllowAnonymous]
[ApiController]
[Route("healthz")]
public sealed class RegionalHealthController : ControllerBase
{
    private static readonly DateTimeOffset ProcessStartUtc = DateTimeOffset.UtcNow;
    private readonly HealthCheckService _health;
    private readonly IConfiguration _config;

    public RegionalHealthController(HealthCheckService health, IConfiguration config)
    {
        _health = health;
        _config = config;
    }

    [HttpGet("regional")]
    public async Task<IActionResult> Regional(CancellationToken ct)
    {
        var report = await _health.CheckHealthAsync(ct);
        var version = typeof(RegionalHealthController).Assembly.GetName().Version?.ToString() ?? "unknown";

        var payload = new
        {
            region = _config["Studio:Region:Code"] ?? Environment.GetEnvironmentVariable("STUDIO_REGION") ?? "default",
            instance = _config["Studio:Region:Instance"] ?? Environment.MachineName,
            version,
            uptime_seconds = (int)(DateTimeOffset.UtcNow - ProcessStartUtc).TotalSeconds,
            healthy = report.Status == HealthStatus.Healthy,
            status = report.Status.ToString(),
            checks = report.Entries.Select(kv => new
            {
                name = kv.Key,
                status = kv.Value.Status.ToString(),
                duration_ms = (int)kv.Value.Duration.TotalMilliseconds,
                description = kv.Value.Description
            }).ToArray(),
            timestamp_utc = DateTimeOffset.UtcNow
        };

        // Failover'a yardimci: Healthy olmayan instance'i load balancer havuzdan dussun
        var statusCode = report.Status switch
        {
            HealthStatus.Healthy => 200,
            HealthStatus.Degraded => 200,    // hala servis verebilir
            _ => 503
        };

        Response.Headers["Cache-Control"] = "no-store";
        return StatusCode(statusCode, payload);
    }
}
