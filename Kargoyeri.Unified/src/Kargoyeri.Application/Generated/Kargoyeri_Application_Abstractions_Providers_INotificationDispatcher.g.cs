// Generated from assembly metadata during repository recovery.
#nullable enable
namespace Kargoyeri.Application.Abstractions.Providers;

public interface INotificationDispatcher
{
    global::System.Threading.Tasks.Task DispatchAsync(global::Kargoyeri.Domain.Entities.CustomerTenant customer, global::Kargoyeri.Domain.Entities.NotificationMessage notification, global::System.Threading.CancellationToken cancellationToken);
}
