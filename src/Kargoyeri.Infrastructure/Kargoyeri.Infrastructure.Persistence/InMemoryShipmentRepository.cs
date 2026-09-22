using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Kargoyeri.Application.Abstractions.Persistence;
using Kargoyeri.Domain.Entities;
using Kargoyeri.Domain.Enums;

namespace Kargoyeri.Infrastructure.Persistence;

internal sealed class InMemoryShipmentRepository : IShipmentRepository
{
	private readonly List<CargoShipment> _shipments = new List<CargoShipment>();

	private readonly object _lock = new object();

	public Task UpsertAsync(CargoShipment shipment, CancellationToken cancellationToken)
	{
		CargoShipment shipment2 = shipment;
		lock (_lock)
		{
			int num = _shipments.FindIndex((CargoShipment x) => x.ShipmentReference == shipment2.ShipmentReference);
			if (num >= 0)
			{
				_shipments[num] = shipment2;
			}
			else
			{
				_shipments.Add(shipment2);
			}
		}
		return Task.CompletedTask;
	}

	public Task<CargoShipment?> GetByReferenceAsync(string shipmentReference, CancellationToken cancellationToken)
	{
		string shipmentReference2 = shipmentReference;
		lock (_lock)
		{
			return Task.FromResult(_shipments.FirstOrDefault((CargoShipment x) => x.ShipmentReference == shipmentReference2));
		}
	}

	public Task<CargoShipment?> GetByTrackingNumberAsync(string trackingNumber, CancellationToken cancellationToken)
	{
		string trackingNumber2 = trackingNumber;
		lock (_lock)
		{
			return Task.FromResult((from x in _shipments
				where string.Equals(x.TrackingNumber, trackingNumber2, StringComparison.OrdinalIgnoreCase)
				orderby x.UpdatedAtUtc descending
				select x).FirstOrDefault());
		}
	}

	public Task<CargoShipment?> GetByIdempotencyKeyAsync(string tenantKey, string idempotencyKey, CancellationToken cancellationToken)
	{
		string tenantKey2 = tenantKey;
		string idempotencyKey2 = idempotencyKey;
		lock (_lock)
		{
			return Task.FromResult(_shipments.FirstOrDefault((CargoShipment x) => string.Equals(x.TenantKey, tenantKey2, StringComparison.OrdinalIgnoreCase) && string.Equals(x.IdempotencyKey, idempotencyKey2, StringComparison.OrdinalIgnoreCase)));
		}
	}

	public Task<IReadOnlyCollection<CargoShipment>> ListAsync(string? tenantKey, CancellationToken cancellationToken)
	{
		string tenantKey2 = tenantKey;
		lock (_lock)
		{
			CargoShipment[] result = (string.IsNullOrWhiteSpace(tenantKey2) ? _shipments.ToArray() : _shipments.Where((CargoShipment x) => string.Equals(x.TenantKey, tenantKey2, StringComparison.OrdinalIgnoreCase)).ToArray());
			return Task.FromResult((IReadOnlyCollection<CargoShipment>)(object)result);
		}
	}

	public Task<(IReadOnlyCollection<CargoShipment> Items, int TotalCount)> ListPagedAsync(string tenantKey, string? search, ShipmentStatus? status, CargoProviderType? provider, int page, int pageSize, CancellationToken cancellationToken)
	{
		string tenantKey2 = tenantKey;
		lock (_lock)
		{
			IEnumerable<CargoShipment> source = _shipments.Where((CargoShipment x) => string.Equals(x.TenantKey, tenantKey2, StringComparison.OrdinalIgnoreCase));
			if (!string.IsNullOrWhiteSpace(search))
			{
				string s = search.Trim();
				source = source.Where((CargoShipment x) => x.ShipmentReference.Contains(s, StringComparison.OrdinalIgnoreCase) || x.OrderReference.Contains(s, StringComparison.OrdinalIgnoreCase) || (x.TrackingNumber?.Contains(s, StringComparison.OrdinalIgnoreCase) ?? false));
			}
			if (status.HasValue)
			{
				source = source.Where((CargoShipment x) => x.Status == status.Value);
			}
			if (provider.HasValue)
			{
				source = source.Where((CargoShipment x) => x.Provider == provider.Value);
			}
			List<CargoShipment> list = source.OrderByDescending((CargoShipment x) => x.UpdatedAtUtc).ToList();
			int count = list.Count;
			CargoShipment[] item = list.Skip((Math.Max(1, page) - 1) * pageSize).Take(pageSize).ToArray();
			return Task.FromResult(((IReadOnlyCollection<CargoShipment>)(object)item, count));
		}
	}

	public Task<IReadOnlyCollection<CargoShipment>> GetRefreshCandidatesAsync(int take, CancellationToken cancellationToken)
	{
		lock (_lock)
		{
			CargoShipment[] result = (from x in _shipments.Where(delegate(CargoShipment x)
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
			return Task.FromResult((IReadOnlyCollection<CargoShipment>)(object)result);
		}
	}

	public Task<IReadOnlyCollection<CargoShipment>> ListByDateRangeAsync(string tenantKey, DateTime fromUtc, DateTime toUtc, CancellationToken cancellationToken)
	{
		string tenantKey2 = tenantKey;
		lock (_lock)
		{
			CargoShipment[] result = (from x in _shipments
				where string.Equals(x.TenantKey, tenantKey2, StringComparison.OrdinalIgnoreCase) && x.CreatedAtUtc >= fromUtc && x.CreatedAtUtc <= toUtc
				orderby x.CreatedAtUtc descending
				select x).ToArray();
			return Task.FromResult((IReadOnlyCollection<CargoShipment>)(object)result);
		}
	}
}
