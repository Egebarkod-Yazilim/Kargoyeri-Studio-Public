using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Kargoyeri.Application.Abstractions.Persistence;
using Kargoyeri.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Kargoyeri.Infrastructure.Persistence.SqlServer;

internal sealed class SqlOperationLogRepository : IOperationLogRepository
{
	private readonly KargoyeriDbContext _db;

	public SqlOperationLogRepository(KargoyeriDbContext db)
	{
		_db = db;
	}

	public async Task AppendAsync(ShipmentOperationLog log, CancellationToken cancellationToken)
	{
		_db.OperationLogs.Add(log);
		await Task.CompletedTask;
	}

	public async Task<IReadOnlyCollection<ShipmentOperationLog>> ListByShipmentReferenceAsync(string tenantKey, string shipmentReference, CancellationToken cancellationToken)
	{
		string tenantKey2 = tenantKey;
		string shipmentReference2 = shipmentReference;
		return (IReadOnlyCollection<ShipmentOperationLog>)(object)(await EntityFrameworkQueryableExtensions.ToArrayAsync<ShipmentOperationLog>((IQueryable<ShipmentOperationLog>)(from x in (IQueryable<ShipmentOperationLog>)_db.OperationLogs
			where x.TenantKey == tenantKey2 && x.ShipmentReference == shipmentReference2
			orderby x.OccurredAtUtc descending
			select x), cancellationToken));
	}
}
