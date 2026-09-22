using System.Collections.Concurrent;
using System.Text.Json;

namespace Kargoyeri.Studio.Core.Infrastructure;

/// <summary>
/// P2-#3 — Basarisiz outbound webhook (notification) gonderimleri icin
/// exponential-backoff zamanlayici. NotificationService.RetryAsync ile
/// birlikte calisir. Durum bellekte tutulur, JSONL'a yazilir; restart sonrasi
/// son 7 gun rehidre edilir.
/// </summary>
public sealed class OutboundWebhookRetryQueue
{
    public const int MaxAttempts = 6;

    /// <summary>1m, 5m, 15m, 1h, 6h, 24h — toplam ~31 saat.</summary>
    public static readonly TimeSpan[] BackoffSchedule = new[]
    {
        TimeSpan.FromMinutes(1),
        TimeSpan.FromMinutes(5),
        TimeSpan.FromMinutes(15),
        TimeSpan.FromHours(1),
        TimeSpan.FromHours(6),
        TimeSpan.FromHours(24)
    };

    public sealed record State(
        Guid NotificationId,
        string TenantKey,
        int Attempts,
        DateTimeOffset? LastAttemptUtc,
        DateTimeOffset NextAttemptUtc,
        bool Abandoned,
        string? LastError);

    private readonly string _directory;
    private readonly ConcurrentDictionary<Guid, State> _states = new();
    private readonly object _writeGate = new();

    public OutboundWebhookRetryQueue(IWebHostEnvironment env)
    {
        _directory = Path.Combine(env.ContentRootPath, "webhook-retry");
        Directory.CreateDirectory(_directory);
        Rehydrate();
    }

    public State? TryGet(Guid notificationId) =>
        _states.TryGetValue(notificationId, out var s) ? s : null;

    public IReadOnlyCollection<State> Snapshot() => _states.Values.ToArray();

    /// <summary>
    /// Verilen Failed notification icin ilk kez state olustur.
    /// Zaten varsa sessizce gec.
    /// </summary>
    public void TrackFailure(Guid notificationId, string tenantKey, DateTimeOffset failedAtUtc, string? error)
    {
        _states.GetOrAdd(notificationId, _ =>
        {
            var next = failedAtUtc.Add(BackoffSchedule[0]);
            var s = new State(notificationId, tenantKey, Attempts: 0, LastAttemptUtc: failedAtUtc, NextAttemptUtc: next, Abandoned: false, LastError: error);
            Persist(s);
            return s;
        });
    }

    /// <summary>Bir retry denemesinin sonucunu kaydeder.</summary>
    public void RecordAttempt(Guid notificationId, bool success, string? error)
    {
        _states.AddOrUpdate(notificationId,
            // ilk kez goruluyor — ust kontrolde bulunmali. Yine de defensively olustur.
            _ => new State(notificationId, "", 1, DateTimeOffset.UtcNow,
                DateTimeOffset.UtcNow.Add(BackoffSchedule[Math.Min(1, BackoffSchedule.Length - 1)]),
                false, error),
            (_, prev) =>
            {
                var attempts = prev.Attempts + 1;
                if (success)
                {
                    var s = prev with { Attempts = attempts, LastAttemptUtc = DateTimeOffset.UtcNow, Abandoned = true, LastError = null };
                    Persist(s);
                    return s;
                }

                var abandoned = attempts >= MaxAttempts;
                var nextDelay = abandoned
                    ? TimeSpan.Zero
                    : BackoffSchedule[Math.Min(attempts, BackoffSchedule.Length - 1)];
                var ns = prev with
                {
                    Attempts = attempts,
                    LastAttemptUtc = DateTimeOffset.UtcNow,
                    NextAttemptUtc = DateTimeOffset.UtcNow.Add(nextDelay),
                    Abandoned = abandoned,
                    LastError = error
                };
                Persist(ns);
                return ns;
            });
    }

    public IReadOnlyCollection<State> ListDue(DateTimeOffset now) =>
        _states.Values.Where(s => !s.Abandoned && s.NextAttemptUtc <= now).ToArray();

    private void Persist(State s)
    {
        try
        {
            lock (_writeGate)
            {
                var file = Path.Combine(_directory, $"webhook-retry-{DateTime.UtcNow:yyyyMMdd}.jsonl");
                File.AppendAllText(file, JsonSerializer.Serialize(s) + Environment.NewLine);
            }
        }
        catch
        {
            // Disk yazima hatalari fonksiyonel akisi durdurmamali.
        }
    }

    private void Rehydrate()
    {
        try
        {
            var cutoff = DateTime.UtcNow.AddDays(-7);
            foreach (var file in Directory.GetFiles(_directory, "webhook-retry-*.jsonl")
                .Where(f => File.GetLastWriteTimeUtc(f) >= cutoff))
            {
                foreach (var line in File.ReadAllLines(file))
                {
                    if (string.IsNullOrWhiteSpace(line)) continue;
                    try
                    {
                        var s = JsonSerializer.Deserialize<State>(line);
                        if (s is null) continue;
                        // Son satir kazanir
                        _states[s.NotificationId] = s;
                    }
                    catch { }
                }
            }
        }
        catch { }
    }
}
