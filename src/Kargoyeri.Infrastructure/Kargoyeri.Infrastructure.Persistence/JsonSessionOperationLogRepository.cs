using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Kargoyeri.Application.Abstractions.Persistence;
using Kargoyeri.Domain.Entities;

namespace Kargoyeri.Infrastructure.Persistence;

internal sealed class JsonSessionOperationLogRepository : IOperationLogRepository
{
	private readonly JsonDatabaseSession _session;

	public JsonSessionOperationLogRepository(JsonDatabaseSession session)
	{
		_session = session;
	}

	public async Task AppendAsync(ShipmentOperationLog log, CancellationToken cancellationToken)
	{
		(await _session.GetDocumentAsync(cancellationToken)).Logs.Add(log);
	}

	public async Task<IReadOnlyCollection<ShipmentOperationLog>> ListByShipmentReferenceAsync(string tenantKey, string shipmentReference, CancellationToken cancellationToken)
	{
		string tenantKey2 = tenantKey;
		string shipmentReference2 = shipmentReference;
		return (IReadOnlyCollection<ShipmentOperationLog>)(object)(await _session.GetDocumentAsync(cancellationToken)).Logs.Where((ShipmentOperationLog x) => x.TenantKey == tenantKey2 && x.ShipmentReference == shipmentReference2).ToArray();
	}
}
