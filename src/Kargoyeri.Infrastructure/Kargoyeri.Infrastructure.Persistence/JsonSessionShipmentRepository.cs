using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Kargoyeri.Application.Abstractions.Persistence;
using Kargoyeri.Domain.Entities;
using Kargoyeri.Domain.Enums;

namespace Kargoyeri.Infrastructure.Persistence;

internal sealed class JsonSessionShipmentRepository : IShipmentRepository
{
	private readonly JsonDatabaseSession _session;

	public JsonSessionShipmentRepository(JsonDatabaseSession session)
	{
		_session = session;
	}

	public async Task UpsertAsync(CargoShipment shipment, CancellationToken cancellationToken)
	{
		CargoShipment shipment2 = shipment;
		JsonDatabaseDocument document = await _session.GetDocumentAsync(cancellationToken);
		int index = document.Shipments.FindIndex((CargoShipment x) => x.ShipmentReference == shipment2.ShipmentReference);
		if (index >= 0)
		{
			document.Shipments[index] = shipment2;
		}
		else
		{
			document.Shipments.Add(shipment2);
		}
	}

	public async Task<CargoShipment?> GetByReferenceAsync(string shipmentReference, CancellationToken cancellationToken)
	{
		string shipmentReference2 = shipmentReference;
		return (await _session.GetDocumentAsync(cancellationToken)).Shipments.FirstOrDefault((CargoShipment x) => x.ShipmentReference == shipmentReference2);
	}

	public async Task<CargoShipment?> GetByTrackingNumberAsync(string trackingNumber, CancellationToken cancellationToken)
	{
		string trackingNumber2 = trackingNumber;
		return (from x in (await _session.GetDocumentAsync(cancellationToken)).Shipments
			where string.Equals(x.TrackingNumber, trackingNumber2, StringComparison.OrdinalIgnoreCase)
			orderby x.UpdatedAtUtc descending
			select x).FirstOrDefault();
	}

	public async Task<CargoShipment?> GetByIdempotencyKeyAsync(string tenantKey, string idempotencyKey, CancellationToken cancellationToken)
	{
		string tenantKey2 = tenantKey;
		string idempotencyKey2 = idempotencyKey;
		return (await _session.GetDocumentAsync(cancellationToken)).Shipments.FirstOrDefault((CargoShipment x) => string.Equals(x.TenantKey, tenantKey2, StringComparison.OrdinalIgnoreCase) && string.Equals(x.IdempotencyKey, idempotencyKey2, StringComparison.OrdinalIgnoreCase));
	}

	public async Task<IReadOnlyCollection<CargoShipment>> ListAsync(string? tenantKey, CancellationToken cancellationToken)
	{
		string tenantKey2 = tenantKey;
		JsonDatabaseDocument document = await _session.GetDocumentAsync(cancellationToken);
		return string.IsNullOrWhiteSpace(tenantKey2) ? ((IReadOnlyCollection<CargoShipment>)(object)document.Shipments.ToArray()) : ((IReadOnlyCollection<CargoShipment>)(object)document.Shipments.Where((CargoShipment x) => string.Equals(x.TenantKey, tenantKey2, StringComparison.OrdinalIgnoreCase)).ToArray());
	}

	public async Task<(IReadOnlyCollection<CargoShipment> Items, int TotalCount)> ListPagedAsync(string tenantKey, string? search, ShipmentStatus? status, CargoProviderType? provider, int page, int pageSize, CancellationToken cancellationToken)
	{
		string tenantKey2 = tenantKey;
		IEnumerable<CargoShipment> q = (await _session.GetDocumentAsync(cancellationToken)).Shipments.Where((CargoShipment x) => string.Equals(x.TenantKey, tenantKey2, StringComparison.OrdinalIgnoreCase));
		if (!string.IsNullOrWhiteSpace(search))
		{
			string s = search.Trim();
			q = q.Where((CargoShipment x) => x.ShipmentReference.Contains(s, StringComparison.OrdinalIgnoreCase) || x.OrderReference.Contains(s, StringComparison.OrdinalIgnoreCase) || (x.TrackingNumber?.Contains(s, StringComparison.OrdinalIgnoreCase) ?? false));
		}
		if (status.HasValue)
		{
			q = q.Where((CargoShipment x) => x.Status == status.Value);
		}
		if (provider.HasValue)
		{
			q = q.Where((CargoShipment x) => x.Provider == provider.Value);
		}
		List<CargoShipment> list = q.OrderByDescending((CargoShipment x) => x.UpdatedAtUtc).ToList();
		int total = list.Count;
		CargoShipment[] items = list.Skip((Math.Max(1, page) - 1) * pageSize).Take(pageSize).ToArray();
		return (Items: (IReadOnlyCollection<CargoShipment>)(object)items, TotalCount: total);
	}

	public async Task<IReadOnlyCollection<CargoShipment>> GetRefreshCandidatesAsync(int take, CancellationToken cancellationToken)
	{
		return (IReadOnlyCollection<CargoShipment>)(object)(from x in (await _session.GetDocumentAsync(cancellationToken)).Shipments.Where(delegate(CargoShipment x)
			{
				switch (x.Status)
				{
				case ShipmentStatus.Pending:
				case ShipmentStatus.ProviderAccepted:
				case ShipmentStatus.InTransit:
					return true;
				default:
					return false;
				}
			})
			orderby x.LastStatusCheckAtUtc ?? x.CreatedAtUtc
			select x).Take(take).ToArray();
	}

	public async Task<IReadOnlyCollection<CargoShipment>> ListByDateRangeAsync(string tenantKey, DateTime fromUtc, DateTime toUtc, CancellationToken cancellationToken)
	{
		string tenantKey2 = tenantKey;
		return (IReadOnlyCollection<CargoShipment>)(object)(from x in (await _session.GetDocumentAsync(cancellationToken)).Shipments
			where string.Equals(x.TenantKey, tenantKey2, StringComparison.OrdinalIgnoreCase) && x.CreatedAtUtc >= fromUtc && x.CreatedAtUtc <= toUtc
			orderby x.CreatedAtUtc descending
			select x).ToArray();
	}
}
