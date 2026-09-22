// Generated from assembly metadata during repository recovery.
#nullable enable
namespace Kargoyeri.Domain.Entities;

public sealed partial class CargoShipment
{
    public global::System.Guid Id { get; set; }
    public string ShipmentReference { get; set; }
    public string TenantKey { get; set; }
    public string OrderReference { get; set; }
    public string ClientShipmentReference { get; set; }
    public string IdempotencyKey { get; set; }
    public global::Kargoyeri.Domain.Enums.CargoProviderType Provider { get; set; }
    public global::Kargoyeri.Domain.Enums.IntegrationSourceType Source { get; set; }
    public global::Kargoyeri.Domain.Enums.OrderSourceChannel? SourceChannel { get; set; }
    public string SourceChannelCode { get; set; }
    public global::Kargoyeri.Domain.Enums.ShipmentStatus Status { get; set; }
    public decimal? CollectionAmount { get; set; }
    public string CurrencyCode { get; set; }
    public global::Kargoyeri.Domain.ValueObjects.AddressInfo Sender { get; set; }
    public global::Kargoyeri.Domain.ValueObjects.AddressInfo Recipient { get; set; }
    public global::System.Collections.Generic.List<global::Kargoyeri.Domain.ValueObjects.PackageInfo> Packages { get; set; }
    public global::System.Collections.Generic.Dictionary<string, string> Metadata { get; set; }
    public string TrackingNumber { get; set; }
    public string LabelUrl { get; set; }
    public string LabelContentBase64 { get; set; }
    public string ProviderMessage { get; set; }
    public string ErrorMessage { get; set; }
    public int RetryCount { get; set; }
    public global::System.DateTimeOffset CreatedAtUtc { get; set; }
    public global::System.DateTimeOffset UpdatedAtUtc { get; set; }
    public global::System.DateTimeOffset? LastStatusCheckAtUtc { get; set; }
    public CargoShipment() { }
    public static global::Kargoyeri.Domain.Entities.CargoShipment Create(string shipmentReference, string tenantKey, string orderReference, string clientShipmentReference, string idempotencyKey, global::Kargoyeri.Domain.Enums.CargoProviderType provider, global::Kargoyeri.Domain.Enums.IntegrationSourceType source, decimal? collectionAmount, string currencyCode, global::Kargoyeri.Domain.ValueObjects.AddressInfo sender, global::Kargoyeri.Domain.ValueObjects.AddressInfo recipient, global::System.Collections.Generic.IEnumerable<global::Kargoyeri.Domain.ValueObjects.PackageInfo> packages, global::System.Collections.Generic.Dictionary<string, string> metadata, global::Kargoyeri.Domain.Enums.OrderSourceChannel? sourceChannel, string sourceChannelCode)
    {
        throw new global::System.NotImplementedException();
    }
    public void ApplyProviderSuccess(global::Kargoyeri.Domain.Enums.ShipmentStatus status, string trackingNumber, string labelUrl, string labelContentBase64, string message)
    {
        throw new global::System.NotImplementedException();
    }
    public void ApplyProviderFailure(string message)
    {
        throw new global::System.NotImplementedException();
    }
    public void MarkCancelled(string message)
    {
        throw new global::System.NotImplementedException();
    }
    public void RegisterRefreshAttempt()
    {
        throw new global::System.NotImplementedException();
    }
}
