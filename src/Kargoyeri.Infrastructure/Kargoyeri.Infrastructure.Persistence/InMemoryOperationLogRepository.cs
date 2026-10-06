using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Kargoyeri.Application.Abstractions.Persistence;
using Kargoyeri.Domain.Entities;

namespace Kargoyeri.Infrastructure.Persistence;

internal sealed class InMemoryOperationLogRepository : IOperationLogRepository
{
	private readonly List<ShipmentOperationLog> _logs = new List<ShipmentOperationLog>();

	private readonly object _lock = new object();

	public Task AppendAsync(ShipmentOperationLog log, CancellationToken cancellationToken)
	{
		lock (_lock)
		{
			_logs.Add(log);
		}
		return Task.CompletedTask;
	}

	public Task<IReadOnlyCollection<ShipmentOperationLog>> ListByShipmentReferenceAsync(string tenantKey, string shipmentReference, CancellationToken cancellationToken)
	{
		string tenantKey2 = tenantKey;
		string shipmentReference2 = shipmentReference;
		lock (_lock)
		{
			return Task.FromResult((IReadOnlyCollection<ShipmentOperationLog>)(object)_logs.Where((ShipmentOperationLog x) => x.TenantKey == tenantKey2 && x.ShipmentReference == shipmentReference2).ToArray());
		}
	}
}
