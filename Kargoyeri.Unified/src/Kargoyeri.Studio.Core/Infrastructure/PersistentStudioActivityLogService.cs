using System.Collections.Concurrent;
using System.Text.Json;

namespace Kargoyeri.Studio.Core.Infrastructure;

/// <summary>
/// Ring buffer + disk persistence. JSONL formatinda gunluk dosyalar yazar
/// (activity-yyyyMMdd.jsonl). Uygulama yeniden baslatildiginda son 2 gunun
/// kayitlarini hafizaya geri yukler. Arka planda channel uzerinden yazarak
/// request path'inde IO bloklamaz.
/// </summary>
public sealed class PersistentStudioActivityLogService : IStudioActivityLogService, IDisposable
{
    private const int Capacity = 5000;
    private const int RehydrateDays = 2;

    private readonly ConcurrentQueue<StudioActivityLogEntry> _queue = new();
    private readonly System.Threading.Channels.Channel<StudioActivityLogEntry> _writeChannel;
    private readonly string _directory;
    private readonly CancellationTokenSource _cts = new();
    private readonly Task _writer;
    private int _count;

    public PersistentStudioActivityLogService(IWebHostEnvironment env)
    {
        _directory = Path.Combine(env.ContentRootPath, "activity-logs");
        Directory.CreateDirectory(_directory);

        _writeChannel = System.Threading.Channels.Channel.CreateUnbounded<StudioActivityLogEntry>(
            new System.Threading.Channels.UnboundedChannelOptions
            {
                SingleReader = true,
                SingleWriter = false
            });

        RehydrateFromDisk();

        _writer = Task.Run(WriteLoopAsync);
    }

    public void Append(StudioActivityLogEntry entry)
    {
        _queue.Enqueue(entry);
        if (System.Threading.Interlocked.Increment(ref _count) > Capacity)
        {
            _queue.TryDequeue(out _);
            System.Threading.Interlocked.Decrement(ref _count);
        }

        _writeChannel.Writer.TryWrite(entry);
    }

    public IReadOnlyList<StudioActivityLogEntry> GetRecent(string? tenantKey = null, string? username = null, int max = 200)
    {
        var entries = _queue.AsEnumerable();
        if (!string.IsNullOrWhiteSpace(tenantKey))
            entries = entries.Where(e => string.Equals(e.TenantKey, tenantKey, StringComparison.OrdinalIgnoreCase));
        if (!string.IsNullOrWhiteSpace(username))
            entries = entries.Where(e => string.Equals(e.Username, username, StringComparison.OrdinalIgnoreCase));
        return entries.OrderByDescending(e => e.OccurredAtUtc).Take(max).ToList();
    }

    public IReadOnlyList<string> GetTenantKeys() =>
        _queue.Select(e => e.TenantKey).Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(x => x).ToList();

    /// <summary>Diskteki son N gunun dosyalarini hafizaya yukler.</summary>
    private void RehydrateFromDisk()
    {
        try
        {
            for (int i = RehydrateDays - 1; i >= 0; i--)
            {
                var date = DateTime.UtcNow.Date.AddDays(-i);
                var file = GetFilePath(date);
                if (!File.Exists(file)) continue;

                foreach (var line in File.ReadLines(file))
                {
                    if (string.IsNullOrWhiteSpace(line)) continue;
                    try
                    {
                        var entry = JsonSerializer.Deserialize<StudioActivityLogEntry>(line);
                        if (entry is null) continue;
                        _queue.Enqueue(entry);
                        if (System.Threading.Interlocked.Increment(ref _count) > Capacity)
                        {
                            _queue.TryDequeue(out _);
                            System.Threading.Interlocked.Decrement(ref _count);
                        }
                    }
                    catch { /* bozuk satir yoksay */ }
                }
            }
        }
        catch { /* rehydrate opsiyonel */ }
    }

    private async Task WriteLoopAsync()
    {
        try
        {
            await foreach (var entry in _writeChannel.Reader.ReadAllAsync(_cts.Token))
            {
                try
                {
                    var file = GetFilePath(entry.OccurredAtUtc.UtcDateTime.Date);
                    var json = JsonSerializer.Serialize(entry);
                    await File.AppendAllTextAsync(file, json + Environment.NewLine, _cts.Token);
                }
                catch { /* disk yazimi basarisiz olsa da request bloklanmaz */ }
            }
        }
        catch (OperationCanceledException) { }
    }

    private string GetFilePath(DateTime utcDate) =>
        Path.Combine(_directory, $"activity-{utcDate:yyyyMMdd}.jsonl");

    public void Dispose()
    {
        _writeChannel.Writer.TryComplete();
        _cts.Cancel();
        try { _writer.Wait(TimeSpan.FromSeconds(2)); } catch { }
        _cts.Dispose();
    }
}
