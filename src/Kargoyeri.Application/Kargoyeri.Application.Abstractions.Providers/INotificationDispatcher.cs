using System.Threading;
using System.Threading.Tasks;
using Kargoyeri.Domain.Entities;

namespace Kargoyeri.Application.Abstractions.Providers;

public interface INotificationDispatcher
{
	Task DispatchAsync(CustomerTenant customer, NotificationMessage notification, CancellationToken cancellationToken);
}
