using System.Collections.Concurrent;
using Kargoyeri.Application.Abstractions.Persistence;

namespace Kargoyeri.Studio.Core.Infrastructure;

/// <summary>
/// P2-#6 — Soft-delete edilmis (arsivlenmis) gonderilerin tenant bazinda
/// in-memory indeksi. Liste ekraninda DB-paged sonuclari arsivlenmis kayitlardan
/// hizlica filtrelemek icin kullanilir.
///
/// Arsiv durumu CargoShipment.Metadata["shipment.archived"] = "true" olarak
/// kalici saklanir; bu indeks sadece okuma cache'idir, yazimda repo guncellenir.
/// </summary>
public sealed class ShipmentArchiveIndex
{
    public const string ArchivedFlagKey = "shipment.archived";

    private readonly ConcurrentDictionary<string, HashSet<string>> _byTenant =
        new(StringComparer.OrdinalIgnoreCase);
    private readonly ConcurrentDictionary<string, DateTimeOffset> _loadedAt =
        new(StringComparer.OrdinalIgnoreCase);
    private readonly object _gate = new();

    /// <summary>
    /// Tenant icin arsivlenmis shipment referans setini doner.
    /// Cache'de yoksa repodan yukler.
    /// </summary>
    public async Task<HashSet<string>> GetAsync(IShipmentRepository repo, string tenantKey, CancellationToken ct)
    {
        if (_byTenant.TryGetValue(tenantKey, out var set))
            return set;

        var loaded = await ReloadAsync(repo, tenantKey, ct);
        return loaded;
    }

    /// <summary>Tenant'in arsiv setini repodan yeniden yukler.</summary>
    public async Task<HashSet<string>> ReloadAsync(IShipmentRepository repo, string tenantKey, CancellationToken ct)
    {
        var ships = await repo.ListAsync(tenantKey, ct);
        var set = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var s in ships)
        {
            if (s.Metadata.TryGetValue(ArchivedFlagKey, out var v) &&
                string.Equals(v, "true", StringComparison.OrdinalIgnoreCase))
            {
                set.Add(s.ShipmentReference);
            }
        }
        _byTenant[tenantKey] = set;
        _loadedAt[tenantKey] = DateTimeOffset.UtcNow;
        return set;
    }

    public void MarkArchived(string tenantKey, string shipmentReference)
    {
        lock (_gate)
        {
            var set = _byTenant.GetOrAdd(tenantKey, _ => new HashSet<string>(StringComparer.OrdinalIgnoreCase));
            set.Add(shipmentReference);
        }
    }

    public void MarkUnarchived(string tenantKey, string shipmentReference)
    {
        lock (_gate)
        {
            if (_byTenant.TryGetValue(tenantKey, out var set))
                set.Remove(shipmentReference);
        }
    }

    public bool IsArchived(string tenantKey, string shipmentReference)
        => _byTenant.TryGetValue(tenantKey, out var set) && set.Contains(shipmentReference);

    public int CountFor(string tenantKey)
        => _byTenant.TryGetValue(tenantKey, out var set) ? set.Count : 0;
}
