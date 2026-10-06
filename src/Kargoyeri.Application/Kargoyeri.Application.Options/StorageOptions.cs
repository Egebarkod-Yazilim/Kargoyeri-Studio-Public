namespace Kargoyeri.Application.Options;

public sealed class StorageOptions
{
	public string Mode { get; set; } = "JsonFile";


	public string BasePath { get; set; } = "data";


	public string? ConnectionString { get; set; }
}
