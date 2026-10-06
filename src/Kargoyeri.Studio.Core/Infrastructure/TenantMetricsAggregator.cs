using System.Collections.Concurrent;
using System.Text.Json;
using Kargoyeri.Application.Services;
using Kargoyeri.Contracts.Dtos;
using Kargoyeri.Contracts.Enums;

namespace Kargoyeri.Studio.Core.Infrastructure;

/// <summary>
/// P4-#1 — Gunluk tenant metrik rollup'i.
///
/// Her tenant icin gunluk olarak:
///   * total shipment count
///   * by-status count map
///   * by-provider count map
///   * delivered count (o gun teslim edilen)
///
/// JSON dosyalari `tenant-metrics/yyyy-MM-dd.json` altinda saklanir.
/// Bu dosyalar billing (P4-#2) ve analytics icin kullanilir.
/// </summary>
public sealed class TenantMetricsAggregator
{
    private readonly string _directory;
    private readonly CustomerService _customerService;
    private readonly CargoOrchestrator _orchestrator;
    private readonly ILogger<TenantMetricsAggregator> _logger;
    private readonly ConcurrentDictionary<string, TenantDailyMetrics> _todayCache = new();

    public TenantMetricsAggregator(
        IWebHostEnvironment env,
        CustomerService customerService,
        CargoOrchestrator orchestrator,
        ILogger<TenantMetricsAggregator> logger)
    {
        _directory = Path.Combine(env.ContentRootPath, "tenant-metrics");
        System.IO.Directory.CreateDirectory(_directory);
        _customerService = customerService;
        _orchestrator = orchestrator;
        _logger = logger;
    }

    public string Directory => _directory;

    /// <summary>Computes today's snapshot in-memory (idempotent, can be called repeatedly).</summary>
    public async Task<IReadOnlyList<TenantDailyMetrics>> ComputeTodayAsync(CancellationToken ct)
    {
        var date = DateOnly.FromDateTime(DateTime.UtcNow);
        return await ComputeForDateAsync(date, ct);
    }

    public async Task<IReadOnlyList<TenantDailyMetrics>> ComputeForDateAsync(DateOnly date, CancellationToken ct)
    {
        var results = new List<TenantDailyMetrics>();
        var tenants = await _customerService.ListAllAsync(ct);

        foreach (var tenant in tenants)
        {
            ct.ThrowIfCancellationRequested();
            var ships = await _orchestrator.ListShipmentsAsync(tenant.TenantKey, ct);

            var byStatus = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            var byProvider = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            int deliveredOnDate = 0;
            int createdOnDate = 0;

            foreach (var s in ships)
            {
                byStatus[s.Status.ToString()] = byStatus.GetValueOrDefault(s.Status.ToString()) + 1;
                byProvider[s.Provider.ToString()] = byProvider.GetValueOrDefault(s.Provider.ToString()) + 1;

                var createdLocal = DateOnly.FromDateTime(s.CreatedAtUtc.UtcDateTime);
                if (createdLocal == date) createdOnDate++;

                if (s.Status == ShipmentStatusDto.Delivered &&
                    DateOnly.FromDateTime(s.UpdatedAtUtc.UtcDateTime) == date)
                {
                    deliveredOnDate++;
                }
            }

            var metric = new TenantDailyMetrics
            {
                Date = date,
                TenantKey = tenant.TenantKey,
                TenantName = tenant.Name,
                TotalShipments = ships.Count,
                CreatedOnDate = createdOnDate,
                DeliveredOnDate = deliveredOnDate,
                ByStatus = byStatus,
                ByProvider = byProvider,
                ComputedAtUtc = DateTimeOffset.UtcNow
            };
            results.Add(metric);
            _todayCache[$"{date:yyyy-MM-dd}|{tenant.TenantKey}"] = metric;
        }

        return results;
    }

    /// <summary>Persists a snapshot to disk. Overwrites existing same-day file.</summary>
    public async Task<string> PersistAsync(DateOnly date, IReadOnlyList<TenantDailyMetrics> snapshot, CancellationToken ct)
    {
        var file = Path.Combine(_directory, $"{date:yyyy-MM-dd}.json");
        var payload = JsonSerializer.Serialize(snapshot, new JsonSerializerOptions { WriteIndented = true });
        await File.WriteAllTextAsync(file, payload, ct);
        _logger.LogInformation("Tenant metrics persisted: {File} ({Count} tenants)", file, snapshot.Count);
        return file;
    }

    /// <summary>Loads a snapshot from disk; returns empty if missing.</summary>
    public async Task<IReadOnlyList<TenantDailyMetrics>> LoadAsync(DateOnly date, CancellationToken ct)
    {
        var file = Path.Combine(_directory, $"{date:yyyy-MM-dd}.json");
        if (!File.Exists(file)) return Array.Empty<TenantDailyMetrics>();
        var json = await File.ReadAllTextAsync(file, ct);
        var data = JsonSerializer.Deserialize<List<TenantDailyMetrics>>(json);
        return data ?? new List<TenantDailyMetrics>();
    }

    /// <summary>Lists which dates have persisted snapshots, descending.</summary>
    public IReadOnlyList<DateOnly> ListPersistedDates()
    {
        return new DirectoryInfo(_directory)
            .EnumerateFiles("*.json")
            .Select(f =>
            {
                var name = Path.GetFileNameWithoutExtension(f.Name);
                return DateOnly.TryParse(name, out var d) ? d : (DateOnly?)null;
            })
            .Where(d => d.HasValue)
            .Select(d => d!.Value)
            .OrderByDescending(d => d)
            .ToList();
    }

    /// <summary>Aggregates a date range (for billing). Inclusive on both ends.</summary>
    public async Task<IReadOnlyDictionary<string, TenantPeriodMetrics>> AggregateRangeAsync(
        DateOnly fromInclusive, DateOnly toInclusive, CancellationToken ct)
    {
        var result = new Dictionary<string, TenantPeriodMetrics>(StringComparer.OrdinalIgnoreCase);
        for (var d = fromInclusive; d <= toInclusive; d = d.AddDays(1))
        {
            var snap = await LoadAsync(d, ct);
            foreach (var m in snap)
            {
                if (!result.TryGetValue(m.TenantKey, out var period))
                {
                    period = new TenantPeriodMetrics
                    {
                        TenantKey = m.TenantKey,
                        TenantName = m.TenantName,
                        From = fromInclusive,
                        To = toInclusive
                    };
                    result[m.TenantKey] = period;
                }
                period.CreatedTotal += m.CreatedOnDate;
                period.DeliveredTotal += m.DeliveredOnDate;
            }
        }
        return result;
    }
}

public sealed class TenantDailyMetrics
{
    public DateOnly Date { get; set; }
    public string TenantKey { get; set; } = string.Empty;
    public string TenantName { get; set; } = string.Empty;
    public int TotalShipments { get; set; }
    public int CreatedOnDate { get; set; }
    public int DeliveredOnDate { get; set; }
    public Dictionary<string, int> ByStatus { get; set; } = new();
    public Dictionary<string, int> ByProvider { get; set; } = new();
    public DateTimeOffset ComputedAtUtc { get; set; }
}

public sealed class TenantPeriodMetrics
{
    public string TenantKey { get; set; } = string.Empty;
    public string TenantName { get; set; } = string.Empty;
    public DateOnly From { get; set; }
    public DateOnly To { get; set; }
    public int CreatedTotal { get; set; }
    public int DeliveredTotal { get; set; }
}
