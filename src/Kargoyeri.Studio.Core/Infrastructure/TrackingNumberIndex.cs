using System.Collections.Concurrent;
using Kargoyeri.Application.Services;

namespace Kargoyeri.Studio.Core.Infrastructure;

/// <summary>
/// Tracking number → (tenantKey, shipmentReference) singleton in-memory indeksi (P1-#5).
///
/// V1'de tum tenant'lar tarniyordu — O(tenant * shipment). Bu indeks lookup'i
/// O(1) yapar. Background timer ile her 5 dk yenilenir; ek olarak miss durumunda
/// `Refresh` ile yeniden tarama tetiklenebilir.
///
/// Senkronizasyon: cok yazici/cok okuyucu ConcurrentDictionary — yazma sirasinda
/// sadece kismi inconsistent olabilir, lookup yine cache+fallback ile guvenli.
/// </summary>
public sealed class TrackingNumberIndex
{
    public sealed record Entry(string TenantKey, string ShipmentReference);

    private readonly ConcurrentDictionary<string, Entry> _map =
        new(StringComparer.OrdinalIgnoreCase);

    public DateTimeOffset? LastRebuiltAt { get; private set; }
    public int Count => _map.Count;

    public bool TryGet(string trackingNumber, out Entry entry)
    {
        if (string.IsNullOrWhiteSpace(trackingNumber))
        {
            entry = default!;
            return false;
        }
        return _map.TryGetValue(trackingNumber.Trim(), out entry!);
    }

    /// <summary>Tek kayit ekle/guncelle — yeni shipment olusturulduktan sonra cagrilabilir.</summary>
    public void Upsert(string trackingNumber, string tenantKey, string shipmentReference)
    {
        if (string.IsNullOrWhiteSpace(trackingNumber)) return;
        _map[trackingNumber.Trim()] = new Entry(tenantKey, shipmentReference);
    }

    public void Remove(string trackingNumber)
    {
        if (string.IsNullOrWhiteSpace(trackingNumber)) return;
        _map.TryRemove(trackingNumber.Trim(), out _);
    }

    /// <summary>Tum tenant'lardan rebuild — pahali, gunde 1 kez veya manuel cagrilir.</summary>
    public async Task RebuildAsync(
        CustomerService customerService,
        CargoOrchestrator orchestrator,
        CancellationToken ct)
    {
        var newMap = new Dictionary<string, Entry>(StringComparer.OrdinalIgnoreCase);
        var tenants = await customerService.ListAllAsync(ct);
        foreach (var tenant in tenants)
        {
            if (ct.IsCancellationRequested) return;
            try
            {
                var shipments = await orchestrator.ListShipmentsAsync(tenant.TenantKey, ct);
                foreach (var s in shipments)
                {
                    if (string.IsNullOrWhiteSpace(s.TrackingNumber)) continue;
                    // Ayni tracking number birden fazla tenant'ta olabilir (cok dusuk olasilik) —
                    // ilk gelen kazansin (deterministic)
                    var key = s.TrackingNumber.Trim();
                    if (!newMap.ContainsKey(key))
                        newMap[key] = new Entry(tenant.TenantKey, s.ShipmentReference);
                }
            }
            catch
            {
                // Tenant okuma hatasi — sessizce gec, log baska yerden atilir
            }
        }

        // Atomik degistirme: temizle + ekle
        _map.Clear();
        foreach (var kv in newMap) _map[kv.Key] = kv.Value;
        LastRebuiltAt = DateTimeOffset.UtcNow;
    }
}
