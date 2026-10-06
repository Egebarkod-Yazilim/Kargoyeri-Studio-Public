namespace Kargoyeri.Studio.Core.Infrastructure;

/// <summary>
/// REST API erisimi icin API key yapilandirmasi.
/// appsettings.json'da "StudioApi:ApiKey" olarak tanimlanir.
/// Bos birakilirsa API anonim erisime acik kalir (development icin).
/// </summary>
public sealed class StudioApiKeyOptions
{
    public string? ApiKey { get; set; }
}
