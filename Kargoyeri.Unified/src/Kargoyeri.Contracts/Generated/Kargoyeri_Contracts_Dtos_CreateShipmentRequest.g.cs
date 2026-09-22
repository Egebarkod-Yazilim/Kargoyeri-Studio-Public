// Generated from assembly metadata during repository recovery.
#nullable enable
namespace Kargoyeri.Contracts.Dtos;

public sealed partial class CreateShipmentRequest
{
    public string TenantKey { get; set; }
    public global::Kargoyeri.Contracts.Enums.IntegrationSourceTypeDto Source { get; set; }
    public global::Kargoyeri.Contracts.Enums.CargoProviderTypeDto Provider { get; set; }
    public string OrderReference { get; set; }
    public string ClientShipmentReference { get; set; }
    public string IdempotencyKey { get; set; }
    public decimal? CollectionAmount { get; set; }
    public string CurrencyCode { get; set; }
    public global::Kargoyeri.Contracts.Dtos.AddressDto Sender { get; set; }
    public global::Kargoyeri.Contracts.Dtos.AddressDto Recipient { get; set; }
    public global::System.Collections.Generic.List<global::Kargoyeri.Contracts.Dtos.PackageDto> Packages { get; set; }
    public global::System.Collections.Generic.Dictionary<string, string> Metadata { get; set; }
    public global::Kargoyeri.Contracts.Enums.OrderSourceChannelDto? SourceChannel { get; set; }
    public string SourceChannelCode { get; set; }
    public CreateShipmentRequest() { }
}
