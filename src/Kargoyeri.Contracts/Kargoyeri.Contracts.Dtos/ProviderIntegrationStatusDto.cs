using System;
using System.Collections.Generic;
using Kargoyeri.Contracts.Enums;

namespace Kargoyeri.Contracts.Dtos;

public sealed class ProviderIntegrationStatusDto
{
	public string CustomerCode { get; set; } = string.Empty;


	public CargoProviderTypeDto Provider { get; set; }

	public bool Configured { get; set; }

	public bool LiveTransportImplemented { get; set; }

	public bool SimulationEnabled { get; set; }

	public bool CanCreateShipment { get; set; }

	public string IntegrationMode { get; set; } = string.Empty;


	public string RecommendedIntegrationMode { get; set; } = string.Empty;


	public string? SourceUrl { get; set; }

	public string? OfficialDocsSummary { get; set; }

	public string? RecommendedAction { get; set; }

	public List<string> MissingSettings { get; set; } = new List<string>();


	public List<string> KnownApiProducts { get; set; } = new List<string>();


	public List<string> KnownEndpointHints { get; set; } = new List<string>();


	public List<string> ModernizationNotes { get; set; } = new List<string>();


	public DateTimeOffset CheckedAtUtc { get; set; }
}
