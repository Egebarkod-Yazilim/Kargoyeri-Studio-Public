using Kargoyeri.Application.Abstractions.Persistence;
using Kargoyeri.Application.Abstractions.Providers;
using Kargoyeri.Contracts.Enums;
using Kargoyeri.Domain.Entities;
using Kargoyeri.Domain.Enums;
using Kargoyeri.Studio.Core.Models;

namespace Kargoyeri.Studio.Core.Infrastructure;

public sealed class WorkflowNotificationDispatcher
{
    private readonly INotificationRepository _notificationRepository;
    private readonly INotificationDispatcher _notificationDispatcher;
    private readonly IUnitOfWork _unitOfWork;

    public WorkflowNotificationDispatcher(
        INotificationRepository notificationRepository,
        INotificationDispatcher notificationDispatcher,
        IUnitOfWork unitOfWork)
    {
        _notificationRepository = notificationRepository;
        _notificationDispatcher = notificationDispatcher;
        _unitOfWork = unitOfWork;
    }

    public async Task<int> DispatchAsync(
        CustomerTenant customer,
        CargoShipment shipment,
        WorkflowRuleDefinition rule,
        CancellationToken cancellationToken)
    {
        var sent = 0;
        var targetChannel = (NotificationChannel)rule.Channel;
        var subject = $"{customer.Name} - {shipment.ShipmentReference} - Workflow";
        var body = Render(rule.MessageTemplate, shipment, rule);

        foreach (var target in customer.NotificationTargets.Where(x => x.IsEnabled && x.Channel == targetChannel))
        {
            var notification = new NotificationMessage
            {
                TenantKey = customer.TenantKey,
                ShipmentReference = shipment.ShipmentReference,
                EventType = NotificationEventType.ShipmentStatusChanged,
                Channel = targetChannel,
                Address = target.Address,
                Subject = subject,
                Body = body,
                CreatedAtUtc = DateTimeOffset.UtcNow
            };

            await _notificationRepository.AppendAsync(notification, cancellationToken);
            await _notificationDispatcher.DispatchAsync(customer, notification, cancellationToken);
            await _notificationRepository.UpdateAsync(notification, cancellationToken);
            sent++;
        }

        if (sent > 0)
        {
            await _unitOfWork.CommitAsync(cancellationToken);
        }

        return sent;
    }

    private static string Render(string template, CargoShipment shipment, WorkflowRuleDefinition rule)
    {
        var hoursInStatus = Math.Max(1, (int)Math.Floor((DateTimeOffset.UtcNow - shipment.UpdatedAtUtc).TotalHours));
        return (template ?? string.Empty)
            .Replace("{ShipmentReference}", shipment.ShipmentReference, StringComparison.OrdinalIgnoreCase)
            .Replace("{TrackingNumber}", shipment.TrackingNumber ?? "-", StringComparison.OrdinalIgnoreCase)
            .Replace("{Provider}", shipment.Provider.ToString(), StringComparison.OrdinalIgnoreCase)
            .Replace("{Status}", shipment.Status.ToString(), StringComparison.OrdinalIgnoreCase)
            .Replace("{HoursInStatus}", hoursInStatus.ToString(), StringComparison.OrdinalIgnoreCase)
            .Replace("{RecipientCity}", shipment.Recipient?.City ?? "-", StringComparison.OrdinalIgnoreCase)
            .Replace("{RuleName}", rule.Name, StringComparison.OrdinalIgnoreCase);
    }
}
