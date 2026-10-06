using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Kargoyeri.Domain.Entities;

namespace Kargoyeri.Application.Abstractions.Persistence;

public interface IOperationLogRepository
{
	Task AppendAsync(ShipmentOperationLog log, CancellationToken cancellationToken);

	Task<IReadOnlyCollection<ShipmentOperationLog>> ListByShipmentReferenceAsync(string tenantKey, string shipmentReference, CancellationToken cancellationToken);
}
