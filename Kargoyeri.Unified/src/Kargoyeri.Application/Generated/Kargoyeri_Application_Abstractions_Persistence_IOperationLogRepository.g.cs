// Generated from assembly metadata during repository recovery.
#nullable enable
namespace Kargoyeri.Application.Abstractions.Persistence;

public interface IOperationLogRepository
{
    global::System.Threading.Tasks.Task AppendAsync(global::Kargoyeri.Domain.Entities.ShipmentOperationLog log, global::System.Threading.CancellationToken cancellationToken);
    global::System.Threading.Tasks.Task<global::System.Collections.Generic.IReadOnlyCollection<global::Kargoyeri.Domain.Entities.ShipmentOperationLog>> ListByShipmentReferenceAsync(string tenantKey, string shipmentReference, global::System.Threading.CancellationToken cancellationToken);
}
