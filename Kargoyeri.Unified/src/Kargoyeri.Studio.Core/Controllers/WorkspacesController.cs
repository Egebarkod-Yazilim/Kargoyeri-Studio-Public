using Kargoyeri.Application.Abstractions.Persistence;
using Kargoyeri.Application.Options;
using Kargoyeri.Application.Services;
using Kargoyeri.Contracts.Dtos;
using Kargoyeri.Contracts.Enums;
using Kargoyeri.Studio.Core.Infrastructure;
using Kargoyeri.Studio.Core.Infrastructure.OrderChannels;
using Kargoyeri.Studio.Core.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.Extensions.Options;

namespace Kargoyeri.Studio.Core.Controllers;

[Authorize(Roles = StudioRoles.Admin)]
public sealed class WorkspacesController : Controller
{
    private readonly CustomerService _customerService;
    private readonly ProviderCatalogService _providerCatalogService;
    private readonly IStudioWorkspaceContext _workspaceContext;
    private readonly MarketplaceSyncMonitor _marketplaceSyncMonitor;
    private readonly MarketplaceOrderSyncEngine _marketplaceOrderSyncEngine;
    private readonly CargoOrchestrator _orchestrator;
    private readonly IProviderSettingsRepository _providerSettingsRepository;
    private readonly SmtpOptions _smtp;
    private readonly NetgsmOptions _netgsm;

    public WorkspacesController(
        CustomerService customerService,
        ProviderCatalogService providerCatalogService,
        IStudioWorkspaceContext workspaceContext,
        MarketplaceSyncMonitor marketplaceSyncMonitor,
        MarketplaceOrderSyncEngine marketplaceOrderSyncEngine,
        CargoOrchestrator orchestrator,
        IProviderSettingsRepository providerSettingsRepository,
        IOptions<SmtpOptions> smtpOptions,
        IOptions<NetgsmOptions> netgsmOptions)
    {
        _customerService = customerService;
        _providerCatalogService = providerCatalogService;
        _workspaceContext = workspaceContext;
        _marketplaceSyncMonitor = marketplaceSyncMonitor;
        _marketplaceOrderSyncEngine = marketplaceOrderSyncEngine;
        _orchestrator = orchestrator;
        _providerSettingsRepository = providerSettingsRepository;
        _smtp = smtpOptions.Value;
        _netgsm = netgsmOptions.Value;
    }

    [HttpGet]
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        var workspace = await _workspaceContext.GetOrCreateAsync(_customerService, cancellationToken);
        return View(await BuildViewModelAsync(workspace.TenantKey, workspace.Name, null, cancellationToken));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SaveProviders(
        [FromForm] List<string> allowedProviders,
        CancellationToken cancellationToken)
    {
        var workspace = await _workspaceContext.GetOrCreateAsync(_customerService, cancellationToken);
        var existingProfile = await _customerService.GetProfileAsync(workspace.TenantKey, cancellationToken);
        allowedProviders ??= new List<string>();

        var parsedProviders = allowedProviders
            .Where(p => Enum.TryParse<CargoProviderTypeDto>(p, true, out _))
            .Select(p => Enum.Parse<CargoProviderTypeDto>(p, true))
            .ToList();

        await _customerService.UpsertAsync(workspace.TenantKey, new UpsertCustomerRequest
        {
            TenantKey = workspace.TenantKey,
            Name = workspace.Name,
            IsActive = true,
            AllowedProviders = parsedProviders,
            NotificationTargets = existingProfile?.NotificationTargets.ToList() ?? new(),
            Metadata = existingProfile?.Metadata ?? new()
        }, cancellationToken);

        TempData["StudioMessage"] = "Kargo firmasi yetkileri guncellendi.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Save(WorkspaceSetupInput input, CancellationToken cancellationToken)
    {
        if (!ModelState.IsValid)
        {
            return View("Index", await BuildViewModelAsync(input.CustomerCode, input.CustomerName, input, cancellationToken));
        }

        var existingProfile = await _customerService.GetProfileAsync(input.CustomerCode, cancellationToken);

        // ── Multi-channel marketplace selections → metadata ──────────────────
        var metadata = BuildMetadata(existingProfile?.Metadata, input);
        ApplyMarketplaceSelections(metadata, input.Marketplaces);

        // ── Multi-provider cargo selections → AllowedProviders + ProviderCredential ─
        var allowedProviders = BuildAllowedProviders(input.Providers, existingProfile);

        var profile = await _customerService.UpsertAsync(
            input.CustomerCode,
            new UpsertCustomerRequest
            {
                TenantKey = input.CustomerCode,
                Name = input.CustomerName,
                IsActive = true,
                AllowedProviders = allowedProviders,
                NotificationTargets = BuildNotificationTargets(input),
                Metadata = metadata
            },
            cancellationToken);

        // Provider credential'larını orchestrator üzerinden ayrı kaydet (şifre/api-key boşsa eski korunur).
        await ApplyProviderCredentialsAsync(input.CustomerCode, input.Providers, cancellationToken);

        _workspaceContext.Set(profile.TenantKey, profile.Name);
        TempData["StudioMessage"] = $"'{profile.Name}' icin entegrasyon bilgileri kaydedildi.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SyncMarketplace(CancellationToken cancellationToken)
    {
        var workspace = await _workspaceContext.GetOrCreateAsync(_customerService, cancellationToken);
        var run = await _marketplaceOrderSyncEngine.SyncTenantAsync(workspace.TenantKey, force: true, cancellationToken);

        TempData["StudioMessage"] = run.Success
            ? $"{run.Platform} senkronizasyonu tamamlandi. Cekilen: {run.PulledCount}, yeni kayit: {run.ImportedCount}, atlanan: {run.SkippedCount}."
            : $"Marketplace senkronizasyonu hata verdi: {run.Message}";
        TempData["StudioMessageType"] = run.Success ? "success" : "error";
        return RedirectToAction(nameof(Index));
    }

    private static IReadOnlyCollection<ChecklistItemViewModel> BuildChecklist(CustomerProfileDto? profile)
    {
        var marketplace = WorkspaceFeatureMetadata.ReadMarketplace(profile);
        var addressValidation = WorkspaceFeatureMetadata.ReadAddressValidation(profile);
        var branding = WorkspaceFeatureMetadata.ReadBranding(profile);

        return
        [
            new()
            {
                Title = "Musteri kodu ve adi kayitli",
                Description = "Panel artik bu musteriye ait verilerle calisir.",
                IsDone = profile is not null
            },
            new()
            {
                Title = "Kaynak entegrasyon bilgisi var",
                Description = "Siparislerin alinacagi API baglantisi tanimli.",
                IsDone = HasSourceConfiguration(profile)
            },
            new()
            {
                Title = "Bildirim hedefi var",
                Description = "Webhook, e-posta veya telefon hedeflerinden en az biri tanimli.",
                IsDone = profile?.NotificationTargets.Any(x => x.IsEnabled) == true
            },
            new()
            {
                Title = "Provider kullanimi acik",
                Description = "Musteri icin secili kargo firmalari devreye alinabilir durumda.",
                IsDone = profile?.AllowedProviders.Count > 0
            },
            new()
            {
                Title = "Marketplace senkron ayari var",
                Description = "Trendyol / Hepsiburada / N11 siparisleri otomatik cekilmeye hazir.",
                IsDone = marketplace.HasConfiguration
            },
            new()
            {
                Title = "Adres dogrulama acik",
                Description = "Yanlis adresler gonderi olusturmadan once kontrol ediliyor.",
                IsDone = addressValidation.Enabled
            },
            new()
            {
                Title = "White-label bilgisi girildi",
                Description = "Bayi / co-branded gorunum icin marka alani tanimli.",
                IsDone = !string.IsNullOrWhiteSpace(branding.DisplayName) ||
                         !string.IsNullOrWhiteSpace(branding.LogoUrl) ||
                         !string.IsNullOrWhiteSpace(branding.PartnerLabel)
            }
        ];
    }

    private static bool HasSourceConfiguration(CustomerProfileDto? profile) =>
        !string.IsNullOrWhiteSpace(GetMetadata(profile, "source.apiBaseUrl")) &&
        !string.IsNullOrWhiteSpace(GetMetadata(profile, "source.systemName"));

    private static string? GetMetadata(CustomerProfileDto? profile, string key) =>
        profile is not null && profile.Metadata.TryGetValue(key, out var value) ? value : null;

    private static Dictionary<string, string> BuildMetadata(
        Dictionary<string, string>? existing,
        WorkspaceSetupInput input)
    {
        var metadata = existing is null
            ? new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
            : new Dictionary<string, string>(existing, StringComparer.OrdinalIgnoreCase);

        SetOrRemove(metadata, "source.systemName", input.SourceSystemName);
        SetOrRemove(metadata, "source.apiBaseUrl", input.SourceApiBaseUrl);
        SetOrRemove(metadata, "source.apiKey", input.SourceApiKey);
        SetOrRemove(metadata, "source.apiSecret", input.SourceApiSecret);
        SetOrRemove(metadata, "source.channelCode", input.SourceChannelCode);
        SetOrRemove(metadata, "webhook.secret", input.WebhookSecret);
        WorkspaceFeatureMetadata.ApplyExtendedSettings(metadata, input);

        return metadata;
    }

    private static List<NotificationTargetDto> BuildNotificationTargets(WorkspaceSetupInput input)
    {
        var targets = new List<NotificationTargetDto>
        {
            new()
            {
                Channel = NotificationChannelDto.Internal,
                Address = input.CustomerCode,
                IsEnabled = true
            }
        };

        AddTarget(targets, NotificationChannelDto.Webhook, input.NotificationWebhook);
        AddTarget(targets, NotificationChannelDto.Email, input.NotificationEmail);
        AddTarget(targets, NotificationChannelDto.Sms, input.NotificationPhone);

        return targets;
    }

    private static void AddTarget(List<NotificationTargetDto> targets, NotificationChannelDto channel, string? address)
    {
        if (string.IsNullOrWhiteSpace(address))
        {
            return;
        }

        targets.Add(new NotificationTargetDto
        {
            Channel = channel,
            Address = address.Trim(),
            IsEnabled = true
        });
    }

    private static void SetOrRemove(Dictionary<string, string> metadata, string key, string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            metadata.Remove(key);
            return;
        }

        metadata[key] = value.Trim();
    }

    private async Task<WorkspaceViewModel> BuildViewModelAsync(
        string workspaceCode,
        string workspaceName,
        WorkspaceSetupInput? input,
        CancellationToken cancellationToken)
    {
        var profile = await _customerService.GetProfileAsync(workspaceCode, cancellationToken);
        var allProviders = _providerCatalogService.List();
        var allowedSet = profile?.AllowedProviders.ToHashSet() ?? new HashSet<CargoProviderTypeDto>();
        var providerPermissions = allProviders
            .Where(p => Enum.TryParse<CargoProviderTypeDto>(p.ProviderCode, true, out var pt)
                        && pt != CargoProviderTypeDto.Sandbox)
            .Select(p =>
            {
                Enum.TryParse<CargoProviderTypeDto>(p.ProviderCode, true, out var pt2);
                return new ProviderPermissionViewModel
                {
                    Provider = pt2,
                    DisplayName = p.ProviderCode,
                    IsAllowed = allowedSet.Contains(pt2)
                };
            })
            .OrderBy(p => p.DisplayName)
            .ToArray();

        input ??= BuildInput(profile, workspaceCode, workspaceName);

        return new WorkspaceViewModel
        {
            CurrentWorkspaceCode = workspaceCode,
            CurrentWorkspaceName = workspaceName,
            Profile = profile,
            SetupChecklist = BuildChecklist(profile),
            SourceConfigured = HasSourceConfiguration(profile),
            IsSmtpConfigured = _smtp.IsConfigured,
            IsNetgsmConfigured = _netgsm.IsConfigured,
            AllProviders = providerPermissions,
            Input = input,
            MarketplaceRuns = _marketplaceSyncMonitor.ListByTenant(workspaceCode)
                .Select(x => new MarketplaceSyncRunViewModel
                {
                    Platform = x.Platform,
                    StartedAtUtc = x.StartedAtUtc,
                    CompletedAtUtc = x.CompletedAtUtc,
                    PulledCount = x.PulledCount,
                    ImportedCount = x.ImportedCount,
                    SkippedCount = x.SkippedCount,
                    FailedCount = x.FailedCount,
                    Success = x.Success,
                    Message = x.Message
                })
                .ToArray()
        };
    }

    private WorkspaceSetupInput BuildInput(CustomerProfileDto? profile, string workspaceCode, string workspaceName)
    {
        var marketplace = WorkspaceFeatureMetadata.ReadMarketplace(profile);
        var validation = WorkspaceFeatureMetadata.ReadAddressValidation(profile);
        var branding = WorkspaceFeatureMetadata.ReadBranding(profile);

        return new WorkspaceSetupInput
        {
            CustomerCode = workspaceCode,
            CustomerName = workspaceName,
            NotificationWebhook = profile?.NotificationTargets.FirstOrDefault(x => x.Channel == NotificationChannelDto.Webhook)?.Address,
            WebhookSecret = GetMetadata(profile, "webhook.secret"),
            NotificationEmail = profile?.NotificationTargets.FirstOrDefault(x => x.Channel == NotificationChannelDto.Email)?.Address,
            NotificationPhone = profile?.NotificationTargets.FirstOrDefault(x => x.Channel == NotificationChannelDto.Sms)?.Address,
            SourceSystemName = GetMetadata(profile, "source.systemName"),
            SourceApiBaseUrl = GetMetadata(profile, "source.apiBaseUrl"),
            SourceApiKey = GetMetadata(profile, "source.apiKey"),
            SourceApiSecret = GetMetadata(profile, "source.apiSecret"),
            SourceChannelCode = GetMetadata(profile, "source.channelCode"),
            MarketplacePlatform = marketplace.Platform,
            MarketplaceApiBaseUrl = marketplace.ApiBaseUrl,
            MarketplaceApiKey = marketplace.ApiKey,
            MarketplaceApiSecret = marketplace.ApiSecret,
            MarketplaceStoreId = marketplace.StoreId,
            MarketplaceChannelCode = marketplace.ChannelCode,
            MarketplaceAutoSyncEnabled = marketplace.AutoSyncEnabled,
            MarketplaceAutoSyncIntervalMinutes = marketplace.AutoSyncIntervalMinutes,
            MarketplaceDefaultProvider = marketplace.DefaultProvider,
            MarketplaceSenderName = marketplace.SenderName,
            MarketplaceSenderPhone = marketplace.SenderPhone,
            MarketplaceSenderCity = marketplace.SenderCity,
            MarketplaceSenderDistrict = marketplace.SenderDistrict,
            MarketplaceSenderAddress = marketplace.SenderAddress,
            AddressValidationEnabled = validation.Enabled,
            AddressValidationStrictMode = validation.StrictMode,
            AddressValidationApiBaseUrl = validation.ApiBaseUrl,
            AddressValidationApiKey = validation.ApiKey,
            AddressValidationApiSecret = validation.ApiSecret,
            BrandDisplayName = branding.DisplayName,
            BrandLogoUrl = branding.LogoUrl,
            BrandAccentColor = branding.AccentColor,
            BrandPartnerLabel = branding.PartnerLabel,
            BrandFooterText = branding.FooterText,
            BrandHidePoweredBy = branding.HidePoweredBy,
            Marketplaces = BuildMarketplaceMap(profile),
            Providers = BuildProviderMap(profile, workspaceCode)
        };
    }

    private static void ApplyMarketplaceSelections(
        Dictionary<string, string> metadata,
        Dictionary<string, MarketplaceSelectionInput>? selections)
    {
        if (selections is null || selections.Count == 0) return;

        foreach (var kvp in selections)
        {
            var descriptor = OrderChannelCatalog.FindByCode(kvp.Key);
            if (descriptor is null) continue;
            if (descriptor.Stage == OrderChannelStage.ComingSoon) continue;

            var sel = kvp.Value ?? new MarketplaceSelectionInput();
            // Boş alanlar (özellikle secret) eski değeri ezmesin: descriptor'a göre Apply
            // secret + boş = mevcut değer korunur (OrderChannelMetadata.Apply içinde böyle).
            OrderChannelMetadata.Apply(
                metadata,
                descriptor.Type,
                sel.Enabled,
                sel.DefaultProvider,
                sel.Fields ?? new Dictionary<string, string>());
        }
    }

    private static List<CargoProviderTypeDto> BuildAllowedProviders(
        Dictionary<string, ProviderSelectionInput>? providers,
        CustomerProfileDto? existingProfile)
    {
        if (providers is null || providers.Count == 0)
        {
            return existingProfile?.AllowedProviders.ToList() ?? new List<CargoProviderTypeDto>();
        }

        var list = new List<CargoProviderTypeDto>();
        foreach (var kvp in providers)
        {
            if (kvp.Value is null || !kvp.Value.Enabled) continue;
            if (Enum.TryParse<CargoProviderTypeDto>(kvp.Key, true, out var pt) && pt != CargoProviderTypeDto.Sandbox)
            {
                list.Add(pt);
            }
        }
        return list;
    }

    private async Task ApplyProviderCredentialsAsync(
        string tenantKey,
        Dictionary<string, ProviderSelectionInput>? providers,
        CancellationToken ct)
    {
        if (providers is null || providers.Count == 0) return;

        foreach (var kvp in providers)
        {
            var sel = kvp.Value;
            if (sel is null) continue;
            if (!Enum.TryParse<CargoProviderTypeDto>(kvp.Key, true, out var pt) || pt == CargoProviderTypeDto.Sandbox) continue;

            // Hiçbir alan girilmemişse boş kayıt yazma — sadece toggle değiştiyse Allowed listesi yeter.
            var hasAnyValue =
                !string.IsNullOrWhiteSpace(sel.ClientCode) ||
                !string.IsNullOrWhiteSpace(sel.Username) ||
                !string.IsNullOrWhiteSpace(sel.Password) ||
                !string.IsNullOrWhiteSpace(sel.ApiKey) ||
                !string.IsNullOrWhiteSpace(sel.EndpointBase);
            if (!hasAnyValue && !sel.Enabled) continue;

            // GUVENLIK: Password / ApiKey boş → mevcut değer korunur.
            var existing = await _providerSettingsRepository.GetAsync(
                tenantKey, (Kargoyeri.Domain.Enums.CargoProviderType)pt, ct);
            var effectivePassword = string.IsNullOrEmpty(sel.Password) ? existing?.Password : sel.Password;
            var effectiveApiKey   = string.IsNullOrEmpty(sel.ApiKey)   ? existing?.ApiKey   : sel.ApiKey;

            await _orchestrator.UpsertProviderSettingsAsync(tenantKey, new UpsertProviderSettingsRequest
            {
                Provider = pt,
                IsEnabled = sel.Enabled,
                ClientCode = sel.ClientCode,
                Username = sel.Username,
                Password = effectivePassword,
                ApiKey = effectiveApiKey,
                EndpointBase = sel.EndpointBase,
                AdditionalSettings = existing?.AdditionalSettings ?? new Dictionary<string, string>()
            }, ct);
        }
    }

    private static Dictionary<string, MarketplaceSelectionInput> BuildMarketplaceMap(CustomerProfileDto? profile)
    {
        var dict = new Dictionary<string, MarketplaceSelectionInput>(StringComparer.OrdinalIgnoreCase);
        foreach (var d in OrderChannelCatalog.All)
        {
            // View hem "Pazaryeri" (Step 3) hem "E-Ticaret" (Step 2) gruplarini render eder.
            // Backend tek bir Marketplaces dictionary'sinde her ikisini de tutar — bu sayede
            // ApplyMarketplaceSelections tek dongude tum kanal metadata'sini yazar.
            if (!string.Equals(d.Group, "Pazaryeri", StringComparison.OrdinalIgnoreCase) &&
                !string.Equals(d.Group, "E-Ticaret", StringComparison.OrdinalIgnoreCase))
                continue;
            if (d.Stage == OrderChannelStage.ComingSoon) continue;
            var state = OrderChannelMetadata.Read(profile, d.Type);
            dict[d.Code] = new MarketplaceSelectionInput
            {
                Enabled = state.Enabled,
                DefaultProvider = state.DefaultProvider,
                Fields = new Dictionary<string, string>(state.Fields, StringComparer.OrdinalIgnoreCase)
            };
        }
        return dict;
    }

    private Dictionary<string, ProviderSelectionInput> BuildProviderMap(CustomerProfileDto? profile, string tenantKey)
    {
        var dict = new Dictionary<string, ProviderSelectionInput>(StringComparer.OrdinalIgnoreCase);
        var allowed = profile?.AllowedProviders.ToHashSet() ?? new HashSet<CargoProviderTypeDto>();
        var allProviders = _providerCatalogService.List();

        foreach (var p in allProviders)
        {
            if (!Enum.TryParse<CargoProviderTypeDto>(p.ProviderCode, true, out var pt)) continue;
            if (pt == CargoProviderTypeDto.Sandbox) continue;

            var pcode = pt.ToString().ToLowerInvariant();
            ProviderSelectionInput sel;
            try
            {
                var existing = _providerSettingsRepository
                    .GetAsync(tenantKey, (Kargoyeri.Domain.Enums.CargoProviderType)pt, CancellationToken.None)
                    .GetAwaiter().GetResult();
                sel = new ProviderSelectionInput
                {
                    Enabled = allowed.Contains(pt),
                    ClientCode = existing?.ClientCode,
                    Username = existing?.Username,
                    EndpointBase = existing?.EndpointBase,
                    // Password ve ApiKey'i forma echo etmiyoruz (güvenlik). Boş gelirse mevcut korunur.
                    Password = null,
                    ApiKey = null
                };
            }
            catch
            {
                sel = new ProviderSelectionInput { Enabled = allowed.Contains(pt) };
            }
            dict[pcode] = sel;
        }
        return dict;
    }
}
