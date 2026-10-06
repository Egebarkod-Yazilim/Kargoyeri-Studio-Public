using System.Text.Json;
using Kargoyeri.Contracts.Enums;

namespace Kargoyeri.Studio.Core.Infrastructure;

public sealed record ProviderHealthSample(
    string TenantKey,
    CargoProviderTypeDto Provider,
    bool Success,
    int? LatencyMs,
    DateTimeOffset OccurredAtUtc,
    string? Endpoint = null,
    string? Error = null,
    int? StatusCode = null);

public sealed class ProviderHealthSummary
{
    public CargoProviderTypeDto Provider { get; init; }
    public int ProbeCount { get; init; }
    public int SuccessCount { get; init; }
    public int FailureCount { get; init; }
    public double SuccessRate { get; init; }
    public double ErrorRate { get; init; }
    public int? P95LatencyMs { get; init; }
    public DateTimeOffset? LastCheckedAtUtc { get; init; }
    public string? LastError { get; init; }
}

public sealed class ProviderHealthMonitor
{
    private const int MaxSamples = 20000;
    private const int RehydrateDays = 2;

    private readonly string _directory;
    private readonly List<ProviderHealthSample> _samples = new();
    private readonly object _gate = new();

    public ProviderHealthMonitor(IWebHostEnvironment env)
    {
        _directory = Path.Combine(env.ContentRootPath, "provider-health");
        Directory.CreateDirectory(_directory);
        Rehydrate();
    }

    public void Record(ProviderHealthSample sample)
    {
        lock (_gate)
        {
            _samples.Add(sample);
            if (_samples.Count > MaxSamples)
            {
                _samples.RemoveRange(0, _samples.Count - MaxSamples);
            }
        }

        try
        {
            var file = Path.Combine(_directory, $"provider-health-{sample.OccurredAtUtc.UtcDateTime:yyyyMMdd}.jsonl");
            var json = JsonSerializer.Serialize(sample);
            File.AppendAllText(file, json + Environment.NewLine);
        }
        catch
        {
        }
    }

    public IReadOnlyCollection<ProviderHealthSummary> SummarizeTenant(
        string tenantKey,
        IEnumerable<CargoProviderTypeDto> providers,
        TimeSpan window)
    {
        var since = DateTimeOffset.UtcNow.Subtract(window);
        List<ProviderHealthSample> items;
        lock (_gate)
        {
            items = _samples
                .Where(x =>
                    string.Equals(x.TenantKey, tenantKey, StringComparison.OrdinalIgnoreCase) &&
                    x.OccurredAtUtc >= since)
                .ToList();
        }

        return providers
            .Distinct()
            .Select(provider =>
            {
                var providerItems = items
                    .Where(x => x.Provider == provider)
                    .OrderBy(x => x.OccurredAtUtc)
                    .ToList();

                var probeCount = providerItems.Count;
                var successCount = providerItems.Count(x => x.Success);
                var failureCount = probeCount - successCount;
                var orderedLatencies = providerItems
                    .Where(x => x.LatencyMs.HasValue)
                    .Select(x => x.LatencyMs!.Value)
                    .OrderBy(x => x)
                    .ToArray();

                return new ProviderHealthSummary
                {
                    Provider = provider,
                    ProbeCount = probeCount,
                    SuccessCount = successCount,
                    FailureCount = failureCount,
                    SuccessRate = probeCount == 0 ? 0 : Math.Round(100d * successCount / probeCount, 1),
                    ErrorRate = probeCount == 0 ? 0 : Math.Round(100d * failureCount / probeCount, 1),
                    P95LatencyMs = orderedLatencies.Length == 0 ? null : orderedLatencies[(int)Math.Ceiling(orderedLatencies.Length * 0.95d) - 1],
                    LastCheckedAtUtc = providerItems.LastOrDefault()?.OccurredAtUtc,
                    LastError = providerItems.LastOrDefault(x => !x.Success)?.Error
                };
            })
            .ToArray();
    }

    private void Rehydrate()
    {
        try
        {
            for (var i = RehydrateDays - 1; i >= 0; i--)
            {
                var date = DateTime.UtcNow.Date.AddDays(-i);
                var file = Path.Combine(_directory, $"provider-health-{date:yyyyMMdd}.jsonl");
                if (!File.Exists(file))
                {
                    continue;
                }

                foreach (var line in File.ReadLines(file))
                {
                    if (string.IsNullOrWhiteSpace(line))
                    {
                        continue;
                    }

                    try
                    {
                        var sample = JsonSerializer.Deserialize<ProviderHealthSample>(line);
                        if (sample is not null)
                        {
                            _samples.Add(sample);
                        }
                    }
                    catch
                    {
                    }
                }
            }

            if (_samples.Count > MaxSamples)
            {
                _samples.RemoveRange(0, _samples.Count - MaxSamples);
            }
        }
        catch
        {
        }
    }
}
