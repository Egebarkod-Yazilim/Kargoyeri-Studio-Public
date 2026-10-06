using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;
using Kargoyeri.Domain.Entities;

namespace Kargoyeri.Application.Abstractions.Persistence;

public interface INotificationRepository
{
	Task AppendAsync(NotificationMessage notification, CancellationToken cancellationToken);

	Task UpdateAsync(NotificationMessage notification, CancellationToken cancellationToken);

	Task<NotificationMessage?> GetByIdAsync(Guid id, CancellationToken cancellationToken);

	Task<IReadOnlyCollection<NotificationMessage>> ListByTenantAsync(string tenantKey, CancellationToken cancellationToken);

	Task<IReadOnlyCollection<NotificationMessage>> ListByShipmentAsync(string tenantKey, string shipmentReference, CancellationToken cancellationToken);
}
