using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Kargoyeri.Application.Abstractions.Persistence;
using Kargoyeri.Domain.Entities;

namespace Kargoyeri.Infrastructure.Persistence;

internal sealed class JsonSessionNotificationRepository : INotificationRepository
{
	private readonly JsonDatabaseSession _session;

	public JsonSessionNotificationRepository(JsonDatabaseSession session)
	{
		_session = session;
	}

	public async Task AppendAsync(NotificationMessage notification, CancellationToken cancellationToken)
	{
		(await _session.GetDocumentAsync(cancellationToken)).Notifications.Add(notification);
	}

	public async Task UpdateAsync(NotificationMessage notification, CancellationToken cancellationToken)
	{
		NotificationMessage notification2 = notification;
		JsonDatabaseDocument document = await _session.GetDocumentAsync(cancellationToken);
		int index = document.Notifications.FindIndex((NotificationMessage x) => x.Id == notification2.Id);
		if (index >= 0)
		{
			document.Notifications[index] = notification2;
		}
	}

	public async Task<NotificationMessage?> GetByIdAsync(Guid id, CancellationToken cancellationToken)
	{
		return (await _session.GetDocumentAsync(cancellationToken)).Notifications.FirstOrDefault((NotificationMessage x) => x.Id == id);
	}

	public async Task<IReadOnlyCollection<NotificationMessage>> ListByTenantAsync(string tenantKey, CancellationToken cancellationToken)
	{
		string tenantKey2 = tenantKey;
		return (IReadOnlyCollection<NotificationMessage>)(object)(await _session.GetDocumentAsync(cancellationToken)).Notifications.Where((NotificationMessage x) => x.TenantKey == tenantKey2).ToArray();
	}

	public async Task<IReadOnlyCollection<NotificationMessage>> ListByShipmentAsync(string tenantKey, string shipmentReference, CancellationToken cancellationToken)
	{
		string tenantKey2 = tenantKey;
		string shipmentReference2 = shipmentReference;
		return (IReadOnlyCollection<NotificationMessage>)(object)(await _session.GetDocumentAsync(cancellationToken)).Notifications.Where((NotificationMessage x) => x.TenantKey == tenantKey2 && x.ShipmentReference == shipmentReference2).ToArray();
	}
}
