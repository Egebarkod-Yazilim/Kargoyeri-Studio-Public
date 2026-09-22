// Generated from assembly metadata during repository recovery.
#nullable enable
namespace Kargoyeri.Application.Services;

public sealed partial class NotificationService
{
    public NotificationService(global::Kargoyeri.Application.Abstractions.Persistence.INotificationRepository notificationRepository, global::Kargoyeri.Application.Abstractions.Providers.INotificationDispatcher notificationDispatcher, global::Kargoyeri.Application.Abstractions.Persistence.ICustomerRepository customerRepository, global::Kargoyeri.Application.Abstractions.Persistence.IUnitOfWork unitOfWork) { }
    public global::System.Threading.Tasks.Task PublishAsync(global::Kargoyeri.Domain.Entities.CustomerTenant customer, global::Kargoyeri.Domain.Entities.CargoShipment shipment, global::Kargoyeri.Domain.Enums.NotificationEventType eventType, string message, global::System.Threading.CancellationToken cancellationToken)
    {
        throw new global::System.NotImplementedException();
    }
    public global::System.Threading.Tasks.Task<global::System.Collections.Generic.IReadOnlyCollection<global::Kargoyeri.Contracts.Dtos.NotificationMessageDto>> ListAsync(string tenantKey, string shipmentReference, global::System.Threading.CancellationToken cancellationToken)
    {
        throw new global::System.NotImplementedException();
    }
    public global::System.Threading.Tasks.Task<bool> RetryAsync(global::System.Guid notificationId, string tenantKey, global::System.Threading.CancellationToken cancellationToken)
    {
        throw new global::System.NotImplementedException();
    }
}
