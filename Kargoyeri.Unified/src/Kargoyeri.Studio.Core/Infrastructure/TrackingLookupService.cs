using Kargoyeri.Application.Services;
using Kargoyeri.Contracts.Dtos;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Logging;

namespace Kargoyeri.Studio.Core.Infrastructure;

/// <summary>
/// Public tracking sayfasi icin tracking number → (tenant, shipment) cozumlemesi yapar.
/// Tum tenant'lari tarar (V1 brute force). Sonuclar kisa sureli cache'lenir.
/// </summary>
public sealed class TrackingLookupService
{
    private static readonly TimeSpan PositiveCacheTtl = TimeSpan.FromMinutes(2);
    private static readonly TimeSpan NegativeCacheTtl = TimeSpan.FromSeconds(30);

    private readonly CargoOrchestrator _orchestrator;
    private readonly CustomerService _customerService;
    private readonly TrackingNumberIndex _index;
    private readonly IMemoryCache _cache;
    private readonly ILogger<TrackingLookupService> _logger;

    public TrackingLookupService(
        CargoOrchestrator orchestrator,
        CustomerService customerService,
        TrackingNumberIndex index,
        IMemoryCache cache,
        ILogger<TrackingLookupService> logger)
    {
        _orchestrator = orchestrator;
        _customerService = customerService;
        _index = index;
        _cache = cache;
        _logger = logger;
    }

    public async Task<TrackingLookupResult?> FindAsync(string trackingNumber, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(trackingNumber)) return null;
        var normalizedTracking = trackingNumber.Trim();
        var key = $"track:{normalizedTracking}";
        if (_cache.TryGetValue<TrackingLookupResult?>(key, out var cached))
        {
            return cached;
        }

        TrackingLookupResult? hit = null;
        try
        {
            if (_index.TryGet(normalizedTracking, out var entry))
            {
                hit = await TryLoadIndexedAsync(normalizedTracking, entry, cancellationToken);
            }

            if (hit is null)
            {
                hit = await TryLoadRepositoryLookupAsync(normalizedTracking, cancellationToken);
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Tracking lookup hatasi: {Tracking}", normalizedTracking);
        }

        _cache.Set(key, hit, hit is null ? NegativeCacheTtl : PositiveCacheTtl);
        return hit;
    }

    private async Task<TrackingLookupResult?> TryLoadIndexedAsync(
        string trackingNumber,
        TrackingNumberIndex.Entry entry,
        CancellationToken cancellationToken)
    {
        var tenant = await _customerService.GetProfileAsync(entry.TenantKey, cancellationToken);
        if (tenant is null)
        {
            _index.Remove(trackingNumber);
            return null;
        }

        var detail = await _orchestrator.GetShipmentAsync(entry.ShipmentReference, entry.TenantKey, cancellationToken);
        if (detail is null)
        {
            _index.Remove(trackingNumber);
            return null;
        }

        if (string.IsNullOrWhiteSpace(detail.TrackingNumber) ||
            !string.Equals(detail.TrackingNumber.Trim(), trackingNumber, StringComparison.OrdinalIgnoreCase))
        {
            _index.Remove(trackingNumber);
            return null;
        }

        var logs = await _orchestrator.ListLogsAsync(entry.ShipmentReference, entry.TenantKey, cancellationToken);
        return new TrackingLookupResult(tenant, detail, logs);
    }

    private async Task<TrackingLookupResult?> TryLoadRepositoryLookupAsync(
        string trackingNumber,
        CancellationToken cancellationToken)
    {
        var detail = await _orchestrator.GetShipmentByTrackingNumberAsync(trackingNumber, cancellationToken);
        if (detail is null)
        {
            return null;
        }

        var tenant = await _customerService.GetProfileAsync(detail.CustomerCode, cancellationToken);
        if (tenant is null)
        {
            return null;
        }

        var logs = await _orchestrator.ListLogsAsync(detail.ShipmentReference, detail.CustomerCode, cancellationToken);
        _index.Upsert(trackingNumber, detail.CustomerCode, detail.ShipmentReference);
        return new TrackingLookupResult(tenant, detail, logs);
    }

    private static ShipmentDetailResponse ToDetail(ShipmentListItemResponse item) => new()
    {
        ShipmentReference = item.ShipmentReference,
        CustomerCode = item.CustomerCode,
        OrderReference = item.OrderReference,
        Provider = item.Provider,
        Status = item.Status,
        TrackingNumber = item.TrackingNumber,
        CreatedAtUtc = item.CreatedAtUtc,
        UpdatedAtUtc = item.UpdatedAtUtc
    };
}

public sealed record TrackingLookupResult(
    CustomerProfileDto Tenant,
    ShipmentDetailResponse Shipment,
    IReadOnlyCollection<ShipmentOperationLogDto> Logs);
