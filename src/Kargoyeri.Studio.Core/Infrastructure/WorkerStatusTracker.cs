namespace Kargoyeri.Studio.Core.Infrastructure;

/// <summary>
/// Background worker'ın son çalışma durumunu tutan singleton.
/// Dashboard'da gösterilmek üzere thread-safe güncellenir.
/// </summary>
public sealed class WorkerStatusTracker
{
    private volatile WorkerCycleStatus _last = new();

    public WorkerCycleStatus Last => _last;

    public void RecordCycleStart()
    {
        _last = _last with { IsRunning = true, LastCycleStartedAt = DateTimeOffset.UtcNow };
    }

    public void RecordCycleSuccess(int processed)
    {
        var now = DateTimeOffset.UtcNow;
        _last = _last with
        {
            IsRunning         = false,
            LastSuccessAt     = now,
            LastProcessed     = processed,
            TotalProcessed    = _last.TotalProcessed + processed,
            TotalCycles       = _last.TotalCycles + 1,
            ConsecutiveErrors = 0,
            LastError         = null
        };
    }

    public void RecordCycleError(string error)
    {
        _last = _last with
        {
            IsRunning         = false,
            LastErrorAt       = DateTimeOffset.UtcNow,
            ConsecutiveErrors = _last.ConsecutiveErrors + 1,
            LastError         = error
        };
    }
}

public sealed record WorkerCycleStatus
{
    public bool IsRunning { get; init; }
    public DateTimeOffset? LastCycleStartedAt { get; init; }
    public DateTimeOffset? LastSuccessAt { get; init; }
    public DateTimeOffset? LastErrorAt { get; init; }
    public int LastProcessed { get; init; }
    public int TotalProcessed { get; init; }
    public int TotalCycles { get; init; }
    public int ConsecutiveErrors { get; init; }
    public string? LastError { get; init; }

    public bool IsHealthy => ConsecutiveErrors < 3;
    public TimeSpan? TimeSinceLastSuccess =>
        LastSuccessAt.HasValue ? DateTimeOffset.UtcNow - LastSuccessAt.Value : null;
}
