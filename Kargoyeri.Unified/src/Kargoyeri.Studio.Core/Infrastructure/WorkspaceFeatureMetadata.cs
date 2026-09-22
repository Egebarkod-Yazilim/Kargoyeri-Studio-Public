using System.Globalization;
using Kargoyeri.Contracts.Dtos;

namespace Kargoyeri.Studio.Core.Infrastructure;

public sealed class MarketplaceWorkspaceSettings
{
    public string Platform { get; set; } = string.Empty;
    public string? ApiBaseUrl { get; set; }
    public string? ApiKey { get; set; }
    public string? ApiSecret { get; set; }
    public string? StoreId { get; set; }
    public string? ChannelCode { get; set; }
    public bool AutoSyncEnabled { get; set; }
    public int AutoSyncIntervalMinutes { get; set; } = 30;
    public string? DefaultProvider { get; set; }
    public string? SenderName { get; set; }
    public string? SenderPhone { get; set; }
    public string? SenderCity { get; set; }
    public string? SenderDistrict { get; set; }
    public string? SenderAddress { get; set; }

    public bool HasConfiguration =>
        !string.IsNullOrWhiteSpace(Platform) &&
        !string.IsNullOrWhiteSpace(ApiBaseUrl);
}

public sealed class AddressValidationWorkspaceSettings
{
    public bool Enabled { get; set; }
    public bool StrictMode { get; set; } = true;
    public string? ApiBaseUrl { get; set; }
    public string? ApiKey { get; set; }
    public string? ApiSecret { get; set; }
    public bool HasRemoteConfiguration => Enabled && !string.IsNullOrWhiteSpace(ApiBaseUrl);
}

public sealed class WorkspaceBrandingSettings
{
    public string? DisplayName { get; set; }
    public string? LogoUrl { get; set; }
    public string? AccentColor { get; set; }
    public string? PartnerLabel { get; set; }
    public string? FooterText { get; set; }
    public bool HidePoweredBy { get; set; }
}

public static class WorkspaceFeatureMetadata
{
    public static MarketplaceWorkspaceSettings ReadMarketplace(CustomerProfileDto? profile)
    {
        return new MarketplaceWorkspaceSettings
        {
            Platform = GetMetadata(profile, "marketplace.platform") ?? string.Empty,
            ApiBaseUrl = GetMetadata(profile, "marketplace.apiBaseUrl"),
            ApiKey = GetMetadata(profile, "marketplace.apiKey"),
            ApiSecret = GetMetadata(profile, "marketplace.apiSecret"),
            StoreId = GetMetadata(profile, "marketplace.storeId"),
            ChannelCode = GetMetadata(profile, "marketplace.channelCode"),
            AutoSyncEnabled = GetBool(profile, "marketplace.autoSyncEnabled"),
            AutoSyncIntervalMinutes = GetInt(profile, "marketplace.autoSyncIntervalMinutes", 30),
            DefaultProvider = GetMetadata(profile, "marketplace.defaultProvider"),
            SenderName = GetMetadata(profile, "marketplace.senderName"),
            SenderPhone = GetMetadata(profile, "marketplace.senderPhone"),
            SenderCity = GetMetadata(profile, "marketplace.senderCity"),
            SenderDistrict = GetMetadata(profile, "marketplace.senderDistrict"),
            SenderAddress = GetMetadata(profile, "marketplace.senderAddress")
        };
    }

    public static AddressValidationWorkspaceSettings ReadAddressValidation(CustomerProfileDto? profile)
    {
        return new AddressValidationWorkspaceSettings
        {
            Enabled = GetBool(profile, "addressValidation.enabled"),
            StrictMode = !string.Equals(GetMetadata(profile, "addressValidation.strictMode"), "false", StringComparison.OrdinalIgnoreCase),
            ApiBaseUrl = GetMetadata(profile, "addressValidation.apiBaseUrl"),
            ApiKey = GetMetadata(profile, "addressValidation.apiKey"),
            ApiSecret = GetMetadata(profile, "addressValidation.apiSecret")
        };
    }

    public static WorkspaceBrandingSettings ReadBranding(CustomerProfileDto? profile)
    {
        return new WorkspaceBrandingSettings
        {
            DisplayName = GetMetadata(profile, "brand.displayName"),
            LogoUrl = GetMetadata(profile, "brand.logoUrl") ?? GetMetadata(profile, "brandLogoUrl"),
            AccentColor = NormalizeColor(GetMetadata(profile, "brand.accentColor")),
            PartnerLabel = GetMetadata(profile, "brand.partnerLabel"),
            FooterText = GetMetadata(profile, "brand.footerText"),
            HidePoweredBy = GetBool(profile, "brand.hidePoweredBy")
        };
    }

    public static void ApplyExtendedSettings(Dictionary<string, string> metadata, Kargoyeri.Studio.Core.Models.WorkspaceSetupInput input)
    {
        SetOrRemove(metadata, "marketplace.platform", input.MarketplacePlatform);
        SetOrRemove(metadata, "marketplace.apiBaseUrl", input.MarketplaceApiBaseUrl);
        SetOrRemove(metadata, "marketplace.apiKey", input.MarketplaceApiKey);
        SetOrRemove(metadata, "marketplace.apiSecret", input.MarketplaceApiSecret);
        SetOrRemove(metadata, "marketplace.storeId", input.MarketplaceStoreId);
        SetOrRemove(metadata, "marketplace.channelCode", input.MarketplaceChannelCode);
        SetOrRemove(metadata, "marketplace.defaultProvider", input.MarketplaceDefaultProvider);
        SetOrRemove(metadata, "marketplace.senderName", input.MarketplaceSenderName);
        SetOrRemove(metadata, "marketplace.senderPhone", input.MarketplaceSenderPhone);
        SetOrRemove(metadata, "marketplace.senderCity", input.MarketplaceSenderCity);
        SetOrRemove(metadata, "marketplace.senderDistrict", input.MarketplaceSenderDistrict);
        SetOrRemove(metadata, "marketplace.senderAddress", input.MarketplaceSenderAddress);
        metadata["marketplace.autoSyncEnabled"] = input.MarketplaceAutoSyncEnabled ? "true" : "false";
        metadata["marketplace.autoSyncIntervalMinutes"] = Math.Max(5, input.MarketplaceAutoSyncIntervalMinutes).ToString(CultureInfo.InvariantCulture);

        metadata["addressValidation.enabled"] = input.AddressValidationEnabled ? "true" : "false";
        metadata["addressValidation.strictMode"] = input.AddressValidationStrictMode ? "true" : "false";
        SetOrRemove(metadata, "addressValidation.apiBaseUrl", input.AddressValidationApiBaseUrl);
        SetOrRemove(metadata, "addressValidation.apiKey", input.AddressValidationApiKey);
        SetOrRemove(metadata, "addressValidation.apiSecret", input.AddressValidationApiSecret);

        SetOrRemove(metadata, "brand.displayName", input.BrandDisplayName);
        SetOrRemove(metadata, "brand.logoUrl", input.BrandLogoUrl);
        SetOrRemove(metadata, "brandLogoUrl", input.BrandLogoUrl);
        SetOrRemove(metadata, "brand.accentColor", NormalizeColor(input.BrandAccentColor));
        SetOrRemove(metadata, "brand.partnerLabel", input.BrandPartnerLabel);
        SetOrRemove(metadata, "brand.footerText", input.BrandFooterText);
        metadata["brand.hidePoweredBy"] = input.BrandHidePoweredBy ? "true" : "false";
    }

    public static string? GetMetadata(CustomerProfileDto? profile, string key) =>
        profile is not null && profile.Metadata.TryGetValue(key, out var value) ? value : null;

    public static bool GetBool(CustomerProfileDto? profile, string key)
    {
        return bool.TryParse(GetMetadata(profile, key), out var value) && value;
    }

    public static int GetInt(CustomerProfileDto? profile, string key, int fallback)
    {
        return int.TryParse(GetMetadata(profile, key), NumberStyles.Integer, CultureInfo.InvariantCulture, out var value)
            ? value
            : fallback;
    }

    public static void SetOrRemove(Dictionary<string, string> metadata, string key, string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            metadata.Remove(key);
            return;
        }

        metadata[key] = value.Trim();
    }

    private static string? NormalizeColor(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var trimmed = value.Trim();
        if (!trimmed.StartsWith('#'))
        {
            trimmed = "#" + trimmed;
        }

        return System.Text.RegularExpressions.Regex.IsMatch(trimmed, "^#[0-9a-fA-F]{6}$")
            ? trimmed.ToUpperInvariant()
            : null;
    }
}
