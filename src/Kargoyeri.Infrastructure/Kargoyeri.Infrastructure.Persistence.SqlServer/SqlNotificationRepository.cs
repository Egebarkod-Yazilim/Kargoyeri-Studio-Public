using System;
using System.Collections.Generic;
using System.Linq;
using System.Linq.Expressions;
using System.Threading;
using System.Threading.Tasks;
using Kargoyeri.Application.Abstractions.Persistence;
using Kargoyeri.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Kargoyeri.Infrastructure.Persistence.SqlServer;

internal sealed class SqlNotificationRepository : INotificationRepository
{
	private readonly KargoyeriDbContext _db;

	public SqlNotificationRepository(KargoyeriDbContext db)
	{
		_db = db;
	}

	public async Task AppendAsync(NotificationMessage notification, CancellationToken cancellationToken)
	{
		_db.Notifications.Add(notification);
		await Task.CompletedTask;
	}

	public async Task UpdateAsync(NotificationMessage notification, CancellationToken cancellationToken)
	{
		NotificationMessage notification2 = notification;
		NotificationMessage existing = await EntityFrameworkQueryableExtensions.FirstOrDefaultAsync<NotificationMessage>((IQueryable<NotificationMessage>)_db.Notifications, (Expression<Func<NotificationMessage, bool>>)((NotificationMessage x) => x.Id == notification2.Id), cancellationToken);
		if (existing != null)
		{
			existing.Status = notification2.Status;
			existing.ErrorMessage = notification2.ErrorMessage;
			existing.DeliveredAtUtc = notification2.DeliveredAtUtc;
		}
	}

	public async Task<NotificationMessage?> GetByIdAsync(Guid id, CancellationToken cancellationToken)
	{
		return await EntityFrameworkQueryableExtensions.FirstOrDefaultAsync<NotificationMessage>((IQueryable<NotificationMessage>)_db.Notifications, (Expression<Func<NotificationMessage, bool>>)((NotificationMessage x) => x.Id == id), cancellationToken);
	}

	public async Task<IReadOnlyCollection<NotificationMessage>> ListByTenantAsync(string tenantKey, CancellationToken cancellationToken)
	{
		string tenantKey2 = tenantKey;
		return (IReadOnlyCollection<NotificationMessage>)(object)(await EntityFrameworkQueryableExtensions.ToArrayAsync<NotificationMessage>((IQueryable<NotificationMessage>)(from x in (IQueryable<NotificationMessage>)_db.Notifications
			where x.TenantKey == tenantKey2
			orderby x.CreatedAtUtc descending
			select x), cancellationToken));
	}

	public async Task<IReadOnlyCollection<NotificationMessage>> ListByShipmentAsync(string tenantKey, string shipmentReference, CancellationToken cancellationToken)
	{
		string tenantKey2 = tenantKey;
		string shipmentReference2 = shipmentReference;
		return (IReadOnlyCollection<NotificationMessage>)(object)(await EntityFrameworkQueryableExtensions.ToArrayAsync<NotificationMessage>((IQueryable<NotificationMessage>)(from x in (IQueryable<NotificationMessage>)_db.Notifications
			where x.TenantKey == tenantKey2 && x.ShipmentReference == shipmentReference2
			orderby x.CreatedAtUtc descending
			select x), cancellationToken));
	}
}
