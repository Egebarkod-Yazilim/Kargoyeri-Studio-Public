using Kargoyeri.Application.Services;
using Kargoyeri.Contracts.Enums;
using Kargoyeri.Studio.Core.Infrastructure;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Configuration;

namespace Kargoyeri.Studio.Core.Controllers;

/// <summary>
/// P2-#4 — Prometheus scrape edilebilir <c>/metrics</c> endpoint.
///
/// Yayinlanan metrikler:
///   * kargoyeri_tenants_total                                         (gauge)
///   * kargoyeri_shipments_total{tenant,provider,status}              (gauge)
///   * kargoyeri_provider_health_success_rate{tenant,provider} 0..100 (gauge, 24h)
///   * kargoyeri_provider_health_p95_latency_ms{tenant,provider}     (gauge)
///   * kargoyeri_webhook_retry_pending                               (gauge)
///   * kargoyeri_webhook_retry_abandoned                             (gauge)
///   * kargoyeri_process_uptime_seconds                              (gauge)
///   * kargoyeri_process_memory_bytes                                (gauge)
///
/// Auth: <c>Studio:Metrics:Token</c> set edildiyse <c>X-Metrics-Token</c>
/// header veya <c>?token=</c> query param zorunlu. Aksi halde acik (dev).
/// </summary>
[AllowAnonymous]
[ApiController]
[Route("metrics")]
public sealed class MetricsController : ControllerBase
{
    private static readonly DateTimeOffset ProcessStartUtc = DateTimeOffset.UtcNow;

    private readonly CustomerService _customerService;
    private readonly CargoOrchestrator _orchestrator;
    private readonly ProviderHealthMonitor _providerHealth;
    private readonly OutboundWebhookRetryQueue _retryQueue;
    private readonly IConfiguration _configuration;

    public MetricsController(
        CustomerService customerService,
        CargoOrchestrator orchestrator,
        ProviderHealthMonitor providerHealth,
        OutboundWebhookRetryQueue retryQueue,
        IConfiguration configuration)
    {
        _customerService = customerService;
        _orchestrator    = orchestrator;
        _providerHealth  = providerHealth;
        _retryQueue      = retryQueue;
        _configuration   = configuration;
    }

    [HttpGet("")]
    public async Task<IActionResult> Index([FromQuery] string? token, CancellationToken ct)
    {
        // ── Auth gate (opsiyonel) ──
        var configuredToken = _configuration["Studio:Metrics:Token"];
        if (!string.IsNullOrWhiteSpace(configuredToken))
        {
            var headerToken = Request.Headers.TryGetValue("X-Metrics-Token", out var h) ? h.ToString() : null;
            var supplied    = !string.IsNullOrWhiteSpace(headerToken) ? headerToken : token;
            if (!string.Equals(supplied, configuredToken, StringComparison.Ordinal))
                return Unauthorized();
        }

        var w = new PrometheusTextWriter();

        // ── Process metrics ──
        var uptime = (DateTimeOffset.UtcNow - ProcessStartUtc).TotalSeconds;
        w.Gauge("kargoyeri_process_uptime_seconds", "Studio surecinin baslangictan beri gecen saniye.", uptime);
        w.Gauge("kargoyeri_process_memory_bytes",   "GC tarafindan ayrilmis toplam managed bellek.", GC.GetTotalMemory(forceFullCollection: false));

        // ── Tenants ──
        var tenants = await _customerService.ListAllAsync(ct);
        w.Gauge("kargoyeri_tenants_total", "Sistemdeki toplam tenant sayisi.", tenants.Count);

        // ── Shipments per tenant/provider/status ──
        // Yuksek hacimde maliyetli olabilir; cache yok — Prometheus scrape periyodu >= 30sn olmali.
        foreach (var tenant in tenants)
        {
            ct.ThrowIfCancellationRequested();
            var ships = await _orchestrator.ListShipmentsAsync(tenant.TenantKey, ct);

            var grouped = ships.GroupBy(s => (s.Provider, s.Status));
            foreach (var g in grouped)
            {
                w.Gauge("kargoyeri_shipments_total",
                    "Tenant + provider + status bazinda gonderi sayisi.",
                    g.Count(),
                    new[]
                    {
                        ("tenant",   tenant.TenantKey),
                        ("provider", g.Key.Provider.ToString()),
                        ("status",   g.Key.Status.ToString())
                    });
            }

            // Provider health (24h penceresi)
            var allowed = tenant.AllowedProviders.ToArray();
            if (allowed.Length > 0)
            {
                var summaries = _providerHealth.SummarizeTenant(tenant.TenantKey, allowed, TimeSpan.FromHours(24));
                foreach (var s in summaries)
                {
                    if (s.ProbeCount == 0) continue;
                    var labels = new[]
                    {
                        ("tenant",   tenant.TenantKey),
                        ("provider", s.Provider.ToString())
                    };
                    w.Gauge("kargoyeri_provider_health_success_rate",
                        "24 saatlik provider probe basari orani (yuzde).", s.SuccessRate, labels);
                    if (s.P95LatencyMs is { } p95)
                    {
                        w.Gauge("kargoyeri_provider_health_p95_latency_ms",
                            "24 saatlik provider probe p95 latency (ms).", p95, labels);
                    }
                }
            }
        }

        // ── Webhook retry kuyrugu ──
        var states = _retryQueue.Snapshot();
        w.Gauge("kargoyeri_webhook_retry_pending",
            "Otomatik retry kuyrugunda bekleyen (terkedilmemis) bildirim sayisi.",
            states.Count(s => !s.Abandoned));
        w.Gauge("kargoyeri_webhook_retry_abandoned",
            "6 deneme sonrasi terkedilen webhook bildirimi sayisi.",
            states.Count(s => s.Abandoned));

        return Content(w.Build(), "text/plain; version=0.0.4; charset=utf-8");
    }
}
