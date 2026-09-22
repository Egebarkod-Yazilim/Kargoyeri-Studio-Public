using System;
using System.Collections.Generic;
using System.Linq;
using Kargoyeri.Domain.Enums;
using Kargoyeri.Domain.ValueObjects;

namespace Kargoyeri.Domain.Entities;

public sealed class CargoShipment
{
	public Guid Id { get; set; } = Guid.NewGuid();


	public string ShipmentReference { get; set; } = string.Empty;


	public string TenantKey { get; set; } = string.Empty;


	public string OrderReference { get; set; } = string.Empty;


	public string? ClientShipmentReference { get; set; }

	public string? IdempotencyKey { get; set; }

	public CargoProviderType Provider { get; set; }

	public IntegrationSourceType Source { get; set; }

	public OrderSourceChannel? SourceChannel { get; set; }

	public string? SourceChannelCode { get; set; }

	public ShipmentStatus Status { get; set; } = ShipmentStatus.Pending;


	public decimal? CollectionAmount { get; set; }

	public string CurrencyCode { get; set; } = "TRY";


	public AddressInfo Sender { get; set; } = new AddressInfo();


	public AddressInfo Recipient { get; set; } = new AddressInfo();


	public List<PackageInfo> Packages { get; set; } = new List<PackageInfo>();


	public Dictionary<string, string> Metadata { get; set; } = new Dictionary<string, string>();


	public string? TrackingNumber { get; set; }

	public string? LabelUrl { get; set; }

	public string? LabelContentBase64 { get; set; }

	public string? ProviderMessage { get; set; }

	public string? ErrorMessage { get; set; }

	public int RetryCount { get; set; }

	public DateTimeOffset CreatedAtUtc { get; set; } = DateTimeOffset.UtcNow;


	public DateTimeOffset UpdatedAtUtc { get; set; } = DateTimeOffset.UtcNow;


	public DateTimeOffset? LastStatusCheckAtUtc { get; set; }

	public static CargoShipment Create(string shipmentReference, string tenantKey, string orderReference, string? clientShipmentReference, string? idempotencyKey, CargoProviderType provider, IntegrationSourceType source, decimal? collectionAmount, string currencyCode, AddressInfo sender, AddressInfo recipient, IEnumerable<PackageInfo> packages, Dictionary<string, string> metadata, OrderSourceChannel? sourceChannel = null, string? sourceChannelCode = null)
	{
		return new CargoShipment
		{
			ShipmentReference = shipmentReference,
			TenantKey = tenantKey,
			OrderReference = orderReference,
			ClientShipmentReference = clientShipmentReference,
			IdempotencyKey = idempotencyKey,
			Provider = provider,
			Source = source,
			SourceChannel = sourceChannel,
			SourceChannelCode = sourceChannelCode,
			CollectionAmount = collectionAmount,
			CurrencyCode = currencyCode,
			Sender = sender,
			Recipient = recipient,
			Packages = packages.ToList(),
			Metadata = metadata,
			Status = ShipmentStatus.Pending,
			CreatedAtUtc = DateTimeOffset.UtcNow,
			UpdatedAtUtc = DateTimeOffset.UtcNow
		};
	}

	public void ApplyProviderSuccess(ShipmentStatus status, string? trackingNumber, string? labelUrl, string? labelContentBase64, string? message)
	{
		Status = status;
		TrackingNumber = trackingNumber ?? TrackingNumber;
		LabelUrl = labelUrl ?? LabelUrl;
		LabelContentBase64 = labelContentBase64 ?? LabelContentBase64;
		ProviderMessage = message;
		ErrorMessage = null;
		UpdatedAtUtc = DateTimeOffset.UtcNow;
	}

	public void ApplyProviderFailure(string? message)
	{
		Status = ShipmentStatus.Failed;
		ErrorMessage = message;
		UpdatedAtUtc = DateTimeOffset.UtcNow;
	}

	public void MarkCancelled(string? message)
	{
		Status = ShipmentStatus.Cancelled;
		ProviderMessage = message;
		ErrorMessage = null;
		UpdatedAtUtc = DateTimeOffset.UtcNow;
	}

	public void RegisterRefreshAttempt()
	{
		RetryCount++;
		LastStatusCheckAtUtc = DateTimeOffset.UtcNow;
		UpdatedAtUtc = DateTimeOffset.UtcNow;
	}
}
