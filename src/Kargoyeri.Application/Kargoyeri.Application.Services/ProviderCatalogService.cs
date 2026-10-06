using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Kargoyeri.Application.Abstractions.Persistence;
using Kargoyeri.Application.Abstractions.Providers;
using Kargoyeri.Contracts.Dtos;
using Kargoyeri.Contracts.Enums;
using Kargoyeri.Domain.Entities;
using Kargoyeri.Domain.Enums;

namespace Kargoyeri.Application.Services;

public sealed class ProviderCatalogService
{
	private readonly IReadOnlyDictionary<CargoProviderTypeDto, IProviderBlueprint> _blueprints;

	private readonly IProviderSettingsRepository _providerSettingsRepository;

	private readonly CustomerService _customerService;

	public ProviderCatalogService(IEnumerable<IProviderBlueprint> blueprints, IProviderSettingsRepository providerSettingsRepository, CustomerService customerService)
	{
		_blueprints = blueprints.ToDictionary((IProviderBlueprint x) => ShipmentMapping.MapProvider(x.SupportedProvider), (IProviderBlueprint x) => x);
		_providerSettingsRepository = providerSettingsRepository;
		_customerService = customerService;
	}

	public IReadOnlyCollection<ProviderProfileDto> List()
	{
		return (IReadOnlyCollection<ProviderProfileDto>)(object)(from x in _blueprints.Values
			select NormalizeProfile(x.Describe()) into x
			orderby x.Provider
			select x).ToArray();
	}

	public ProviderProfileDto? Get(CargoProviderTypeDto provider)
	{
		IProviderBlueprint value;
		return _blueprints.TryGetValue(provider, out value) ? NormalizeProfile(value.Describe()) : null;
	}

	public async Task<ProviderRequestPreviewResponse> PreviewCreateAsync(string tenantKey, CargoProviderTypeDto provider, CreateShipmentRequest request, CancellationToken cancellationToken)
	{
		CustomerTenant customer = await _customerService.RequireTenantAsync(tenantKey, cancellationToken);
		CargoProviderType domainProvider = ShipmentMapping.MapProvider(provider);
		if (!customer.CanUseProvider(domainProvider))
		{
			throw new InvalidOperationException($"Tenant '{customer.TenantKey}' is not allowed to use provider '{domainProvider}'.");
		}
		if (!_blueprints.TryGetValue(provider, out IProviderBlueprint blueprint))
		{
			throw new InvalidOperationException($"Provider '{provider}' is not modeled in the current API.");
		}
		request.TenantKey = tenantKey;
		request.Provider = provider;
		CargoShipment shipment = ShipmentMapping.BuildTransientShipment(request);
		ProviderCredential settings = await _providerSettingsRepository.GetAsync(tenantKey, domainProvider, cancellationToken);
		return blueprint.PreviewCreateShipment(shipment, settings);
	}

	private static ProviderProfileDto NormalizeProfile(ProviderProfileDto profile)
	{
		if (profile.SettingsSchema.Any((ProviderSettingFieldDto x) => x.Scope.Equals("AdditionalSettings", StringComparison.OrdinalIgnoreCase) && x.Key.Equals("simulationMode", StringComparison.OrdinalIgnoreCase)))
		{
			return profile;
		}
		profile.SettingsSchema.Add(new ProviderSettingFieldDto
		{
			Key = "simulationMode",
			Label = "Simulation Mode",
			Scope = "AdditionalSettings",
			IsRequired = false,
			IsSecret = false,
			Description = "true verilirse canli transport bagli olmasa bile provider simulation modunda uctan uca test edilir.",
			ExampleValue = "true"
		});
		return profile;
	}
}
