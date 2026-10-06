using System.Collections.Generic;
using Kargoyeri.Contracts.Enums;

namespace Kargoyeri.Contracts.Dtos;

public sealed class ProviderRequestPreviewResponse
{
	public CargoProviderTypeDto Provider { get; set; }

	public string Operation { get; set; } = "CreateShipment";


	public bool RequestMapped { get; set; }

	public bool LiveTransportImplemented { get; set; }

	public string ShipmentReference { get; set; } = string.Empty;


	public string? TrackingNumberCandidate { get; set; }

	public string PayloadFormat { get; set; } = "json";


	public string? PayloadPreview { get; set; }

	public List<string> MissingConfiguration { get; set; } = new List<string>();


	public List<string> MissingMetadata { get; set; } = new List<string>();


	public List<string> Notes { get; set; } = new List<string>();

}
