using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Kargoyeri.Domain.Entities;
using Kargoyeri.Domain.Enums;

namespace Kargoyeri.Application.Abstractions.Persistence;

public interface IShipmentRepository
{
	Task UpsertAsync(CargoShipment shipment, CancellationToken cancellationToken);

	Task<CargoShipment?> GetByReferenceAsync(string shipmentReference, CancellationToken cancellationToken);

	Task<CargoShipment?> GetByTrackingNumberAsync(string trackingNumber, CancellationToken cancellationToken);

	Task<CargoShipment?> GetByIdempotencyKeyAsync(string tenantKey, string idempotencyKey, CancellationToken cancellationToken);

	Task<IReadOnlyCollection<CargoShipment>> ListAsync(string? tenantKey, CancellationToken cancellationToken);

	Task<(IReadOnlyCollection<CargoShipment> Items, int TotalCount)> ListPagedAsync(string tenantKey, string? search, ShipmentStatus? status, CargoProviderType? provider, int page, int pageSize, CancellationToken cancellationToken);

	Task<IReadOnlyCollection<CargoShipment>> GetRefreshCandidatesAsync(int take, CancellationToken cancellationToken);

	Task<IReadOnlyCollection<CargoShipment>> ListByDateRangeAsync(string tenantKey, DateTime fromUtc, DateTime toUtc, CancellationToken cancellationToken);
}
