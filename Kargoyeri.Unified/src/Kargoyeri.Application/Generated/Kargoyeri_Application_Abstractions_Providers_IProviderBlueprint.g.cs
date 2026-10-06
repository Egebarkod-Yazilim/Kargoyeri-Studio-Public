// Generated from assembly metadata during repository recovery.
#nullable enable
namespace Kargoyeri.Application.Abstractions.Providers;

public interface IProviderBlueprint
{
    global::Kargoyeri.Domain.Enums.CargoProviderType SupportedProvider { get; }
    global::Kargoyeri.Contracts.Dtos.ProviderProfileDto Describe();
    global::Kargoyeri.Contracts.Dtos.ProviderRequestPreviewResponse PreviewCreateShipment(global::Kargoyeri.Domain.Entities.CargoShipment shipment, global::Kargoyeri.Domain.Entities.ProviderCredential credential);
}
