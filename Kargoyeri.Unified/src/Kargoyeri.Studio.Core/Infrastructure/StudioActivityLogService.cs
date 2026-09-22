using System.Collections.Concurrent;

namespace Kargoyeri.Studio.Core.Infrastructure;

public sealed record StudioActivityLogEntry(
    Guid                Id,
    string              TenantKey,
    string              Username,
    string              Role,
    string              Controller,
    string              Action,
    string              HttpMethod,
    string              Path,
    string              IpAddress,
    int                 StatusCode,
    DateTimeOffset      OccurredAtUtc);

public interface IStudioActivityLogService
{
    void Append(StudioActivityLogEntry entry);
    IReadOnlyList<StudioActivityLogEntry> GetRecent(string? tenantKey = null, string? username = null, int max = 200);
    IReadOnlyList<string> GetTenantKeys();
}

/// <summary>
/// Ring buffer — en fazla <see cref="Capacity"/> kayıt tutar, eskiler düşer.
/// Uygulama yeniden başlatılırsa kayıtlar sıfırlanır (in-memory).
/// </summary>
public sealed class InMemoryStudioActivityLogService : IStudioActivityLogService
{
    private const int Capacity = 5000;

    private readonly ConcurrentQueue<StudioActivityLogEntry> _queue = new();
    private int _count;

    public void Append(StudioActivityLogEntry entry)
    {
        _queue.Enqueue(entry);

        // Kapasite aşıldıysa en eski kaydı at
        if (System.Threading.Interlocked.Increment(ref _count) > Capacity)
        {
            _queue.TryDequeue(out _);
            System.Threading.Interlocked.Decrement(ref _count);
        }
    }

    public IReadOnlyList<StudioActivityLogEntry> GetRecent(string? tenantKey = null, string? username = null, int max = 200)
    {
        var entries = _queue.AsEnumerable();

        if (!string.IsNullOrWhiteSpace(tenantKey))
            entries = entries.Where(e => string.Equals(e.TenantKey, tenantKey, StringComparison.OrdinalIgnoreCase));

        if (!string.IsNullOrWhiteSpace(username))
            entries = entries.Where(e => string.Equals(e.Username, username, StringComparison.OrdinalIgnoreCase));

        return entries
            .OrderByDescending(e => e.OccurredAtUtc)
            .Take(max)
            .ToList();
    }

    public IReadOnlyList<string> GetTenantKeys() =>
        _queue.Select(e => e.TenantKey).Distinct(StringComparer.OrdinalIgnoreCase).OrderBy(x => x).ToList();
}
