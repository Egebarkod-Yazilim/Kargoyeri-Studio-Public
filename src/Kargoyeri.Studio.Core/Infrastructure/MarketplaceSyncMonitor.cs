using System.Collections.Concurrent;

namespace Kargoyeri.Studio.Core.Infrastructure;

public sealed class MarketplaceSyncRun
{
    public string TenantKey { get; init; } = string.Empty;
    public string Platform { get; init; } = string.Empty;
    public DateTimeOffset StartedAtUtc { get; init; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? CompletedAtUtc { get; set; }
    public int PulledCount { get; set; }
    public int ImportedCount { get; set; }
    public int SkippedCount { get; set; }
    public int FailedCount { get; set; }
    public bool Success { get; set; }
    public string? Message { get; set; }
}

public sealed class MarketplaceSyncMonitor
{
    private readonly ConcurrentDictionary<string, ConcurrentQueue<MarketplaceSyncRun>> _runs = new(StringComparer.OrdinalIgnoreCase);

    public void Record(MarketplaceSyncRun run)
    {
        var queue = _runs.GetOrAdd(run.TenantKey, _ => new ConcurrentQueue<MarketplaceSyncRun>());
        queue.Enqueue(run);

        while (queue.Count > 10 && queue.TryDequeue(out _))
        {
        }
    }

    public IReadOnlyCollection<MarketplaceSyncRun> ListByTenant(string tenantKey)
    {
        if (!_runs.TryGetValue(tenantKey, out var queue))
        {
            return Array.Empty<MarketplaceSyncRun>();
        }

        return queue.ToArray()
            .OrderByDescending(x => x.StartedAtUtc)
            .ToArray();
    }
}
