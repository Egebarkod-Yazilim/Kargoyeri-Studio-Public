using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Kargoyeri.Application.Abstractions.Persistence;
using Kargoyeri.Application.Abstractions.Providers;
using Kargoyeri.Contracts.Dtos;
using Kargoyeri.Contracts.Enums;
using Kargoyeri.Domain.Entities;
using Kargoyeri.Domain.Enums;
using Kargoyeri.Domain.ValueObjects;

namespace Kargoyeri.Application.Services;

public sealed class NotificationService
{
	private readonly INotificationRepository _notificationRepository;

	private readonly INotificationDispatcher _notificationDispatcher;

	private readonly ICustomerRepository _customerRepository;

	private readonly IUnitOfWork _unitOfWork;

	public NotificationService(INotificationRepository notificationRepository, INotificationDispatcher notificationDispatcher, ICustomerRepository customerRepository, IUnitOfWork unitOfWork)
	{
		_notificationRepository = notificationRepository;
		_notificationDispatcher = notificationDispatcher;
		_customerRepository = customerRepository;
		_unitOfWork = unitOfWork;
	}

	public async Task PublishAsync(CustomerTenant customer, CargoShipment shipment, NotificationEventType eventType, string message, CancellationToken cancellationToken)
	{
		string subject = $"{customer.Name} - {shipment.ShipmentReference} - {eventType}";
		foreach (NotificationTarget target in customer.NotificationTargets.Where((NotificationTarget x) => x.IsEnabled))
		{
			NotificationMessage notification = new NotificationMessage
			{
				TenantKey = customer.TenantKey,
				ShipmentReference = shipment.ShipmentReference,
				EventType = eventType,
				Channel = target.Channel,
				Address = target.Address,
				Subject = subject,
				Body = message,
				CreatedAtUtc = DateTimeOffset.UtcNow
			};
			await _notificationRepository.AppendAsync(notification, cancellationToken);
			await _notificationDispatcher.DispatchAsync(customer, notification, cancellationToken);
			await _notificationRepository.UpdateAsync(notification, cancellationToken);
		}
		await _unitOfWork.CommitAsync(cancellationToken);
	}

	public async Task<IReadOnlyCollection<NotificationMessageDto>> ListAsync(string tenantKey, string? shipmentReference, CancellationToken cancellationToken)
	{
		IReadOnlyCollection<NotificationMessage> readOnlyCollection = ((!string.IsNullOrWhiteSpace(shipmentReference)) ? (await _notificationRepository.ListByShipmentAsync(tenantKey, shipmentReference, cancellationToken)) : (await _notificationRepository.ListByTenantAsync(tenantKey, cancellationToken)));
		IReadOnlyCollection<NotificationMessage> notifications = readOnlyCollection;
		return (IReadOnlyCollection<NotificationMessageDto>)(object)(from x in notifications
			orderby x.CreatedAtUtc descending
			select new NotificationMessageDto
			{
				Id = x.Id,
				CustomerCode = x.TenantKey,
				ShipmentReference = x.ShipmentReference,
				EventType = (NotificationEventTypeDto)x.EventType,
				Channel = (NotificationChannelDto)x.Channel,
				Status = (NotificationDeliveryStatusDto)x.Status,
				Address = x.Address,
				Subject = x.Subject,
				Body = x.Body,
				ErrorMessage = x.ErrorMessage,
				CreatedAtUtc = x.CreatedAtUtc,
				DeliveredAtUtc = x.DeliveredAtUtc
			}).ToArray();
	}

	public async Task<bool> RetryAsync(Guid notificationId, string tenantKey, CancellationToken cancellationToken)
	{
		NotificationMessage notification = await _notificationRepository.GetByIdAsync(notificationId, cancellationToken);
		if (notification == null || notification.TenantKey != tenantKey)
		{
			return false;
		}
		if (notification.Status != NotificationDeliveryStatus.Failed)
		{
			return false;
		}
		CustomerTenant customer = await _customerRepository.GetByTenantKeyAsync(tenantKey, cancellationToken);
		if (customer == null)
		{
			return false;
		}
		notification.Status = NotificationDeliveryStatus.Queued;
		notification.ErrorMessage = null;
		await _notificationDispatcher.DispatchAsync(customer, notification, cancellationToken);
		await _notificationRepository.UpdateAsync(notification, cancellationToken);
		await _unitOfWork.CommitAsync(cancellationToken);
		return true;
	}
}
