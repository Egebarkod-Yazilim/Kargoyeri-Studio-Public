// Generated from assembly metadata during repository recovery.
#nullable enable
namespace Kargoyeri.Application.Abstractions.Persistence;

public interface INotificationRepository
{
    global::System.Threading.Tasks.Task AppendAsync(global::Kargoyeri.Domain.Entities.NotificationMessage notification, global::System.Threading.CancellationToken cancellationToken);
    global::System.Threading.Tasks.Task UpdateAsync(global::Kargoyeri.Domain.Entities.NotificationMessage notification, global::System.Threading.CancellationToken cancellationToken);
    global::System.Threading.Tasks.Task<global::Kargoyeri.Domain.Entities.NotificationMessage> GetByIdAsync(global::System.Guid id, global::System.Threading.CancellationToken cancellationToken);
    global::System.Threading.Tasks.Task<global::System.Collections.Generic.IReadOnlyCollection<global::Kargoyeri.Domain.Entities.NotificationMessage>> ListByTenantAsync(string tenantKey, global::System.Threading.CancellationToken cancellationToken);
    global::System.Threading.Tasks.Task<global::System.Collections.Generic.IReadOnlyCollection<global::Kargoyeri.Domain.Entities.NotificationMessage>> ListByShipmentAsync(string tenantKey, string shipmentReference, global::System.Threading.CancellationToken cancellationToken);
}
