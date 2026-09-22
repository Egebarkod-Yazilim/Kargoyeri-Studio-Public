using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Kargoyeri.Application.Abstractions.Persistence;
using Kargoyeri.Domain.Entities;

namespace Kargoyeri.Infrastructure.Persistence;

internal sealed class InMemoryNotificationRepository : INotificationRepository
{
	private readonly List<NotificationMessage> _notifications = new List<NotificationMessage>();

	private readonly object _lock = new object();

	public Task AppendAsync(NotificationMessage notification, CancellationToken cancellationToken)
	{
		lock (_lock)
		{
			_notifications.Add(notification);
		}
		return Task.CompletedTask;
	}

	public Task UpdateAsync(NotificationMessage notification, CancellationToken cancellationToken)
	{
		NotificationMessage notification2 = notification;
		lock (_lock)
		{
			int num = _notifications.FindIndex((NotificationMessage x) => x.Id == notification2.Id);
			if (num >= 0)
			{
				_notifications[num] = notification2;
			}
		}
		return Task.CompletedTask;
	}

	public Task<NotificationMessage?> GetByIdAsync(Guid id, CancellationToken cancellationToken)
	{
		lock (_lock)
		{
			return Task.FromResult(_notifications.FirstOrDefault((NotificationMessage x) => x.Id == id));
		}
	}

	public Task<IReadOnlyCollection<NotificationMessage>> ListByTenantAsync(string tenantKey, CancellationToken cancellationToken)
	{
		string tenantKey2 = tenantKey;
		lock (_lock)
		{
			return Task.FromResult((IReadOnlyCollection<NotificationMessage>)(object)_notifications.Where((NotificationMessage x) => x.TenantKey == tenantKey2).ToArray());
		}
	}

	public Task<IReadOnlyCollection<NotificationMessage>> ListByShipmentAsync(string tenantKey, string shipmentReference, CancellationToken cancellationToken)
	{
		string tenantKey2 = tenantKey;
		string shipmentReference2 = shipmentReference;
		lock (_lock)
		{
			return Task.FromResult((IReadOnlyCollection<NotificationMessage>)(object)_notifications.Where((NotificationMessage x) => x.TenantKey == tenantKey2 && x.ShipmentReference == shipmentReference2).ToArray());
		}
	}
}
