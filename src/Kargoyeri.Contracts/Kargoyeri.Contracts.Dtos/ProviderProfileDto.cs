using System.Collections.Generic;

namespace Kargoyeri.Contracts.Dtos;

public sealed class ProviderProfileDto
{
	public string ProviderCode { get; set; } = string.Empty;


	public string Provider { get; set; } = string.Empty;


	public string IntegrationStyle { get; set; } = string.Empty;


	public string AuthenticationStyle { get; set; } = string.Empty;


	public string RecommendedIntegrationMode { get; set; } = string.Empty;


	public bool PublicDocsAvailable { get; set; }

	public bool LiveTransportImplemented { get; set; }

	public bool RequestPreviewAvailable { get; set; }

	public string? LegacySource { get; set; }

	public string SourceNote { get; set; } = string.Empty;


	public string? SourceUrl { get; set; }

	public string? OfficialDocsSummary { get; set; }

	public string? LastVerifiedDate { get; set; }

	public List<string> SupportedOperations { get; set; } = new List<string>();


	public List<string> KnownApiProducts { get; set; } = new List<string>();


	public List<string> KnownEndpointHints { get; set; } = new List<string>();


	public List<string> ModernizationNotes { get; set; } = new List<string>();


	public List<string> MetadataHints { get; set; } = new List<string>();


	public List<ProviderSettingFieldDto> SettingsSchema { get; set; } = new List<ProviderSettingFieldDto>();

}
