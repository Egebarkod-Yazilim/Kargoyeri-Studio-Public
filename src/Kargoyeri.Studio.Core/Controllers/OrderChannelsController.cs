using Kargoyeri.Application.Services;
using Kargoyeri.Contracts.Dtos;
using Kargoyeri.Studio.Core.Infrastructure;
using Kargoyeri.Studio.Core.Infrastructure.OrderChannels;
using Kargoyeri.Studio.Core.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Kargoyeri.Studio.Core.Controllers;

[Authorize(Roles = StudioRoles.Admin)]
[Route("admin/order-channels")]
public sealed class OrderChannelsController : Controller
{
    private readonly CustomerService _customerService;
    private readonly IStudioWorkspaceContext _workspaceContext;
    private readonly OrderChannelSyncEngine _syncEngine;
    private readonly OrderChannelRegistry _registry;
    private readonly OrderChannelSyncMonitor _monitor;

    public OrderChannelsController(
        CustomerService customerService,
        IStudioWorkspaceContext workspaceContext,
        OrderChannelSyncEngine syncEngine,
        OrderChannelRegistry registry,
        OrderChannelSyncMonitor monitor)
    {
        _customerService = customerService;
        _workspaceContext = workspaceContext;
        _syncEngine = syncEngine;
        _registry = registry;
        _monitor = monitor;
    }

    [HttpGet("")]
    public async Task<IActionResult> Index(CancellationToken ct)
    {
        var workspace = await _workspaceContext.GetOrCreateAsync(_customerService, ct);
        var profile = await _customerService.GetProfileAsync(workspace.TenantKey, ct);
        var vm = BuildViewModel(workspace.TenantKey, workspace.Name, profile);
        return View(vm);
    }

    [HttpGet("configure/{channel}")]
    public async Task<IActionResult> Configure(string channel, CancellationToken ct)
    {
        var workspace = await _workspaceContext.GetOrCreateAsync(_customerService, ct);
        var profile = await _customerService.GetProfileAsync(workspace.TenantKey, ct);

        if (!Enum.TryParse<OrderChannelType>(channel, true, out var channelType))
        {
            // belki code (string) verildi — catalog'tan dene
            var byCode = OrderChannelCatalog.FindByCode(channel);
            if (byCode is null) return NotFound();
            channelType = byCode.Type;
        }

        var descriptor = OrderChannelCatalog.Find(channelType);
        if (descriptor is null) return NotFound();

        var state = OrderChannelMetadata.Read(profile, channelType);
        var recentRuns = _monitor.Recent(workspace.TenantKey, 50)
            .Where(r => r.Channel == channelType)
            .Take(10)
            .ToList();

        var vm = new OrderChannelConfigureViewModel(
            TenantKey: workspace.TenantKey,
            TenantName: workspace.Name,
            Descriptor: descriptor,
            State: state,
            RecentRuns: recentRuns);

        return View(vm);
    }

    [HttpPost("save")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Save(OrderChannelEditInput input, CancellationToken ct)
    {
        var workspace = await _workspaceContext.GetOrCreateAsync(_customerService, ct);
        var profile = await _customerService.GetProfileAsync(workspace.TenantKey, ct);
        if (profile is null)
        {
            TempData["StudioMessage"] = "Tenant profili bulunamadi.";
            return RedirectToAction(nameof(Index));
        }

        if (!Enum.TryParse<OrderChannelType>(input.Channel, true, out var channelType))
        {
            TempData["StudioMessage"] = "Bilinmeyen kanal kodu.";
            return RedirectToAction(nameof(Index));
        }

        var descriptor = OrderChannelCatalog.Find(channelType);
        if (descriptor is null || descriptor.Stage == OrderChannelStage.ComingSoon)
        {
            TempData["StudioMessage"] = $"{descriptor?.DisplayName ?? channelType.ToString()} bu surumde aktif degil.";
            return RedirectToAction(nameof(Index));
        }

        var metadata = new Dictionary<string, string>(profile.Metadata, StringComparer.OrdinalIgnoreCase);
        var fieldValues = input.Fields ?? new Dictionary<string, string>();

        OrderChannelMetadata.Apply(metadata, channelType, input.Enabled, input.DefaultProvider, fieldValues);

        await _customerService.UpsertAsync(workspace.TenantKey, new UpsertCustomerRequest
        {
            TenantKey = workspace.TenantKey,
            Name = profile.Name,
            IsActive = profile.IsActive,
            AllowedProviders = profile.AllowedProviders.ToList(),
            NotificationTargets = profile.NotificationTargets.ToList(),
            Metadata = metadata
        }, ct);

        TempData["StudioMessage"] = $"{descriptor.DisplayName} ayarlari kaydedildi.";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost("test/{channel}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Test(string channel, CancellationToken ct)
    {
        var workspace = await _workspaceContext.GetOrCreateAsync(_customerService, ct);
        var profile = await _customerService.GetProfileAsync(workspace.TenantKey, ct);

        if (profile is null || !Enum.TryParse<OrderChannelType>(channel, true, out var channelType))
        {
            TempData["StudioMessage"] = "Test edilecek kanal bulunamadi.";
            return RedirectToAction(nameof(Index));
        }

        var state = OrderChannelMetadata.Read(profile, channelType);
        if (!_registry.TryGet(channelType, out var adapter))
        {
            TempData["StudioMessage"] = "Kanal adapter'i kayitli degil.";
            return RedirectToAction(nameof(Index));
        }

        var creds = new OrderChannelCredentials(workspace.TenantKey, channelType, state.Fields);
        var result = await adapter.TestConnectionAsync(creds, ct);
        TempData["StudioMessage"] = (result.Success ? "[OK] " : "[HATA] ") + result.Message;
        return RedirectToAction(nameof(Index));
    }

    [HttpPost("sync/{channel}")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Sync(string channel, CancellationToken ct)
    {
        var workspace = await _workspaceContext.GetOrCreateAsync(_customerService, ct);

        if (!Enum.TryParse<OrderChannelType>(channel, true, out var channelType))
        {
            TempData["StudioMessage"] = "Senkronize edilecek kanal bulunamadi.";
            return RedirectToAction(nameof(Index));
        }

        var run = await _syncEngine.SyncChannelAsync(workspace.TenantKey, channelType, includeSinceLast: true, ct);
        var prefix = run.Success ? "[OK]" : "[HATA]";
        TempData["StudioMessage"] =
            $"{prefix} {OrderChannelCatalog.Find(channelType)?.DisplayName ?? channel} sync: {run.Message} (cekilen: {run.FetchedCount})";
        return RedirectToAction(nameof(Index));
    }

    [HttpPost("sync-all")]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> SyncAll(CancellationToken ct)
    {
        var workspace = await _workspaceContext.GetOrCreateAsync(_customerService, ct);
        var runs = await _syncEngine.SyncAllEnabledAsync(workspace.TenantKey, ct);
        var totalFetched = runs.Sum(r => r.FetchedCount);
        var ok = runs.Count(r => r.Success);
        TempData["StudioMessage"] =
            $"Toplu sync tamamlandi. Aktif kanal: {runs.Count}, basarili: {ok}, toplam cekilen: {totalFetched}";
        return RedirectToAction(nameof(Index));
    }

    private OrderChannelsViewModel BuildViewModel(string tenantKey, string tenantName, CustomerProfileDto? profile)
    {
        var states = OrderChannelMetadata.ReadAll(profile);
        var statesByType = states.ToDictionary(s => s.Channel);

        var rows = OrderChannelCatalog.All.Select(d => new OrderChannelRow(
            Descriptor: d,
            State: statesByType.TryGetValue(d.Type, out var s) ? s : new OrderChannelState { Channel = d.Type })
        ).ToList();

        return new OrderChannelsViewModel(
            TenantKey: tenantKey,
            TenantName: tenantName,
            Rows: rows,
            RecentRuns: _monitor.Recent(tenantKey, 15));
    }
}

public sealed class OrderChannelEditInput
{
    public string Channel { get; set; } = string.Empty;
    public bool Enabled { get; set; }
    public string? DefaultProvider { get; set; }
    public Dictionary<string, string>? Fields { get; set; }
}

public sealed record OrderChannelsViewModel(
    string TenantKey,
    string TenantName,
    IReadOnlyList<OrderChannelRow> Rows,
    IReadOnlyList<OrderChannelSyncRun> RecentRuns);

public sealed record OrderChannelRow(
    OrderChannelDescriptor Descriptor,
    OrderChannelState State);

public sealed record OrderChannelConfigureViewModel(
    string TenantKey,
    string TenantName,
    OrderChannelDescriptor Descriptor,
    OrderChannelState State,
    IReadOnlyList<OrderChannelSyncRun> RecentRuns);
