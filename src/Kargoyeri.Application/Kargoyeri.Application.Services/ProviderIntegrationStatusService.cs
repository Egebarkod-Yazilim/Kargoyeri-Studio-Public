using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Kargoyeri.Application.Abstractions.Persistence;
using Kargoyeri.Contracts.Dtos;
using Kargoyeri.Contracts.Enums;
using Kargoyeri.Domain.Entities;

namespace Kargoyeri.Application.Services;

public sealed class ProviderIntegrationStatusService
{
	private readonly IProviderSettingsRepository _providerSettingsRepository;

	private readonly ProviderCatalogService _providerCatalogService;

	private readonly CustomerService _customerService;

	public ProviderIntegrationStatusService(IProviderSettingsRepository providerSettingsRepository, ProviderCatalogService providerCatalogService, CustomerService customerService)
	{
		_providerSettingsRepository = providerSettingsRepository;
		_providerCatalogService = providerCatalogService;
		_customerService = customerService;
	}

	public async Task<ProviderIntegrationStatusDto> GetAsync(string customerCode, CargoProviderTypeDto provider, CancellationToken cancellationToken)
	{
		CustomerTenant customer = await _customerService.RequireTenantAsync(customerCode, cancellationToken);
		ProviderProfileDto profile = _providerCatalogService.Get(provider) ?? throw new InvalidOperationException($"Provider '{provider}' is not modeled in the current API.");
		ProviderCredential settings = await _providerSettingsRepository.GetAsync(customer.TenantKey, ShipmentMapping.MapProvider(provider), cancellationToken);
		List<string> missingSettings = GetMissingSettings(profile, settings);
		string integrationMode = ResolveIntegrationMode(settings);
		bool simulationEnabled = IsSimulationEnabled(settings);
		bool canCreateShipment = profile.LiveTransportImplemented || simulationEnabled;
		return new ProviderIntegrationStatusDto
		{
			CustomerCode = customer.TenantKey,
			Provider = provider,
			Configured = (settings != null && settings.IsEnabled && missingSettings.Count == 0),
			LiveTransportImplemented = profile.LiveTransportImplemented,
			SimulationEnabled = simulationEnabled,
			CanCreateShipment = canCreateShipment,
			IntegrationMode = integrationMode,
			RecommendedIntegrationMode = profile.RecommendedIntegrationMode,
			SourceUrl = profile.SourceUrl,
			OfficialDocsSummary = profile.OfficialDocsSummary,
			RecommendedAction = BuildRecommendedAction(profile, settings, missingSettings, simulationEnabled),
			MissingSettings = missingSettings,
			KnownApiProducts = profile.KnownApiProducts,
			KnownEndpointHints = profile.KnownEndpointHints,
			ModernizationNotes = profile.ModernizationNotes,
			CheckedAtUtc = DateTimeOffset.UtcNow
		};
	}

	private static List<string> GetMissingSettings(ProviderProfileDto profile, ProviderCredential? settings)
	{
		List<string> list = new List<string>();
		if (settings == null || !settings.IsEnabled)
		{
			list.Add("Provider settings are missing or disabled.");
			return list;
		}
		foreach (ProviderSettingFieldDto item in profile.SettingsSchema.Where((ProviderSettingFieldDto x) => x.IsRequired))
		{
			string value = (item.Scope.Equals("Root", StringComparison.OrdinalIgnoreCase) ? GetRootValue(settings, item.Key) : GetAdditionalValue(settings, item.Key));
			if (string.IsNullOrWhiteSpace(value))
			{
				list.Add(item.Scope + "." + item.Key);
			}
		}
		return list;
	}

	private static string ResolveIntegrationMode(ProviderCredential? settings)
	{
		if (settings?.AdditionalSettings == null)
		{
			return "NotConfigured";
		}
		foreach (KeyValuePair<string, string> additionalSetting in settings.AdditionalSettings)
		{
			if (string.Equals(additionalSetting.Key, "integrationMode", StringComparison.OrdinalIgnoreCase))
			{
				return string.IsNullOrWhiteSpace(additionalSetting.Value) ? "Default" : additionalSetting.Value;
			}
		}
		return "Default";
	}

	private static bool IsSimulationEnabled(ProviderCredential? settings)
	{
		if (settings?.AdditionalSettings == null)
		{
			return false;
		}
		foreach (KeyValuePair<string, string> additionalSetting in settings.AdditionalSettings)
		{
			if (string.Equals(additionalSetting.Key, "simulationMode", StringComparison.OrdinalIgnoreCase))
			{
				return additionalSetting.Value != null && (additionalSetting.Value.Equals("true", StringComparison.OrdinalIgnoreCase) || additionalSetting.Value.Equals("1", StringComparison.OrdinalIgnoreCase) || additionalSetting.Value.Equals("yes", StringComparison.OrdinalIgnoreCase));
			}
		}
		return false;
	}

	private static string? GetRootValue(ProviderCredential settings, string key)
	{
		if (1 == 0)
		{
		}
		string result = key switch
		{
			"ClientCode" => settings.ClientCode, 
			"Username" => settings.Username, 
			"Password" => settings.Password, 
			"ApiKey" => settings.ApiKey, 
			"EndpointBase" => settings.EndpointBase, 
			_ => null, 
		};
		if (1 == 0)
		{
		}
		return result;
	}

	private static string? GetAdditionalValue(ProviderCredential settings, string key)
	{
		if (settings.AdditionalSettings == null)
		{
			return null;
		}
		foreach (KeyValuePair<string, string> additionalSetting in settings.AdditionalSettings)
		{
			if (string.Equals(additionalSetting.Key, key, StringComparison.OrdinalIgnoreCase))
			{
				return additionalSetting.Value;
			}
		}
		return null;
	}

	private static string BuildRecommendedAction(ProviderProfileDto profile, ProviderCredential? settings, IReadOnlyCollection<string> missingSettings, bool simulationEnabled)
	{
		if (settings == null || !settings.IsEnabled)
		{
			return "Önce provider settings endpointi ile firma bilgilerini kaydet ve aktif hale getir.";
		}
		if (missingSettings.Count > 0)
		{
			return "Eksik ayar alanlarını doldur; ardından preview ve shipment endpointlerini tekrar dene.";
		}
		if (profile.LiveTransportImplemented)
		{
			return "Canlı transport bağlı. Preview sonrası doğrudan shipment oluşturabilirsin.";
		}
		if (simulationEnabled)
		{
			return "Canlı transport henüz bağlı değil; simulationMode açık olduğu için uçtan uca test yapabilirsin.";
		}
		return "Canlı transport henüz bağlı değil. simulationMode=true ile entegrasyon test et veya gerçek provider adaptörünün eklenmesini bekle.";
	}
}
