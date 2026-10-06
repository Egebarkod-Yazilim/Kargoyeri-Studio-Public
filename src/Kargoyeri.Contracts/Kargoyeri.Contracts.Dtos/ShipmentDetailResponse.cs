using System;
using System.Collections.Generic;
using Kargoyeri.Contracts.Enums;

namespace Kargoyeri.Contracts.Dtos;

public sealed class ShipmentDetailResponse
{
	public string ShipmentReference { get; set; } = string.Empty;


	public string CustomerCode { get; set; } = string.Empty;


	public string OrderReference { get; set; } = string.Empty;


	public string? ClientShipmentReference { get; set; }

	public string? IdempotencyKey { get; set; }

	public CargoProviderTypeDto Provider { get; set; }

	public IntegrationSourceTypeDto Source { get; set; }

	public ShipmentStatusDto Status { get; set; }

	public string? TrackingNumber { get; set; }

	public string? LabelUrl { get; set; }

	public string? ProviderMessage { get; set; }

	public string? ErrorMessage { get; set; }

	public decimal? CollectionAmount { get; set; }

	public string CurrencyCode { get; set; } = "TRY";


	public AddressDto Sender { get; set; } = new AddressDto();


	public AddressDto Recipient { get; set; } = new AddressDto();


	public List<PackageDto> Packages { get; set; } = new List<PackageDto>();


	public Dictionary<string, string> Metadata { get; set; } = new Dictionary<string, string>();


	public DateTimeOffset CreatedAtUtc { get; set; }

	public DateTimeOffset UpdatedAtUtc { get; set; }

	public DateTimeOffset? LastStatusCheckAtUtc { get; set; }

	public int RetryCount { get; set; }
}
