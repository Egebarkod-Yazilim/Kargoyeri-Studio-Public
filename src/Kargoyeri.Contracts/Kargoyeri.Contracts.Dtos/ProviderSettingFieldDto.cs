namespace Kargoyeri.Contracts.Dtos;

public sealed class ProviderSettingFieldDto
{
	public string Key { get; set; } = string.Empty;


	public string Label { get; set; } = string.Empty;


	public string Scope { get; set; } = "AdditionalSettings";


	public bool IsRequired { get; set; }

	public bool IsSecret { get; set; }

	public string? Description { get; set; }

	public string? ExampleValue { get; set; }
}
