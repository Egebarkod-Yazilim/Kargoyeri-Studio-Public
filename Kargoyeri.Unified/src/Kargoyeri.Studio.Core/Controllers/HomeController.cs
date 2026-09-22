using System.Diagnostics;
using Kargoyeri.Application.Services;
using Kargoyeri.Contracts.Dtos;
using Kargoyeri.Contracts.Enums;
using Kargoyeri.Studio.Core.Infrastructure;
using Kargoyeri.Studio.Core.Models;
using Microsoft.AspNetCore.Mvc;

namespace Kargoyeri.Studio.Core.Controllers;

public sealed class HomeController : Controller
{
    private readonly CustomerService _customerService;
    private readonly ProviderCatalogService _providerCatalogService;
    private readonly StudioProviderStatusService _statusService;
    private readonly CargoOrchestrator _orchestrator;
    private readonly NotificationService _notificationService;
    private readonly IStudioWorkspaceContext _workspaceContext;
    private readonly WorkerStatusTracker _workerTracker;
    private readonly DashboardPreferenceStore _dashboardPreferenceStore;

    public HomeController(
        CustomerService customerService,
        ProviderCatalogService providerCatalogService,
        StudioProviderStatusService statusService,
        CargoOrchestrator orchestrator,
        NotificationService notificationService,
        IStudioWorkspaceContext workspaceContext,
        WorkerStatusTracker workerTracker,
        DashboardPreferenceStore dashboardPreferenceStore)
    {
        _customerService = customerService;
        _providerCatalogService = providerCatalogService;
        _statusService = statusService;
        _orchestrator = orchestrator;
        _notificationService = notificationService;
        _workspaceContext = workspaceContext;
        _workerTracker = workerTracker;
        _dashboardPreferenceStore = dashboardPreferenceStore;
    }

    public async Task<IActionResult> Index(CancellationToken cancellationToken)
    {
        // P3-#2 — Musteri kullanicilar Shipments'a yonlendirilmek yerine ozel
        // gorsel dashboard goruyor (KPI widget'lari, trend, son hareketler).
        if (!User.IsInRole(StudioRoles.Admin))
        {
            return await CustomerIndexAsync(cancellationToken);
        }

        var workspace = await _workspaceContext.GetOrCreateAsync(_customerService, cancellationToken);
        var profile = await _customerService.GetProfileAsync(workspace.TenantKey, cancellationToken);
        var branding = WorkspaceFeatureMetadata.ReadBranding(profile);
        var allowedProviders = profile?.AllowedProviders.ToHashSet() ?? new HashSet<CargoProviderTypeDto>();
        var providers = _providerCatalogService.List()
            .Where(x => Enum.TryParse<CargoProviderTypeDto>(x.ProviderCode, true, out var provider) && allowedProviders.Contains(provider))
            .ToArray();
        var statuses = new List<Kargoyeri.Contracts.Dtos.ProviderIntegrationStatusDto>();

        foreach (var provider in providers)
        {
            if (Enum.TryParse<CargoProviderTypeDto>(provider.ProviderCode, true, out var providerType))
            {
                statuses.Add(await _statusService.GetAsync(workspace.TenantKey, providerType, cancellationToken));
            }
        }

        var shipments = (await _orchestrator.ListShipmentsAsync(workspace.TenantKey, cancellationToken))
            .Where(x => allowedProviders.Contains(x.Provider))
            .ToArray();
        var allowedShipmentReferences = shipments
            .Select(x => x.ShipmentReference)
            .ToHashSet(StringComparer.OrdinalIgnoreCase);
        var notifications = (await _notificationService.ListAsync(workspace.TenantKey, null, cancellationToken))
            .Where(x => allowedShipmentReferences.Contains(x.ShipmentReference))
            .ToArray();
        var checklist = BuildChecklist(profile, statuses, shipments, notifications);
        var completedChecklistItems = checklist.Count(x => x.IsDone);

        // ── Grafik hesaplamaları ───────────────────────────────────────────────
        var shipmentsByStatus = shipments
            .GroupBy(x => x.Status.ToString())
            .ToDictionary(g => g.Key, g => g.Count());

        var shipmentsByProvider = shipments
            .GroupBy(x => x.Provider.ToString())
            .ToDictionary(g => g.Key, g => g.Count());

        var today = DateTimeOffset.UtcNow.Date;
        var trendLabels = Enumerable.Range(0, 7)
            .Select(i => today.AddDays(-6 + i))
            .ToList();
        var trendCounts = trendLabels
            .Select(day => shipments.Count(s => s.CreatedAtUtc.UtcDateTime.Date == day))
            .ToList();

        var notificationsByChannel = notifications
            .GroupBy(x => x.Channel.ToString())
            .ToDictionary(
                g => g.Key,
                g => (
                    Total: g.Count(),
                    Delivered: g.Count(n => n.Status == Kargoyeri.Contracts.Enums.NotificationDeliveryStatusDto.Delivered),
                    Failed: g.Count(n => n.Status == Kargoyeri.Contracts.Enums.NotificationDeliveryStatusDto.Failed)
                )
            );
        var visibleWidgets = await _dashboardPreferenceStore.GetAsync(workspace.TenantKey, cancellationToken);

        return View(new DashboardViewModel
        {
            WorkspaceCode = workspace.TenantKey,
            WorkspaceName = workspace.Name,
            ProviderCount = providers.Length,
            ReadyProviderCount = statuses.Count(x => x.CanCreateShipment && x.Configured),
            ShipmentCount = shipments.Length,
            NotificationCount = notifications.Length,
            CurrentPhaseLabel = DetermineCurrentPhase(statuses, shipments),
            CompletedChecklistItems = completedChecklistItems,
            TotalChecklistItems = checklist.Count,
            ProviderStatuses = statuses.OrderBy(x => x.Provider.ToString()).ToList(),
            Checklist = checklist,
            RecentShipments = shipments.Take(5).ToList(),
            RecentNotifications = notifications.Take(5).ToList(),
            WorkerStatus = _workerTracker.Last,
            ShipmentsByStatus = shipmentsByStatus,
            ShipmentsByProvider = shipmentsByProvider,
            TrendLabels = trendLabels.Select(d => d.ToString("dd.MM")).ToList(),
            TrendCounts = trendCounts,
            NotificationsByChannel = notificationsByChannel,
            VisibleWidgets = visibleWidgets,
            PhaseCards =
            [
                new()
                {
                    Phase = "Faz 1",
                    Title = "Calisan Kargo Urunu",
                    Description = "Ayar, gonderi, log ve bildirim ekranlariyla tek basina kullanilabilen urun tabani.",
                    Highlights = new[] { "Provider ayarlari", "Tek provider ile uc uca akis", "Log ve bildirim kaydi", "Yonetim paneli" },
                    Status = "Aktif",
                    Outcome = "Kendi basina calisabilen urun paneli",
                    Deliverables = new[] { "Workspace akisi", "Provider ekranlari", "Shipment operasyonu", "Bildirim merkezi" },
                    NextSteps = new[] { "Ilk canli provider", "Daha net onboarding", "Operasyon raporlari" }
                },
                new()
                {
                    Phase = "Faz 2",
                    Title = "Operasyonel Saglamlik",
                    Description = "Canli transportlar, timeout, retry, raporlama ve daha guclu durum takibi.",
                    Highlights = new[] { "MNG / UPS modern transport", "Worker guclendirmesi", "Daha fazla bildirim", "Gercek veritabani" },
                    Status = "Siradaki",
                    Outcome = "Operasyon ekibinin guvenerek kullanacagi stabil urun",
                    Deliverables = new[] { "Retry ve timeout", "Gercek DB", "Canli provider adapterlari", "Daha derin izleme" },
                    NextSteps = new[] { "MNG baglantisi", "UPS baglantisi", "Bildirim kanallari" }
                },
                new()
                {
                    Phase = "Faz 3",
                    Title = "Embed Edilebilir Host",
                    Description = "Pazaryeri projesine menu ve sayfa gibi oturan ama ayni cekirdegi kullanan mod.",
                    Highlights = new[] { "Sag menu entegrasyonu", "Ayni core", "Standalone + embedded", "Satisa hazir dagitim" },
                    Status = "Planlandi",
                    Outcome = "Ayni urunun hem bagimsiz hem gomulu satilabilmesi",
                    Deliverables = new[] { "Embedded host", "Tema uyumu", "Menu entegrasyonu", "Dagitim paketi" },
                    NextSteps = new[] { "Pazaryeri hostuna montaj", "Yetki uyumu", "Musteriye ozel dagitim" }
                }
            ]
        });
    }

    private List<ChecklistItemViewModel> BuildChecklist(
        CustomerProfileDto? profile,
        IReadOnlyCollection<Kargoyeri.Contracts.Dtos.ProviderIntegrationStatusDto> statuses,
        IReadOnlyCollection<Kargoyeri.Contracts.Dtos.ShipmentListItemResponse> shipments,
        IReadOnlyCollection<Kargoyeri.Contracts.Dtos.NotificationMessageDto> notifications)
    {
        var readyProviders = statuses.Count(x => x.CanCreateShipment && x.Configured);

        return
        [
            new()
            {
                Title = "Workspace tanimi hazir",
                Description = "Musteri kaydi aktif ve panel hangi sirket icin calisacagini biliyor.",
                IsDone = profile?.IsActive == true,
                ActionText = "Workspace ayarina git",
                ActionUrl = Url.Action("Index", "Workspaces") ?? "/Workspaces"
            },
            new()
            {
                Title = "En az bir bildirim hedefi var",
                Description = "Hata veya basari oldugunda sistemin kime donecegi belli.",
                IsDone = profile?.NotificationTargets.Any(x => x.IsEnabled) == true,
                ActionText = "Workspace bildirimlerini tamamla",
                ActionUrl = Url.Action("Index", "Workspaces") ?? "/Workspaces"
            },
            new()
            {
                Title = "Kullanima hazir provider var",
                Description = "En az bir kargo firmasi ayarlanmis ve gonderi olusturabilir durumda.",
                IsDone = readyProviders > 0,
                ActionText = "Provider ayarlarina git",
                ActionUrl = Url.Action("Index", "Providers") ?? "/Providers"
            },
            new()
            {
                Title = "Ilk gonderi olusturuldu",
                Description = "Urun artik sadece hazir degil, kullanilmaya da basladi.",
                IsDone = shipments.Count > 0,
                ActionText = "Gonderi olustur",
                ActionUrl = Url.Action("Index", "Shipments") ?? "/Shipments"
            },
            new()
            {
                Title = "Operasyon kaydi olustu",
                Description = "Bildirim veya log akisindan en az biri akmaya basladi.",
                IsDone = notifications.Count > 0,
                ActionText = "Bildirimleri incele",
                ActionUrl = Url.Action("Index", "Notifications") ?? "/Notifications"
            }
        ];
    }

    private static string DetermineCurrentPhase(
        IReadOnlyCollection<Kargoyeri.Contracts.Dtos.ProviderIntegrationStatusDto> statuses,
        IReadOnlyCollection<Kargoyeri.Contracts.Dtos.ShipmentListItemResponse> shipments)
    {
        var readyProviders = statuses.Count(x => x.CanCreateShipment && x.Configured);

        if (shipments.Count > 0 && readyProviders > 0)
        {
            return "Faz 1 aktif: calisan standalone urun";
        }

        if (readyProviders > 0)
        {
            return "Faz 1 kurulum asamasi: ilk operasyon akisi hazirlaniyor";
        }

        return "Faz 1 baslangic asamasi: provider ve workspace kurulumu bekleniyor";
    }

    /// <summary>
    /// P3-#2 — Musteri kullanicilar icin sade, gorsel dashboard.
    /// Admin'in detayli phase/checklist gorunumu yerine KPI + trend + son hareketler gosterir.
    /// </summary>
    private async Task<IActionResult> CustomerIndexAsync(CancellationToken cancellationToken)
    {
        var workspace = await _workspaceContext.GetOrCreateAsync(_customerService, cancellationToken);
        var profile = await _customerService.GetProfileAsync(workspace.TenantKey, cancellationToken);
        var branding = WorkspaceFeatureMetadata.ReadBranding(profile);
        var allowedProviders = profile?.AllowedProviders.ToHashSet() ?? new HashSet<CargoProviderTypeDto>();

        var shipments = (await _orchestrator.ListShipmentsAsync(workspace.TenantKey, cancellationToken))
            .Where(x => allowedProviders.Count == 0 || allowedProviders.Contains(x.Provider))
            .ToArray();

        var nowUtc = DateTimeOffset.UtcNow;
        var todayUtc = nowUtc.Date;

        var todayCount = shipments.Count(s => s.CreatedAtUtc.UtcDateTime.Date == todayUtc);
        var last7Cutoff = todayUtc.AddDays(-6);
        var last7 = shipments.Where(s => s.CreatedAtUtc.UtcDateTime.Date >= last7Cutoff).ToArray();
        var inTransit = shipments.Count(s => s.Status == ShipmentStatusDto.InTransit ||
                                              s.Status == ShipmentStatusDto.LabelReady ||
                                              s.Status == ShipmentStatusDto.ProviderAccepted);
        var monthCutoff = new DateTime(nowUtc.Year, nowUtc.Month, 1, 0, 0, 0, DateTimeKind.Utc);
        var deliveredThisMonth = shipments.Count(s => s.Status == ShipmentStatusDto.Delivered &&
                                                       s.CreatedAtUtc.UtcDateTime >= monthCutoff);

        var trendDays = Enumerable.Range(0, 7)
            .Select(i => todayUtc.AddDays(-6 + i))
            .ToList();
        var trendCounts = trendDays
            .Select(day => shipments.Count(s => s.CreatedAtUtc.UtcDateTime.Date == day))
            .ToList();
        var trendLabels = trendDays.Select(d => d.ToString("dd.MM")).ToList();
        var trendPeak = trendCounts.Count == 0 ? 1 : Math.Max(1, trendCounts.Max());

        var byStatus = shipments
            .GroupBy(x => x.Status.ToString())
            .OrderByDescending(g => g.Count())
            .ToDictionary(g => g.Key, g => g.Count());

        var byProvider = shipments
            .GroupBy(x => x.Provider.ToString())
            .OrderByDescending(g => g.Count())
            .ToDictionary(g => g.Key, g => g.Count());

        var recent = shipments
            .OrderByDescending(s => s.CreatedAtUtc)
            .Take(6)
            .ToArray();

        return View("Customer", new CustomerDashboardViewModel
        {
            WorkspaceCode      = workspace.TenantKey,
            WorkspaceName      = workspace.Name,
            BrandDisplayName   = branding.DisplayName,
            PartnerLabel       = branding.PartnerLabel,
            AccentColor        = branding.AccentColor,
            TodayCount         = todayCount,
            Last7DaysCount     = last7.Length,
            InTransitCount     = inTransit,
            DeliveredThisMonth = deliveredThisMonth,
            TrendCounts        = trendCounts,
            TrendLabels        = trendLabels,
            TrendPeak          = trendPeak,
            ShipmentsByStatus  = byStatus,
            ShipmentsByProvider= byProvider,
            RecentShipments    = recent
        });
    }

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error()
    {
        return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
    }
}
