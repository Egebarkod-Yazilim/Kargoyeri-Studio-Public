using Kargoyeri.Application.Abstractions.Persistence;
using Kargoyeri.Application.Services;
using Kargoyeri.Contracts.Dtos;
using Kargoyeri.Contracts.Enums;
using Kargoyeri.Domain.Enums;

namespace Kargoyeri.Studio.Core.Infrastructure;

/// <summary>
/// Studio'ya ozel provider durum servisi.
/// Kargoyeri.Application.Services.ProviderIntegrationStatusService ile cakismamasi icin
/// bu wrapper kullanilmaktadir; arka planda ayni Contracts.Dtos.ProviderIntegrationStatusDto'yu dondurur.
/// </summary>
public sealed class StudioProviderStatusService
{
    private readonly ProviderCatalogService _catalogService;
    private readonly IProviderSettingsRepository _settingsRepository;

    public StudioProviderStatusService(
        ProviderCatalogService catalogService,
        IProviderSettingsRepository settingsRepository)
    {
        _catalogService = catalogService;
        _settingsRepository = settingsRepository;
    }

    public async Task<ProviderIntegrationStatusDto> GetAsync(
        string tenantKey,
        CargoProviderTypeDto providerDto,
        CancellationToken cancellationToken)
    {
        var profile = _catalogService.Get(providerDto);
        var domainProvider = (CargoProviderType)providerDto;
        var credential = await _settingsRepository.GetAsync(tenantKey, domainProvider, cancellationToken);

        var configured = credential is { IsEnabled: true };
        var missing = new List<string>();

        if (profile is not null)
        {
            foreach (var field in profile.SettingsSchema.Where(f => f.IsRequired))
            {
                var value = field.Scope == "Root"
                    ? GetRootValue(credential, field.Key)
                    : credential?.AdditionalSettings?.GetValueOrDefault(field.Key);

                if (string.IsNullOrWhiteSpace(value))
                {
                    missing.Add(field.Label);
                }
            }
        }

        var liveTransport = profile?.LiveTransportImplemented ?? false;
        var simulationEnabled = credential?.AdditionalSettings
            ?.TryGetValue("simulationMode", out var simVal) == true
            && string.Equals(simVal, "true", StringComparison.OrdinalIgnoreCase);

        var canCreate = configured && missing.Count == 0;

        string mode;
        if (!configured)
            mode = "Kurulum Gerekli";
        else if (liveTransport)
            mode = "Canli";
        else if (simulationEnabled)
            mode = "Simulasyon";
        else
            mode = "Pasif";

        string action;
        if (!configured)
            action = "Firma ayarlarini girerek kurulumu tamamla.";
        else if (missing.Count > 0)
            action = $"{missing.Count} eksik alan var, tamamlanmasi gerekiyor.";
        else if (!liveTransport)
            action = "Canli transport henuz entegre degil, simulasyon kullanilabilir.";
        else
            action = "Gonderi olusturmaya hazir.";

        return new ProviderIntegrationStatusDto
        {
            CustomerCode = tenantKey,
            Provider = providerDto,
            Configured = configured,
            CanCreateShipment = canCreate,
            LiveTransportImplemented = liveTransport,
            SimulationEnabled = simulationEnabled,
            IntegrationMode = mode,
            MissingSettings = missing,
            RecommendedAction = action,
            CheckedAtUtc = DateTimeOffset.UtcNow
        };
    }

    private static string? GetRootValue(Kargoyeri.Domain.Entities.ProviderCredential? credential, string key) =>
        key switch
        {
            "ClientCode" => credential?.ClientCode,
            "Username" => credential?.Username,
            "Password" => credential?.Password,
            "ApiKey" => credential?.ApiKey,
            "EndpointBase" => credential?.EndpointBase,
            _ => null
        };
}
