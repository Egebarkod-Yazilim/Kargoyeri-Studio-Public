// Generated from assembly metadata during repository recovery.
#nullable enable
namespace Kargoyeri.Application.Abstractions.Persistence;

public interface IShipmentRepository
{
    global::System.Threading.Tasks.Task UpsertAsync(global::Kargoyeri.Domain.Entities.CargoShipment shipment, global::System.Threading.CancellationToken cancellationToken);
    global::System.Threading.Tasks.Task<global::Kargoyeri.Domain.Entities.CargoShipment> GetByReferenceAsync(string shipmentReference, global::System.Threading.CancellationToken cancellationToken);
    global::System.Threading.Tasks.Task<global::Kargoyeri.Domain.Entities.CargoShipment> GetByTrackingNumberAsync(string trackingNumber, global::System.Threading.CancellationToken cancellationToken);
    global::System.Threading.Tasks.Task<global::Kargoyeri.Domain.Entities.CargoShipment> GetByIdempotencyKeyAsync(string tenantKey, string idempotencyKey, global::System.Threading.CancellationToken cancellationToken);
    global::System.Threading.Tasks.Task<global::System.Collections.Generic.IReadOnlyCollection<global::Kargoyeri.Domain.Entities.CargoShipment>> ListAsync(string tenantKey, global::System.Threading.CancellationToken cancellationToken);
    global::System.Threading.Tasks.Task<global::System.ValueTuple<global::System.Collections.Generic.IReadOnlyCollection<global::Kargoyeri.Domain.Entities.CargoShipment>, int>> ListPagedAsync(string tenantKey, string search, global::Kargoyeri.Domain.Enums.ShipmentStatus? status, global::Kargoyeri.Domain.Enums.CargoProviderType? provider, int page, int pageSize, global::System.Threading.CancellationToken cancellationToken);
    global::System.Threading.Tasks.Task<global::System.Collections.Generic.IReadOnlyCollection<global::Kargoyeri.Domain.Entities.CargoShipment>> GetRefreshCandidatesAsync(int take, global::System.Threading.CancellationToken cancellationToken);
    global::System.Threading.Tasks.Task<global::System.Collections.Generic.IReadOnlyCollection<global::Kargoyeri.Domain.Entities.CargoShipment>> ListByDateRangeAsync(string tenantKey, global::System.DateTime fromUtc, global::System.DateTime toUtc, global::System.Threading.CancellationToken cancellationToken);
}
