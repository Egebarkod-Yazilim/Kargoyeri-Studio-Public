using System.Collections.Generic;
using System.Text.Json.Serialization;
using Kargoyeri.Contracts.Enums;

namespace Kargoyeri.Contracts.Dtos;

public sealed class CreateShipmentRequest
{
	[JsonIgnore]
	public string TenantKey { get; set; } = string.Empty;


	public IntegrationSourceTypeDto Source { get; set; } = IntegrationSourceTypeDto.Api;


	public CargoProviderTypeDto Provider { get; set; } = CargoProviderTypeDto.Sandbox;


	public string OrderReference { get; set; } = string.Empty;


	public string? ClientShipmentReference { get; set; }

	public string? IdempotencyKey { get; set; }

	public decimal? CollectionAmount { get; set; }

	public string CurrencyCode { get; set; } = "TRY";


	public AddressDto Sender { get; set; } = new AddressDto();


	public AddressDto Recipient { get; set; } = new AddressDto();


	public List<PackageDto> Packages { get; set; } = new List<PackageDto>();


	public Dictionary<string, string> Metadata { get; set; } = new Dictionary<string, string>();


	public OrderSourceChannelDto? SourceChannel { get; set; }

	public string? SourceChannelCode { get; set; }
}
