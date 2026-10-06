// Generated from assembly metadata during repository recovery.
#nullable enable
namespace Kargoyeri.Application.Abstractions.Providers;

public interface ICargoProvider
{
    global::Kargoyeri.Domain.Enums.CargoProviderType SupportedProvider { get; }
    global::System.Threading.Tasks.Task<global::Kargoyeri.Application.Abstractions.Providers.ProviderShipmentResult> CreateShipmentAsync(global::Kargoyeri.Domain.Entities.CargoShipment shipment, global::Kargoyeri.Domain.Entities.ProviderCredential credential, global::System.Threading.CancellationToken cancellationToken);
    global::System.Threading.Tasks.Task<global::Kargoyeri.Application.Abstractions.Providers.ProviderShipmentResult> CancelShipmentAsync(global::Kargoyeri.Domain.Entities.CargoShipment shipment, global::Kargoyeri.Domain.Entities.ProviderCredential credential, string reason, global::System.Threading.CancellationToken cancellationToken);
    global::System.Threading.Tasks.Task<global::Kargoyeri.Application.Abstractions.Providers.ProviderShipmentResult> RefreshStatusAsync(global::Kargoyeri.Domain.Entities.CargoShipment shipment, global::Kargoyeri.Domain.Entities.ProviderCredential credential, global::System.Threading.CancellationToken cancellationToken);
}
