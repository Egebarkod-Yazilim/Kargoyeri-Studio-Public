using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using Kargoyeri.Application.Abstractions.Providers;
using Kargoyeri.Contracts.Dtos;
using Kargoyeri.Contracts.Enums;
using Kargoyeri.Domain.Entities;
using Kargoyeri.Domain.Enums;

namespace Kargoyeri.Infrastructure.Providers.Blueprints;

internal abstract class ProviderBlueprintBase : IProviderBlueprint
{
	private static readonly JsonSerializerOptions JsonOptions = new JsonSerializerOptions
	{
		WriteIndented = true
	};

	public abstract CargoProviderType SupportedProvider { get; }

	public abstract ProviderProfileDto Describe();

	public abstract ProviderRequestPreviewResponse PreviewCreateShipment(CargoShipment shipment, ProviderCredential? credential);

	protected static ProviderRequestPreviewResponse BuildPreview(CargoProviderType provider, string shipmentReference, string? trackingCandidate, object payload, IEnumerable<string>? missingConfiguration = null, IEnumerable<string>? missingMetadata = null, IEnumerable<string>? notes = null, bool liveTransportImplemented = false)
	{
		return new ProviderRequestPreviewResponse
		{
			Provider = (CargoProviderTypeDto)provider,
			Operation = "CreateShipment",
			RequestMapped = true,
			LiveTransportImplemented = liveTransportImplemented,
			ShipmentReference = shipmentReference,
			TrackingNumberCandidate = trackingCandidate,
			PayloadPreview = JsonSerializer.Serialize(payload, JsonOptions),
			MissingConfiguration = (missingConfiguration?.Distinct<string>(StringComparer.OrdinalIgnoreCase).ToList() ?? new List<string>()),
			MissingMetadata = (missingMetadata?.Distinct<string>(StringComparer.OrdinalIgnoreCase).ToList() ?? new List<string>()),
			Notes = (notes?.ToList() ?? new List<string>())
		};
	}

	protected static string? GetAdditionalSetting(ProviderCredential? credential, string key)
	{
		if (credential?.AdditionalSettings == null)
		{
			return null;
		}
		foreach (KeyValuePair<string, string> additionalSetting in credential.AdditionalSettings)
		{
			if (string.Equals(additionalSetting.Key, key, StringComparison.OrdinalIgnoreCase))
			{
				return additionalSetting.Value;
			}
		}
		return null;
	}

	protected static string? GetRootSetting(ProviderCredential? credential, string key)
	{
		if (1 == 0)
		{
		}
		string result = key switch
		{
			"ClientCode" => credential?.ClientCode, 
			"Username" => credential?.Username, 
			"Password" => credential?.Password, 
			"ApiKey" => credential?.ApiKey, 
			"EndpointBase" => credential?.EndpointBase, 
			_ => null, 
		};
		if (1 == 0)
		{
		}
		return result;
	}

	protected static string? MaskSecret(string? value)
	{
		if (string.IsNullOrWhiteSpace(value))
		{
			return value;
		}
		string result;
		if (value.Length > 4)
		{
			string text = new string('*', value.Length - 4);
			int length = value.Length;
			int num = length - 4;
			result = text + value.Substring(num, length - num);
		}
		else
		{
			result = new string('*', value.Length);
		}
		return result;
	}

	protected static ProviderSettingFieldDto RootField(string key, string label, bool required, bool secret, string description, string? exampleValue = null)
	{
		return new ProviderSettingFieldDto
		{
			Key = key,
			Label = label,
			Scope = "Root",
			IsRequired = required,
			IsSecret = secret,
			Description = description,
			ExampleValue = exampleValue
		};
	}

	protected static ProviderSettingFieldDto AdditionalField(string key, string label, bool required, bool secret, string description, string? exampleValue = null)
	{
		return new ProviderSettingFieldDto
		{
			Key = key,
			Label = label,
			Scope = "AdditionalSettings",
			IsRequired = required,
			IsSecret = secret,
			Description = description,
			ExampleValue = exampleValue
		};
	}
}
