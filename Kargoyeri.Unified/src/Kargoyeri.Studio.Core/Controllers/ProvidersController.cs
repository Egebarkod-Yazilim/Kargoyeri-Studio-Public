using System.Diagnostics;
using Kargoyeri.Application.Abstractions.Persistence;
using Kargoyeri.Application.Services;
using Kargoyeri.Contracts.Dtos;
using Kargoyeri.Contracts.Enums;
using Kargoyeri.Studio.Core.Infrastructure;
using Kargoyeri.Studio.Core.Models;
using Microsoft.AspNetCore.Mvc;

namespace Kargoyeri.Studio.Core.Controllers;

public sealed class ProvidersController : Controller
{
    private readonly ProviderCatalogService _providerCatalogService;
    private readonly StudioProviderStatusService _statusService;
    private readonly CargoOrchestrator _orchestrator;
    private readonly CustomerService _customerService;
    private readonly IProviderSettingsRepository _providerSettingsRepository;
    private readonly IStudioWorkspaceContext _workspaceContext;
    private readonly ProviderHealthMonitor _providerHealthMonitor;

    public ProvidersController(
        ProviderCatalogService providerCatalogService,
        StudioProviderStatusService statusService,
        CargoOrchestrator orchestrator,
        CustomerService customerService,
        IProviderSettingsRepository providerSettingsRepository,
        IStudioWorkspaceContext workspaceContext,
        ProviderHealthMonitor providerHealthMonitor)
    {
        _providerCatalogService = providerCatalogService;
        _statusService = statusService;
        _orchestrator = orchestrator;
        _customerService = customerService;
        _providerSettingsRepository = providerSettingsRepository;
        _workspaceContext = workspaceContext;
        _providerHealthMonitor = providerHealthMonitor;
    }

    [HttpGet]
    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        var workspace = await _workspaceContext.GetOrCreateAsync(_customerService, cancellationToken);
        var customerProfile = await _customerService.GetProfileAsync(workspace.TenantKey, cancellationToken);
        var allowedProviders = GetAllowedProviders(customerProfile);
        var health = _providerHealthMonitor
            .SummarizeTenant(workspace.TenantKey, allowedProviders, TimeSpan.FromHours(24))
            .ToDictionary(x => x.Provider, x => x);
        var cards = new List<ProviderCardViewModel>();

        foreach (var profile in _providerCatalogService.List())
        {
            if (!Enum.TryParse<CargoProviderTypeDto>(profile.ProviderCode, true, out var provider))
            {
                continue;
            }

            if (!allowedProviders.Contains(provider))
            {
                continue;
            }

            cards.Add(new ProviderCardViewModel
            {
                Profile = profile,
                Status = await _statusService.GetAsync(workspace.TenantKey, provider, cancellationToken),
                Visual = StudioVisualRegistry.Resolve(profile),
                Health = health.TryGetValue(provider, out var healthSummary)
                    ? MapHealth(profile.Provider, provider, healthSummary)
                    : new ProviderHealthSnapshotViewModel { Provider = provider, ProviderName = profile.Provider }
            });
        }

        return View(new ProviderIndexViewModel
        {
            WorkspaceCode = workspace.TenantKey,
            WorkspaceName = workspace.Name,
            Providers = cards,
            HealthSnapshots = cards.Select(x => x.Health).OrderBy(x => x.ProviderName).ToArray()
        });
    }

    [HttpGet]
    public async Task<IActionResult> Configure(string id, CancellationToken cancellationToken)
    {
        var workspace = await _workspaceContext.GetOrCreateAsync(_customerService, cancellationToken);
        var customerProfile = await _customerService.GetProfileAsync(workspace.TenantKey, cancellationToken);
        if (!TryParseProvider(id, out var provider))
        {
            return NotFound();
        }

        if (!GetAllowedProviders(customerProfile).Contains(provider))
        {
            return Forbid();
        }

        var profile = _providerCatalogService.Get(provider);
        if (profile is null)
        {
            return NotFound();
        }

        var settings = await _providerSettingsRepository.GetAsync(
            workspace.TenantKey,
            (Kargoyeri.Domain.Enums.CargoProviderType)provider,
            cancellationToken);

        return View(new ProviderSettingsEditorViewModel
        {
            WorkspaceCode = workspace.TenantKey,
            WorkspaceName = workspace.Name,
            ProviderCode = profile.ProviderCode,
            ProviderName = profile.Provider,
            Profile = profile,
            Visual = StudioVisualRegistry.Resolve(profile),
            Status = await _statusService.GetAsync(workspace.TenantKey, provider, cancellationToken),
            IsEnabled = settings?.IsEnabled ?? true,
            ClientCode = settings?.ClientCode,
            Username = settings?.Username,
            // GUVENLIK: Password/ApiKey'i HTML form'a echo etme. Gercek deger POST'ta korunur.
            Password = null,
            ApiKey = null,
            HasStoredPassword = !string.IsNullOrEmpty(settings?.Password),
            HasStoredApiKey = !string.IsNullOrEmpty(settings?.ApiKey),
            EndpointBase = settings?.EndpointBase,
            AdditionalSettingsText = ToText(settings?.AdditionalSettings)
        });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Configure(ProviderSettingsEditorViewModel model, CancellationToken cancellationToken)
    {
        var workspace = await _workspaceContext.GetOrCreateAsync(_customerService, cancellationToken);
        var customerProfile = await _customerService.GetProfileAsync(workspace.TenantKey, cancellationToken);
        if (!TryParseProvider(model.ProviderCode, out var provider))
        {
            return NotFound();
        }

        if (!GetAllowedProviders(customerProfile).Contains(provider))
        {
            return Forbid();
        }

        model.Profile = _providerCatalogService.Get(provider)!;
        model.Visual = StudioVisualRegistry.Resolve(model.Profile);
        model.Status = await _statusService.GetAsync(workspace.TenantKey, provider, cancellationToken);
        model.WorkspaceCode = workspace.TenantKey;
        model.WorkspaceName = workspace.Name;

        if (!ModelState.IsValid)
        {
            return View(model);
        }

        // GUVENLIK: Password/ApiKey bos gelirse eski deger korunur — form'a echo edilmedigi icin
        // kullanici her kaydet basisinda yeniden girmek zorunda kalmasin.
        var existing = await _providerSettingsRepository.GetAsync(
            workspace.TenantKey,
            (Kargoyeri.Domain.Enums.CargoProviderType)provider,
            cancellationToken);

        var effectivePassword = string.IsNullOrEmpty(model.Password) ? existing?.Password : model.Password;
        var effectiveApiKey   = string.IsNullOrEmpty(model.ApiKey)   ? existing?.ApiKey   : model.ApiKey;

        var request = new UpsertProviderSettingsRequest
        {
            Provider = provider,
            IsEnabled = model.IsEnabled,
            ClientCode = model.ClientCode,
            Username = model.Username,
            Password = effectivePassword,
            ApiKey = effectiveApiKey,
            EndpointBase = model.EndpointBase,
            AdditionalSettings = ParseSettings(model.AdditionalSettingsText)
        };

        await _orchestrator.UpsertProviderSettingsAsync(workspace.TenantKey, request, cancellationToken);
        TempData["StudioMessage"] = $"{model.ProviderName} ayarlari kaydedildi.";
        return RedirectToAction(nameof(Configure), new { id = model.ProviderCode });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Ping(string id, CancellationToken cancellationToken)
    {
        var workspace = await _workspaceContext.GetOrCreateAsync(_customerService, cancellationToken);
        var customerProfile = await _customerService.GetProfileAsync(workspace.TenantKey, cancellationToken);
        if (!TryParseProvider(id, out var provider))
        {
            return NotFound();
        }

        if (!GetAllowedProviders(customerProfile).Contains(provider))
        {
            return Forbid();
        }

        var settings = await _providerSettingsRepository.GetAsync(
            workspace.TenantKey,
            (Kargoyeri.Domain.Enums.CargoProviderType)provider,
            cancellationToken);

        if (settings is null || string.IsNullOrWhiteSpace(settings.EndpointBase))
        {
            TempData["PingResult"] = "fail";
            TempData["PingMessage"] = "Endpoint adresi tanimli degil. Once ayarlari kaydedin.";
            return RedirectToAction(nameof(Configure), new { id });
        }

        var sw = Stopwatch.StartNew();
        try
        {
            using var http = new HttpClient { Timeout = TimeSpan.FromSeconds(8) };
            var response = await http.GetAsync(settings.EndpointBase, cancellationToken);
            sw.Stop();
            _providerHealthMonitor.Record(new ProviderHealthSample(
                workspace.TenantKey,
                provider,
                response.IsSuccessStatusCode,
                (int)sw.ElapsedMilliseconds,
                DateTimeOffset.UtcNow,
                settings.EndpointBase,
                response.IsSuccessStatusCode ? null : $"HTTP {(int)response.StatusCode}",
                (int)response.StatusCode));
            TempData["PingResult"] = "ok";
            TempData["PingMessage"] = $"Endpoint erisimde - HTTP {(int)response.StatusCode}, {sw.ElapsedMilliseconds}ms";
        }
        catch (Exception ex)
        {
            sw.Stop();
            _providerHealthMonitor.Record(new ProviderHealthSample(
                workspace.TenantKey,
                provider,
                false,
                (int)sw.ElapsedMilliseconds,
                DateTimeOffset.UtcNow,
                settings.EndpointBase,
                ex.Message));
            TempData["PingResult"] = "fail";
            TempData["PingMessage"] = $"Baglanti basarisiz ({sw.ElapsedMilliseconds}ms): {ex.Message}";
        }

        return RedirectToAction(nameof(Configure), new { id });
    }

    private static bool TryParseProvider(string id, out CargoProviderTypeDto provider) =>
        Enum.TryParse(id, true, out provider);

    private static Dictionary<string, string> ParseSettings(string? text)
    {
        var dictionary = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
        if (string.IsNullOrWhiteSpace(text))
        {
            return dictionary;
        }

        foreach (var line in text.Split(['\r', '\n'], StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
        {
            var separatorIndex = line.IndexOf('=');
            if (separatorIndex <= 0)
            {
                continue;
            }

            var key = line[..separatorIndex].Trim();
            var value = line[(separatorIndex + 1)..].Trim();
            if (!string.IsNullOrWhiteSpace(key))
            {
                dictionary[key] = value;
            }
        }

        return dictionary;
    }

    private static string ToText(Dictionary<string, string>? dictionary)
    {
        if (dictionary is null || dictionary.Count == 0)
        {
            return string.Empty;
        }

        return string.Join(Environment.NewLine, dictionary.OrderBy(x => x.Key).Select(x => $"{x.Key}={x.Value}"));
    }

    private static HashSet<CargoProviderTypeDto> GetAllowedProviders(CustomerProfileDto? profile)
    {
        return profile?.AllowedProviders.ToHashSet() ?? new HashSet<CargoProviderTypeDto>();
    }

    private static ProviderHealthSnapshotViewModel MapHealth(
        string providerName,
        CargoProviderTypeDto provider,
        ProviderHealthSummary summary)
    {
        return new ProviderHealthSnapshotViewModel
        {
            Provider = provider,
            ProviderName = providerName,
            ProbeCount = summary.ProbeCount,
            SuccessCount = summary.SuccessCount,
            FailureCount = summary.FailureCount,
            SuccessRate = summary.SuccessRate,
            ErrorRate = summary.ErrorRate,
            P95LatencyMs = summary.P95LatencyMs,
            LastCheckedAtUtc = summary.LastCheckedAtUtc,
            LastError = summary.LastError
        };
    }
}
