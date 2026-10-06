using System.Collections.Generic;
using Kargoyeri.Domain.Entities;

namespace Kargoyeri.Infrastructure.Persistence;

internal sealed class JsonDatabaseDocument
{
	public List<CustomerTenant> Customers { get; set; } = new List<CustomerTenant>();


	public List<CargoShipment> Shipments { get; set; } = new List<CargoShipment>();


	public List<ShipmentOperationLog> Logs { get; set; } = new List<ShipmentOperationLog>();


	public List<NotificationMessage> Notifications { get; set; } = new List<NotificationMessage>();


	public List<ProviderCredential> ProviderSettings { get; set; } = new List<ProviderCredential>();

}
