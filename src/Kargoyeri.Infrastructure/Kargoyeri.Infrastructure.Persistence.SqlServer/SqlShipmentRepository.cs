using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading;
using System.Threading.Tasks;
using Kargoyeri.Application.Abstractions.Persistence;
using Kargoyeri.Domain.Entities;
using Kargoyeri.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;

namespace Kargoyeri.Infrastructure.Persistence.SqlServer;

internal sealed class SqlShipmentRepository : IShipmentRepository
{
	private readonly KargoyeriDbContext _db;

	public SqlShipmentRepository(KargoyeriDbContext db)
	{
		_db = db;
	}

	public async Task UpsertAsync(CargoShipment shipment, CancellationToken cancellationToken)
	{
		CargoShipment shipment2 = shipment;
		CargoShipment existing = await EntityFrameworkQueryableExtensions.FirstOrDefaultAsync<CargoShipment>((IQueryable<CargoShipment>)_db.Shipments, (Expression<Func<CargoShipment, bool>>)((CargoShipment x) => x.ShipmentReference == shipment2.ShipmentReference), cancellationToken);
		if (existing == null)
		{
			_db.Shipments.Add(shipment2);
			return;
		}
		((EntityEntry)((DbContext)_db).Entry<CargoShipment>(existing)).CurrentValues.SetValues((object)shipment2);
		existing.Sender = shipment2.Sender;
		existing.Recipient = shipment2.Recipient;
		existing.Packages = shipment2.Packages;
		existing.Metadata = shipment2.Metadata;
	}

	public async Task<CargoShipment?> GetByReferenceAsync(string shipmentReference, CancellationToken cancellationToken)
	{
		string shipmentReference2 = shipmentReference;
		return await EntityFrameworkQueryableExtensions.FirstOrDefaultAsync<CargoShipment>((IQueryable<CargoShipment>)_db.Shipments, (Expression<Func<CargoShipment, bool>>)((CargoShipment x) => x.ShipmentReference == shipmentReference2), cancellationToken);
	}

	public async Task<CargoShipment?> GetByTrackingNumberAsync(string trackingNumber, CancellationToken cancellationToken)
	{
		string trackingNumber2 = trackingNumber;
		return await EntityFrameworkQueryableExtensions.FirstOrDefaultAsync<CargoShipment>((IQueryable<CargoShipment>)(from x in (IQueryable<CargoShipment>)_db.Shipments
			where x.TrackingNumber == trackingNumber2
			orderby x.UpdatedAtUtc descending
			select x), cancellationToken);
	}

	public async Task<CargoShipment?> GetByIdempotencyKeyAsync(string tenantKey, string idempotencyKey, CancellationToken cancellationToken)
	{
		string tenantKey2 = tenantKey;
		string idempotencyKey2 = idempotencyKey;
		return await EntityFrameworkQueryableExtensions.FirstOrDefaultAsync<CargoShipment>((IQueryable<CargoShipment>)_db.Shipments, (Expression<Func<CargoShipment, bool>>)((CargoShipment x) => x.TenantKey == tenantKey2 && x.IdempotencyKey == idempotencyKey2), cancellationToken);
	}

	public async Task<IReadOnlyCollection<CargoShipment>> ListAsync(string? tenantKey, CancellationToken cancellationToken)
	{
		string tenantKey2 = tenantKey;
		IQueryable<CargoShipment> query = _db.Shipments.AsQueryable();
		if (!string.IsNullOrWhiteSpace(tenantKey2))
		{
			query = query.Where((CargoShipment x) => x.TenantKey == tenantKey2);
		}
		return (IReadOnlyCollection<CargoShipment>)(object)(await EntityFrameworkQueryableExtensions.ToArrayAsync<CargoShipment>((IQueryable<CargoShipment>)query.OrderByDescending((CargoShipment x) => x.UpdatedAtUtc), cancellationToken));
	}

	public async Task<(IReadOnlyCollection<CargoShipment> Items, int TotalCount)> ListPagedAsync(string tenantKey, string? search, ShipmentStatus? status, CargoProviderType? provider, int page, int pageSize, CancellationToken cancellationToken)
	{
		string tenantKey2 = tenantKey;
		IQueryable<CargoShipment> query = ((IQueryable<CargoShipment>)_db.Shipments).Where((CargoShipment x) => x.TenantKey == tenantKey2);
		if (!string.IsNullOrWhiteSpace(search))
		{
			string s = search.Trim();
			query = query.Where((CargoShipment x) => x.ShipmentReference.Contains(s) || x.OrderReference.Contains(s) || (x.TrackingNumber != null && x.TrackingNumber.Contains(s)));
		}
		if (status.HasValue)
		{
			query = query.Where((CargoShipment x) => (int)x.Status == (int)((ShipmentStatus?)status).Value);
		}
		if (provider.HasValue)
		{
			query = query.Where((CargoShipment x) => (int)x.Provider == (int)((CargoProviderType?)provider).Value);
		}
		return new ValueTuple<IReadOnlyCollection<CargoShipment>, int>(item2: await EntityFrameworkQueryableExtensions.CountAsync<CargoShipment>(query, cancellationToken), item1: (IReadOnlyCollection<CargoShipment>)(object)(await EntityFrameworkQueryableExtensions.ToArrayAsync<CargoShipment>(query.OrderByDescending((CargoShipment x) => x.UpdatedAtUtc).Skip((Math.Max(1, page) - 1) * pageSize).Take(pageSize), cancellationToken)));
	}

	public async Task<IReadOnlyCollection<CargoShipment>> GetRefreshCandidatesAsync(int take, CancellationToken cancellationToken)
	{
		return (IReadOnlyCollection<CargoShipment>)(object)(await EntityFrameworkQueryableExtensions.ToArrayAsync<CargoShipment>((from x in (IQueryable<CargoShipment>)_db.Shipments
			where (int)x.Status == 0 || (int)x.Status == 2 || (int)x.Status == 4
			orderby x.LastStatusCheckAtUtc ?? x.CreatedAtUtc
			select x).Take(take), cancellationToken));
	}

	public async Task<IReadOnlyCollection<CargoShipment>> ListByDateRangeAsync(string tenantKey, DateTime fromUtc, DateTime toUtc, CancellationToken cancellationToken)
	{
		string tenantKey2 = tenantKey;
		return (IReadOnlyCollection<CargoShipment>)(object)(await EntityFrameworkQueryableExtensions.ToArrayAsync<CargoShipment>((IQueryable<CargoShipment>)(from x in (IQueryable<CargoShipment>)_db.Shipments
			where x.TenantKey == tenantKey2 && x.CreatedAtUtc >= (DateTimeOffset)fromUtc && x.CreatedAtUtc <= (DateTimeOffset)toUtc
			orderby x.CreatedAtUtc descending
			select x), cancellationToken));
	}
}
