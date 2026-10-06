using Kargoyeri.Contracts.Dtos;
using Kargoyeri.Domain.Entities;
using Kargoyeri.Domain.Enums;

namespace Kargoyeri.Application.Abstractions.Providers;

public interface IProviderBlueprint
{
	CargoProviderType SupportedProvider { get; }

	ProviderProfileDto Describe();

	ProviderRequestPreviewResponse PreviewCreateShipment(CargoShipment shipment, ProviderCredential? credential);
}
