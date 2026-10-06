using System.Threading;
using System.Threading.Tasks;
using Kargoyeri.Domain.Entities;
using Kargoyeri.Domain.Enums;

namespace Kargoyeri.Application.Abstractions.Providers;

public interface ICargoProvider
{
	CargoProviderType SupportedProvider { get; }

	Task<ProviderShipmentResult> CreateShipmentAsync(CargoShipment shipment, ProviderCredential? credential, CancellationToken cancellationToken);

	Task<ProviderShipmentResult> CancelShipmentAsync(CargoShipment shipment, ProviderCredential? credential, string? reason, CancellationToken cancellationToken);

	Task<ProviderShipmentResult> RefreshStatusAsync(CargoShipment shipment, ProviderCredential? credential, CancellationToken cancellationToken);
}
