using Kargoyeri.Application.Abstractions.Persistence;
using Kargoyeri.Application.Services;
using Kargoyeri.Contracts.Dtos;
using Kargoyeri.Contracts.Enums;
using Kargoyeri.Studio.Core.Infrastructure;
using Kargoyeri.Studio.Core.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Kargoyeri.Studio.Core.Controllers;

public sealed class ShipmentsController : Controller
{
    private readonly CargoOrchestrator _orchestrator;
    private readonly ProviderCatalogService _providerCatalogService;
    private readonly StudioProviderStatusService _statusService;
    private readonly NotificationService _notificationService;
    private readonly CustomerService _customerService;
    private readonly IStudioWorkspaceContext _workspaceContext;
    private readonly ShipmentAddressValidationService _addressValidationService;
    private readonly ShipmentAnomalyDetectionService _anomalyDetectionService;
    private readonly IShipmentRepository _shipmentRepository;
    private readonly ShipmentArchiveIndex _archiveIndex;
    private readonly Kargoyeri.Application.Abstractions.Persistence.IUnitOfWork _unitOfWork;

    public ShipmentsController(
        CargoOrchestrator orchestrator,
        ProviderCatalogService providerCatalogService,
        StudioProviderStatusService statusService,
        NotificationService notificationService,
        CustomerService customerService,
        IStudioWorkspaceContext workspaceContext,
        ShipmentAddressValidationService addressValidationService,
        ShipmentAnomalyDetectionService anomalyDetectionService,
        IShipmentRepository shipmentRepository,
        ShipmentArchiveIndex archiveIndex,
        Kargoyeri.Application.Abstractions.Persistence.IUnitOfWork unitOfWork)
    {
        _orchestrator = orchestrator;
        _providerCatalogService = providerCatalogService;
        _statusService = statusService;
        _notificationService = notificationService;
        _customerService = customerService;
        _workspaceContext = workspaceContext;
        _addressValidationService = addressValidationService;
        _anomalyDetectionService = anomalyDetectionService;
        _shipmentRepository = shipmentRepository;
        _archiveIndex = archiveIndex;
        _unitOfWork = unitOfWork;
    }

    [HttpGet]
    public async Task<IActionResult> Index(
        [FromQuery] int page = 1,
        [FromQuery] string? search = null,
        [FromQuery] string? status = null,
        [FromQuery] string? provider = null,
        [FromQuery] string? source = null,
        [FromQuery] bool showArchived = false,
        CancellationToken cancellationToken = default)
    {
        var workspace = await _workspaceContext.GetOrCreateAsync(_customerService, cancellationToken);
        var filter = new ShipmentFilterViewModel { Search = search, Status = status, Provider = provider, Source = source, ShowArchived = showArchived };
        var vm = await BuildViewModelAsync(
            workspace.TenantKey,
            workspace.Name,
            new ShipmentCreateFormViewModel(),
            null,
            page,
            filter,
            cancellationToken);
        vm.IsAdmin = User.IsInRole(StudioRoles.Admin);
        return View(vm);
    }

    /// <summary>
    /// Müşteri arayüzü: mevcut Pending/Failed bir gönderiye kargo firması atar ve API'ye gönderir.
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Policy = StudioRoles.CanWrite)]
    public async Task<IActionResult> SubmitToCargo(
        [FromForm] string shipmentReference,
        [FromForm] CargoProviderTypeDto selectedProvider,
        CancellationToken cancellationToken)
    {
        var workspace = await _workspaceContext.GetOrCreateAsync(_customerService, cancellationToken);
        var customerProfile = await _customerService.GetProfileAsync(workspace.TenantKey, cancellationToken);
        var allowedProviders = GetAllowedProviders(customerProfile);
        var shipment = await GetVisibleShipmentAsync(shipmentReference, workspace.TenantKey, allowedProviders, cancellationToken);
        if (shipment is null || !allowedProviders.Contains(selectedProvider))
        {
            return Forbid();
        }

        try
        {
            var result = await _orchestrator.SubmitShipmentAsync(
                shipmentReference, workspace.TenantKey, selectedProvider, cancellationToken);

            TempData["StudioMessage"] = result.Status.ToString().Contains("Failed")
                ? $"Gonderi gonderilemedi: {result.Message}"
                : $"Kargo entegrasyonu baslatildi — {result.Provider} / {result.Status}";
        }
        catch (InvalidOperationException ex)
        {
            TempData["StudioMessage"] = $"Hata: {ex.Message}";
        }

        return RedirectToAction(nameof(Index));
    }

    [HttpGet]
    public async Task<IActionResult> Export(
        [FromQuery] string? search = null,
        [FromQuery] string? status = null,
        [FromQuery] string? provider = null,
        CancellationToken cancellationToken = default)
    {
        var workspace = await _workspaceContext.GetOrCreateAsync(_customerService, cancellationToken);
        var customerProfile = await _customerService.GetProfileAsync(workspace.TenantKey, cancellationToken);
        var allowedProviders = GetAllowedProviders(customerProfile);
        var all = await ListVisibleShipmentsAsync(workspace.TenantKey, allowedProviders, new ShipmentFilterViewModel(), cancellationToken);
        var filtered = ApplyFilter(all, new ShipmentFilterViewModel { Search = search, Status = status, Provider = provider });

        var csv = BuildCsv(filtered.AsEnumerable());
        var bytes = System.Text.Encoding.UTF8.GetBytes(csv);
        var fileName = $"gonderiler-{workspace.TenantKey}-{DateTime.Now:yyyyMMdd-HHmmss}.csv";
        return File(bytes, "text/csv; charset=utf-8", fileName);
    }

    private static IReadOnlyCollection<ShipmentListItemResponse> ApplyFilter(
        IReadOnlyCollection<ShipmentListItemResponse> shipments,
        ShipmentFilterViewModel filter)
    {
        var q = shipments.AsEnumerable();

        if (!string.IsNullOrWhiteSpace(filter.Search))
        {
            var s = filter.Search.Trim();
            q = q.Where(x =>
                x.ShipmentReference.Contains(s, StringComparison.OrdinalIgnoreCase) ||
                x.OrderReference.Contains(s, StringComparison.OrdinalIgnoreCase) ||
                (x.TrackingNumber?.Contains(s, StringComparison.OrdinalIgnoreCase) == true));
        }

        if (!string.IsNullOrWhiteSpace(filter.Status) &&
            Enum.TryParse<ShipmentStatusDto>(filter.Status, true, out var parsedStatus))
            q = q.Where(x => x.Status == parsedStatus);

        if (!string.IsNullOrWhiteSpace(filter.Provider) &&
            Enum.TryParse<CargoProviderTypeDto>(filter.Provider, true, out var parsedProvider))
            q = q.Where(x => x.Provider == parsedProvider);

        if (!string.IsNullOrWhiteSpace(filter.Source))
        {
            var src = filter.Source.Trim();
            if (string.Equals(src, "_none", StringComparison.OrdinalIgnoreCase))
            {
                q = q.Where(x => !x.SourceChannel.HasValue && string.IsNullOrWhiteSpace(x.SourceChannelCode));
            }
            else if (Enum.TryParse<OrderSourceChannelDto>(src, true, out var parsedSrc))
            {
                q = q.Where(x => x.SourceChannel == parsedSrc);
            }
            else
            {
                // Free-form code match (Shopify-acme, manual-op12, vs.)
                q = q.Where(x => string.Equals(x.SourceChannelCode, src, StringComparison.OrdinalIgnoreCase));
            }
        }

        return q.ToArray();
    }

    private static string BuildCsv(IEnumerable<ShipmentListItemResponse> shipments)
    {
        var sb = new System.Text.StringBuilder();
        sb.AppendLine("ShipmentReference;OrderReference;Provider;Status;TrackingNumber;CreatedAt;UpdatedAt");
        foreach (var s in shipments)
        {
            sb.AppendLine(string.Join(";",
                Escape(s.ShipmentReference),
                Escape(s.OrderReference),
                Escape(s.Provider.ToString()),
                Escape(s.Status.ToString()),
                Escape(s.TrackingNumber ?? ""),
                s.CreatedAtUtc.ToString("yyyy-MM-dd HH:mm"),
                s.UpdatedAtUtc.ToString("yyyy-MM-dd HH:mm")));
        }
        return sb.ToString();

        static string Escape(string v) => v.Contains(';') || v.Contains('"') || v.Contains('\n')
            ? $"\"{v.Replace("\"", "\"\"")}\"" : v;
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Preview(ShipmentWorkspaceViewModel pageModel, CancellationToken cancellationToken)
    {
        var workspace = await _workspaceContext.GetOrCreateAsync(_customerService, cancellationToken);
        var form = pageModel.CreateForm;
        var providerOptions = await BuildProviderOptionsAsync(workspace.TenantKey, cancellationToken);
        var preview = default(ProviderRequestPreviewResponse);
        AddressValidationSummaryViewModel? validation = null;
        ShipmentAnomalyReportViewModel? anomaly = null;

        NormalizeSelectedProvider(form, providerOptions);
        ValidateProviderSelection(form.Provider, providerOptions, allowPendingProviders: true);

        if (ModelState.IsValid)
        {
            try
            {
                var request = MapCreateRequest(workspace.TenantKey, form);
                validation = await _addressValidationService.ValidateAsync(workspace.TenantKey, request.Recipient, cancellationToken);
                ShipmentAddressValidationService.ApplyNormalization(request.Recipient, validation);
                if (validation.HasBlockingIssues)
                {
                    ModelState.AddModelError(string.Empty, validation.StatusText);
                }

                anomaly = await BuildAnomalyAsync(workspace.TenantKey, form, cancellationToken);
                ShipmentAnomalyDetectionService.ApplyMetadata(request.Metadata, anomaly);

                if (!ModelState.IsValid)
                {
                    return View("Index", await BuildViewModelAsync(
                        workspace.TenantKey,
                        workspace.Name,
                        form,
                        null,
                        1,
                        new ShipmentFilterViewModel(),
                        cancellationToken,
                        providerOptions,
                        validation,
                        anomaly));
                }

                preview = await _providerCatalogService.PreviewCreateAsync(
                    workspace.TenantKey,
                    form.Provider,
                    request,
                    cancellationToken);
            }
            catch (InvalidOperationException ex)
            {
                ModelState.AddModelError(string.Empty, ex.Message);
            }
        }

        return View("Index", await BuildViewModelAsync(
            workspace.TenantKey,
            workspace.Name,
            form,
            preview,
            1,
            new ShipmentFilterViewModel(),
            cancellationToken,
            providerOptions,
            validation,
            anomaly));
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Policy = StudioRoles.CanWrite)]
    public async Task<IActionResult> Create(ShipmentWorkspaceViewModel pageModel, CancellationToken cancellationToken)
    {
        var workspace = await _workspaceContext.GetOrCreateAsync(_customerService, cancellationToken);
        var form = pageModel.CreateForm;
        var providerOptions = await BuildProviderOptionsAsync(workspace.TenantKey, cancellationToken);
        AddressValidationSummaryViewModel? validation = null;
        ShipmentAnomalyReportViewModel? anomaly = null;
        CreateShipmentRequest? request = null;

        NormalizeSelectedProvider(form, providerOptions);
        ValidateProviderSelection(form.Provider, providerOptions, allowPendingProviders: false);

        if (ModelState.IsValid)
        {
            request = MapCreateRequest(workspace.TenantKey, form);
            validation = await _addressValidationService.ValidateAsync(workspace.TenantKey, request.Recipient, cancellationToken);
            ShipmentAddressValidationService.ApplyNormalization(request.Recipient, validation);
            if (validation.HasBlockingIssues)
            {
                ModelState.AddModelError(string.Empty, validation.StatusText);
            }

            anomaly = await BuildAnomalyAsync(workspace.TenantKey, form, cancellationToken);
            ShipmentAnomalyDetectionService.ApplyMetadata(request.Metadata, anomaly);
        }

        if (!ModelState.IsValid)
        {
            return View("Index", await BuildViewModelAsync(
                workspace.TenantKey,
                workspace.Name,
                form,
                null,
                1,
                new ShipmentFilterViewModel(),
                cancellationToken,
                providerOptions,
                validation,
                anomaly));
        }

        try
        {
            var response = await _orchestrator.CreateShipmentAsync(
                request!,
                cancellationToken);

            TempData["StudioMessage"] = string.IsNullOrWhiteSpace(response.Message)
                ? $"{response.ShipmentReference} olusturuldu."
                : $"{response.ShipmentReference} olusturuldu. {response.Message}";

            return RedirectToAction(nameof(Detail), new { shipmentReference = response.ShipmentReference });
        }
        catch (InvalidOperationException ex)
        {
            ModelState.AddModelError(string.Empty, ex.Message);
        }

        return View("Index", await BuildViewModelAsync(
            workspace.TenantKey,
            workspace.Name,
            form,
            null,
            1,
            new ShipmentFilterViewModel(),
            cancellationToken,
            providerOptions,
            validation,
            anomaly));
    }

    [HttpGet]
    public async Task<IActionResult> Detail(string shipmentReference, CancellationToken cancellationToken)
    {
        var workspace = await _workspaceContext.GetOrCreateAsync(_customerService, cancellationToken);
        var customerProfile = await _customerService.GetProfileAsync(workspace.TenantKey, cancellationToken);
        var shipment = await GetVisibleShipmentAsync(
            shipmentReference,
            workspace.TenantKey,
            GetAllowedProviders(customerProfile),
            cancellationToken);
        if (shipment is null)
        {
            return NotFound();
        }

        return View(new ShipmentDetailPageViewModel
        {
            WorkspaceCode = workspace.TenantKey,
            WorkspaceName = workspace.Name,
            Shipment = shipment,
            Logs = await _orchestrator.ListLogsAsync(shipmentReference, workspace.TenantKey, cancellationToken),
            Notifications = await _notificationService.ListAsync(workspace.TenantKey, shipmentReference, cancellationToken),
            AnomalyReport = ShipmentAnomalyDetectionService.TryReadMetadata(shipment.Metadata)
                ?? await _anomalyDetectionService.AnalyzeAsync(workspace.TenantKey, new ShipmentAnomalyInput
                {
                    Provider = shipment.Provider.ToString(),
                    RecipientCity = shipment.Recipient?.City,
                    CollectionAmount = shipment.CollectionAmount,
                    Weight = shipment.Packages.Sum(x => x.Weight),
                    Desi = shipment.Packages.Sum(x => x.Desi)
                }, cancellationToken),
            ActionForm = new ShipmentActionFormViewModel
            {
                ShipmentReference = shipmentReference
            }
        });
    }

    [HttpGet]
    public IActionResult Log([FromQuery] string? shipmentReference = null, [FromQuery] string? shipmentRef = null)
    {
        var resolvedReference = string.IsNullOrWhiteSpace(shipmentReference) ? shipmentRef : shipmentReference;
        return RedirectToAction("Index", "Audit", new { shipmentRef = resolvedReference });
    }

    [HttpGet]
    public IActionResult Logs([FromQuery] string? shipmentReference = null, [FromQuery] string? shipmentRef = null)
    {
        var resolvedReference = string.IsNullOrWhiteSpace(shipmentReference) ? shipmentRef : shipmentReference;
        return RedirectToAction("Index", "Audit", new { shipmentRef = resolvedReference });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Policy = StudioRoles.CanWrite)]
    public async Task<IActionResult> Refresh(ShipmentActionFormViewModel form, CancellationToken cancellationToken)
    {
        var workspace = await _workspaceContext.GetOrCreateAsync(_customerService, cancellationToken);
        var customerProfile = await _customerService.GetProfileAsync(workspace.TenantKey, cancellationToken);
        if (await GetVisibleShipmentAsync(form.ShipmentReference, workspace.TenantKey, GetAllowedProviders(customerProfile), cancellationToken) is null)
        {
            return Forbid();
        }

        await _orchestrator.RefreshShipmentAsync(form.ShipmentReference, workspace.TenantKey, cancellationToken);
        TempData["StudioMessage"] = $"{form.ShipmentReference} icin durum yenilendi.";
        return RedirectToAction(nameof(Detail), new { shipmentReference = form.ShipmentReference });
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Policy = StudioRoles.CanWrite)]
    public async Task<IActionResult> Cancel(ShipmentActionFormViewModel form, CancellationToken cancellationToken)
    {
        var workspace = await _workspaceContext.GetOrCreateAsync(_customerService, cancellationToken);
        var customerProfile = await _customerService.GetProfileAsync(workspace.TenantKey, cancellationToken);

        // 1) Yetki + tenant scope kontrolu
        var shipment = await GetVisibleShipmentAsync(
            form.ShipmentReference, workspace.TenantKey, GetAllowedProviders(customerProfile), cancellationToken);
        if (shipment is null)
        {
            return Forbid();
        }

        // 2) Iptal nedeni zorunlu (en az 5 karakter — UI tarafinda da validation var)
        if (string.IsNullOrWhiteSpace(form.CancelReason) || form.CancelReason.Trim().Length < 5)
        {
            StudioFlash.Error(TempData, "Iptal nedeni en az 5 karakter olmalidir.");
            return RedirectToAction(nameof(Detail), new { shipmentReference = form.ShipmentReference });
        }

        // 3) Status guard — sadece iptal edilebilir durumlar
        if (!CancelPolicy.IsCancellable(shipment.Status))
        {
            StudioFlash.Error(TempData,
                $"Bu gonderi '{shipment.Status}' durumunda. Iptal islemi sadece " +
                $"Pending / Queued / ProviderAccepted / LabelReady / InTransit durumlarinda yapilabilir.");
            return RedirectToAction(nameof(Detail), new { shipmentReference = form.ShipmentReference });
        }

        // 4) Provider iptal destegi
        if (!CancelPolicy.IsProviderSupported(shipment.Provider))
        {
            StudioFlash.Error(TempData,
                $"{shipment.Provider} kargo firmasi otomatik iptali desteklemiyor. " +
                "Lutfen kargo firmasinin musteri panelinden veya cagri merkezinden iptal isteyin.");
            return RedirectToAction(nameof(Detail), new { shipmentReference = form.ShipmentReference });
        }

        // 5) Provider cagri — friendly error handling
        try
        {
            var reason = $"{form.CancelReason!.Trim()} (iptal eden: {User.Identity?.Name ?? "n/a"})";
            await _orchestrator.CancelShipmentAsync(form.ShipmentReference, workspace.TenantKey, reason, cancellationToken);
            StudioFlash.Success(TempData,
                $"{form.ShipmentReference} icin iptal akisi calistirildi. Detayda son duruma bakin.");
        }
        catch (OperationCanceledException)
        {
            throw;
        }
        catch (Exception ex)
        {
            StudioFlash.Error(TempData,
                $"Iptal sirasinda hata: {ex.Message}. Gonderi durumu degismedi.");
        }

        return RedirectToAction(nameof(Detail), new { shipmentReference = form.ShipmentReference });
    }

    private async Task<ShipmentWorkspaceViewModel> BuildViewModelAsync(
        string workspaceCode,
        string workspaceName,
        ShipmentCreateFormViewModel createForm,
        ProviderRequestPreviewResponse? preview,
        int page,
        ShipmentFilterViewModel filter,
        CancellationToken cancellationToken,
        IReadOnlyCollection<ShipmentProviderChoiceViewModel>? providerOptions = null,
        AddressValidationSummaryViewModel? addressValidation = null,
        ShipmentAnomalyReportViewModel? anomalyReport = null)
    {
        const int pageSize = 25;
        var safePage = Math.Max(1, page);
        var profile = await _customerService.GetProfileAsync(workspaceCode, cancellationToken);
        var allowedProviders = GetAllowedProviders(profile);
        providerOptions ??= await BuildProviderOptionsAsync(workspaceCode, cancellationToken);
        NormalizeSelectedProvider(createForm, providerOptions);

        // DB-side sayfalama + filtreleme: CargoOrchestrator.ListShipmentsPagedAsync repository'nin
        // ListPagedAsync metodunu cagirir, EF tarafinda WHERE/ORDER BY/OFFSET FETCH uretir.
        var (rawPaged, rawTotal) = await _orchestrator.ListShipmentsPagedAsync(
            workspaceCode,
            filter.Search,
            filter.Status,
            filter.Provider,
            safePage,
            pageSize,
            cancellationToken);

        // AllowedProviders guvenlik filtresi: sayfa icindeki yetkisiz provider kayitlarini maskele.
        var pagedShipments = rawPaged.Where(x => allowedProviders.Contains(x.Provider)).ToArray();
        var filteredShipmentsCount = rawTotal;

        // Sipariş kaynağı (SourceChannel) filtresi — DB-side henüz desteklenmiyor; sayfa sonrası uygulanır.
        // Not: chip-filter sayımları için ayrıca tüm tenant'ı ListShipmentsAsync ile çekiyoruz.
        if (!string.IsNullOrWhiteSpace(filter.Source))
        {
            pagedShipments = ApplyFilter(pagedShipments, new ShipmentFilterViewModel { Source = filter.Source }).ToArray();
        }

        // SourceCounts — tüm tenant shipment'ları üzerinden gruplama (chip badge sayıları).
        // Performans notu: küçük-orta tenant için yeterli; ileride DB-side GROUP BY ile değiştirilebilir.
        var sourceCounts = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        try
        {
            var allForCount = await _orchestrator.ListShipmentsAsync(workspaceCode, cancellationToken);
            foreach (var s in allForCount.Where(x => allowedProviders.Contains(x.Provider)))
            {
                string key = s.SourceChannel?.ToString()
                             ?? (string.IsNullOrWhiteSpace(s.SourceChannelCode) ? "_none" : s.SourceChannelCode!);
                sourceCounts[key] = sourceCounts.TryGetValue(key, out var c) ? c + 1 : 1;
            }
        }
        catch
        {
            // Sayımlar opsiyonel — başarısız olursa UI boş chip listesi gösterir, sayfa çalışmaya devam eder.
        }

        // P2-#6 — Arsivlenmis (soft-delete) gonderileri filtrele.
        var archivedSet = await _archiveIndex.GetAsync(_shipmentRepository, workspaceCode, cancellationToken);
        if (!filter.ShowArchived && archivedSet.Count > 0)
        {
            pagedShipments = pagedShipments
                .Where(x => !archivedSet.Contains(x.ShipmentReference))
                .ToArray();
            // Toplam sayim ayrimi: kullaniciya goster sayfa-ici filtreden sonra kalan sayidir.
            filteredShipmentsCount = Math.Max(0, filteredShipmentsCount - archivedSet.Count);
        }

        return new ShipmentWorkspaceViewModel
        {
            WorkspaceCode = workspaceCode,
            WorkspaceName = workspaceName,
            Shipments = pagedShipments,
            ArchivedReferences = archivedSet,
            TotalArchived = archivedSet.Count,
            SourceConfigured = profile is not null &&
                profile.Metadata.TryGetValue("source.apiBaseUrl", out var apiBaseUrl) &&
                !string.IsNullOrWhiteSpace(apiBaseUrl),
            SourceSystemName = profile?.Metadata.TryGetValue("source.systemName", out var systemName) == true ? systemName : string.Empty,
            SourceApiBaseUrl = profile?.Metadata.TryGetValue("source.apiBaseUrl", out var sourceUrl) == true ? sourceUrl : null,
            ReadyProviderCount = providerOptions.Count(x => x.IsReady),
            CreateForm = createForm,
            ProviderOptions = providerOptions,
            Preview = preview,
            Filter = filter,
            IsAdmin = User.IsInRole(StudioRoles.Admin),
            TotalVisibleShipments = filteredShipmentsCount,
            Pagination = new PaginationViewModel { Page = safePage, PageSize = pageSize, TotalCount = filteredShipmentsCount },
            AddressValidation = addressValidation,
            AnomalyReport = anomalyReport,
            SourceCounts = sourceCounts,
            SelectedSource = filter.Source
        };
    }

    private async Task<IReadOnlyCollection<ShipmentProviderChoiceViewModel>> BuildProviderOptionsAsync(
        string workspaceCode,
        CancellationToken cancellationToken)
    {
        var customerProfile = await _customerService.GetProfileAsync(workspaceCode, cancellationToken);
        var allowedProviders = GetAllowedProviders(customerProfile);
        var options = new List<ShipmentProviderChoiceViewModel>();

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

            var status = await _statusService.GetAsync(workspaceCode, provider, cancellationToken);
            options.Add(new ShipmentProviderChoiceViewModel
            {
                Provider = provider,
                ProviderCode = profile.ProviderCode,
                DisplayName = status.CanCreateShipment
                    ? $"{profile.Provider} - Hazir"
                    : status.Configured
                        ? $"{profile.Provider} - Ayar eksik"
                        : $"{profile.Provider} - Kurulum gerekli",
                IsReady = status.CanCreateShipment,
                LiveTransportImplemented = status.LiveTransportImplemented,
                SimulationEnabled = status.SimulationEnabled
            });
        }

        return options
            .OrderByDescending(x => x.IsReady)
            .ThenBy(x => x.DisplayName)
            .ToArray();
    }

    private void ValidateProviderSelection(
        CargoProviderTypeDto provider,
        IReadOnlyCollection<ShipmentProviderChoiceViewModel> providerOptions,
        bool allowPendingProviders)
    {
        if (providerOptions.Count == 0)
        {
            ModelState.AddModelError(string.Empty, "Kargo firmasi katalogu bulunamadi.");
            return;
        }

        var selected = providerOptions.FirstOrDefault(x => x.Provider == provider);
        if (selected is null)
        {
            ModelState.AddModelError("CreateForm.Provider", "Gecerli bir kargo firmasi sec.");
            return;
        }

        if (!allowPendingProviders && !selected.IsReady)
        {
            ModelState.AddModelError("CreateForm.Provider", "Secilen kargo firmasi henuz gonderi olusturmaya hazir degil.");
        }
    }

    private static void NormalizeSelectedProvider(
        ShipmentCreateFormViewModel form,
        IReadOnlyCollection<ShipmentProviderChoiceViewModel> providerOptions)
    {
        if (providerOptions.Count == 0)
        {
            return;
        }

        var selected = providerOptions.FirstOrDefault(x => x.Provider == form.Provider);
        if (selected is null)
        {
            form.Provider = providerOptions.First().Provider;
            return;
        }

        if (!selected.IsReady && string.IsNullOrWhiteSpace(form.OrderReference))
        {
            var readyOption = providerOptions.FirstOrDefault(x => x.IsReady);
            if (readyOption is not null)
            {
                form.Provider = readyOption.Provider;
            }
        }
    }

    // ── P2-#6 Soft-delete ─────────────────────────────────────────────────────

    /// <summary>
    /// Gonderiyi soft-delete (arsiv) eder. Veri silinmez; sadece
    /// CargoShipment.Metadata["shipment.archived"] = "true" yazilir ve liste
    /// ekraninda varsayilan olarak gizlenir. Yetkili kullanici "Arsivlenenleri Goster"
    /// toggle'i ile geri getirebilir; Unarchive ile aktif duruma alabilir.
    /// </summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Policy = StudioRoles.CanWrite)]
    public async Task<IActionResult> Archive([FromForm] string shipmentReference, CancellationToken cancellationToken)
    {
        var workspace = await _workspaceContext.GetOrCreateAsync(_customerService, cancellationToken);
        var shipment  = await _shipmentRepository.GetByReferenceAsync(shipmentReference, cancellationToken);
        if (shipment is null || !string.Equals(shipment.TenantKey, workspace.TenantKey, StringComparison.OrdinalIgnoreCase))
            return NotFound();

        shipment.Metadata[ShipmentArchiveIndex.ArchivedFlagKey] = "true";
        shipment.Metadata["shipment.archivedAtUtc"]            = DateTimeOffset.UtcNow.ToString("o");
        shipment.UpdatedAtUtc = DateTimeOffset.UtcNow;

        await _shipmentRepository.UpsertAsync(shipment, cancellationToken);
        await _unitOfWork.CommitAsync(cancellationToken);
        _archiveIndex.MarkArchived(workspace.TenantKey, shipment.ShipmentReference);

        TempData["StudioMessage"] = $"'{shipmentReference}' arsivlendi. 'Arsivlenenleri Goster' ile geri getirebilirsiniz.";
        return RedirectToAction(nameof(Index));
    }

    /// <summary>Arsivlenmis bir gonderiyi tekrar aktif liste icine alir.</summary>
    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Policy = StudioRoles.CanWrite)]
    public async Task<IActionResult> Unarchive([FromForm] string shipmentReference, CancellationToken cancellationToken)
    {
        var workspace = await _workspaceContext.GetOrCreateAsync(_customerService, cancellationToken);
        var shipment  = await _shipmentRepository.GetByReferenceAsync(shipmentReference, cancellationToken);
        if (shipment is null || !string.Equals(shipment.TenantKey, workspace.TenantKey, StringComparison.OrdinalIgnoreCase))
            return NotFound();

        shipment.Metadata.Remove(ShipmentArchiveIndex.ArchivedFlagKey);
        shipment.Metadata.Remove("shipment.archivedAtUtc");
        shipment.UpdatedAtUtc = DateTimeOffset.UtcNow;

        await _shipmentRepository.UpsertAsync(shipment, cancellationToken);
        await _unitOfWork.CommitAsync(cancellationToken);
        _archiveIndex.MarkUnarchived(workspace.TenantKey, shipment.ShipmentReference);

        TempData["StudioMessage"] = $"'{shipmentReference}' arsivden cikarildi.";
        return RedirectToAction(nameof(Index), new { showArchived = true });
    }

    // ── Bulk Import ───────────────────────────────────────────────────────────

    private static HashSet<CargoProviderTypeDto> GetAllowedProviders(CustomerProfileDto? profile)
    {
        return profile?.AllowedProviders.ToHashSet() ?? new HashSet<CargoProviderTypeDto>();
    }

    private async Task<IReadOnlyCollection<ShipmentListItemResponse>> ListVisibleShipmentsAsync(
        string workspaceCode,
        HashSet<CargoProviderTypeDto> allowedProviders,
        ShipmentFilterViewModel filter,
        CancellationToken cancellationToken)
    {
        var allShipments = await _orchestrator.ListShipmentsAsync(workspaceCode, cancellationToken);
        var visibleShipments = allShipments
            .Where(x => allowedProviders.Contains(x.Provider))
            .ToArray();

        return ApplyFilter(visibleShipments, filter);
    }

    private async Task<ShipmentDetailResponse?> GetVisibleShipmentAsync(
        string shipmentReference,
        string workspaceCode,
        HashSet<CargoProviderTypeDto> allowedProviders,
        CancellationToken cancellationToken)
    {
        var shipment = await _orchestrator.GetShipmentAsync(shipmentReference, workspaceCode, cancellationToken);
        if (shipment is null || !allowedProviders.Contains(shipment.Provider))
        {
            return null;
        }

        return shipment;
    }

    [HttpGet]
    public IActionResult Import() => View();

    [HttpGet]
    public IActionResult ImportTemplate()
    {
        var csv = new System.Text.StringBuilder();
        // SourceChannel kolonu opsiyonel — bos birakilirsa Manual (MN prefix) atanir.
        // Gecerli degerler: Trendyol, Hepsiburada, N11, Pazarama, CicekSepeti, PttAvm,
        //   Modanisa, Amazon, NopCommerce, Shopify, WooCommerce, Ticimax, IdeaSoft,
        //   EmbeddedShop, Manual, Custom
        csv.AppendLine("Provider;OrderReference;RecipientName;RecipientPhone;RecipientCity;RecipientDistrict;RecipientAddress;Weight;Desi;CollectionAmount;IdempotencyKey;SourceChannel");
        csv.AppendLine("Surat;ORD-001;Ali Veli;05551234567;Istanbul;Kadikoy;Moda Cad. No:1;1;1;;;Trendyol");
        csv.AppendLine("Sandbox;ORD-002;Ahmet Can;05557654321;Ankara;Cankaya;Ataturk Blv. No:10;2;3;150;;Manual");
        csv.AppendLine("Mng;ORD-003;Zeynep Demir;05552223344;Izmir;Konak;Cumhuriyet Blv. No:50;1;2;;;Shopify");

        var bytes = System.Text.Encoding.UTF8.GetPreamble()
            .Concat(System.Text.Encoding.UTF8.GetBytes(csv.ToString())).ToArray();
        return File(bytes, "text/csv", "kargoyeri-import-template.csv");
    }

    [HttpPost]
    [ValidateAntiForgeryToken]
    [Authorize(Policy = StudioRoles.CanWrite)]
    public async Task<IActionResult> Import(
        IFormFile csvFile,
        CancellationToken cancellationToken)
    {
        var workspace = await _workspaceContext.GetOrCreateAsync(_customerService, cancellationToken);

        if (csvFile is null || csvFile.Length == 0)
        {
            StudioFlash.Error(TempData, "Lutfen bir CSV dosyasi secin.");
            return View();
        }

        if (csvFile.Length > 5 * 1024 * 1024) // 5MB limit (async oldugu icin daha buyuk tolere edilebilir)
        {
            StudioFlash.Error(TempData, "Dosya boyutu 5MB'yi gecemez.");
            return View();
        }

        // Dosyayi oku, arka plan job olarak kuyruga at, hemen status sayfasina yonlendir
        using var reader0 = new System.IO.StreamReader(csvFile.OpenReadStream(), System.Text.Encoding.UTF8, detectEncodingFromByteOrderMarks: true);
        var content = await reader0.ReadToEndAsync(cancellationToken);

        var jobSvc = HttpContext.RequestServices.GetRequiredService<CsvImportJobService>();
        var job    = jobSvc.Enqueue(workspace.TenantKey, csvFile.FileName, content);

        StudioFlash.Success(TempData, $"Import arka planda baslatildi. Job ID: {job.Id}");
        return RedirectToAction(nameof(ImportStatus), new { jobId = job.Id });
    }

    [HttpGet]
    public IActionResult ImportStatus(Guid jobId)
    {
        var jobSvc = HttpContext.RequestServices.GetRequiredService<CsvImportJobService>();
        var job    = jobSvc.Get(jobId);
        if (job is null)
        {
            StudioFlash.Error(TempData, "Import isi bulunamadi (uygulama yeniden baslatilmis olabilir).");
            return RedirectToAction(nameof(Index));
        }
        return View("ImportStatus", job);
    }

    private async Task<ShipmentAnomalyReportViewModel> BuildAnomalyAsync(
        string workspaceCode,
        ShipmentCreateFormViewModel form,
        CancellationToken cancellationToken)
    {
        return await _anomalyDetectionService.AnalyzeAsync(workspaceCode, new ShipmentAnomalyInput
        {
            Provider = form.Provider.ToString(),
            RecipientCity = form.RecipientCity,
            CollectionAmount = form.CollectionAmount,
            Weight = form.Weight,
            Desi = form.Desi
        }, cancellationToken);
    }

    private static CreateShipmentRequest MapCreateRequest(string workspaceCode, ShipmentCreateFormViewModel form)
    {
        var pieceCount = Math.Max(1, Math.Min(form.PieceCount, 99));
        var perPieceWeight = Math.Round(form.Weight / pieceCount, 3);
        var perPieceDesi   = Math.Round(form.Desi   / pieceCount, 3);

        var metadata = new Dictionary<string, string>
        {
            ["studio.origin"] = "manual-create",
            ["studio.recipientPaysShipping"] = form.RecipientPaysShipping ? "true" : "false"
        };

        // Servis seviyesi (P0-#6) — provider-bagimsiz tier secimi.
        // Provider'in dogru metadata key'ine ceviri ServiceTypeCatalog uzerinden yapilir.
        // Provider-spesifik alanlar (UpsServiceCode vb.) asagida tier'i ezer.
        ServiceTypeCatalog.Apply(metadata, form.Provider, form.ServiceTier);

        // Fatura numarası (Sürat, MNG, Yurtiçi)
        if (!string.IsNullOrWhiteSpace(form.InvoiceSerial))
            metadata["invoice.serial"] = form.InvoiceSerial.Trim();
        if (!string.IsNullOrWhiteSpace(form.InvoiceSequence))
            metadata["invoice.sequence"] = form.InvoiceSequence.Trim();

        // Ödeme tipi — kapıda tahsilat yöntemi (Aras, MNG, Sürat)
        if (!string.IsNullOrWhiteSpace(form.PaymentType))
            metadata["payment.type"] = form.PaymentType.Trim();

        // HepsiJet
        if (!string.IsNullOrWhiteSpace(form.HepsiJetDeliveryType))
            metadata["hepsijet.deliveryType"] = form.HepsiJetDeliveryType.Trim();
        if (pieceCount > 1)
            metadata["hepsijet.parcelCount"] = pieceCount.ToString();

        // UPS servis kodu
        if (!string.IsNullOrWhiteSpace(form.UpsServiceCode) && form.UpsServiceCode != "11")
            metadata["ups.serviceCode"] = form.UpsServiceCode.Trim();

        // PTT gönderi tipi
        if (!string.IsNullOrWhiteSpace(form.PttGonderiTip) && form.PttGonderiTip != "NORMAL")
            metadata["ptt.gonderiTip"] = form.PttGonderiTip.Trim();

        // TrendyolExpress
        if (!string.IsNullOrWhiteSpace(form.TrendyolPackageId))
            metadata["trendyol.packageId"] = form.TrendyolPackageId.Trim();
        if (!string.IsNullOrWhiteSpace(form.TrendyolWarehouseId))
            metadata["trendyol.warehouseId"] = form.TrendyolWarehouseId.Trim();
        if (!string.IsNullOrWhiteSpace(form.TrendyolCargoCompanyId))
            metadata["trendyol.cargoCompanyId"] = form.TrendyolCargoCompanyId.Trim();

        // Source channel — operatör hangi kanaldan geldiğini seçer; seçim yoksa Manual default.
        // SourceChannelCode'u null bırakırız; ShipmentMapping.BuildTransientShipment kanal
        // enum'undan kanonik prefix kodu (mn / ty / et-shp ...) otomatik turetir.
        var effectiveSourceChannel = form.SourceChannel ?? OrderSourceChannelDto.Manual;

        return new CreateShipmentRequest
        {
            TenantKey = workspaceCode,
            Source = IntegrationSourceTypeDto.Manual,
            SourceChannel = effectiveSourceChannel,
            SourceChannelCode = string.IsNullOrWhiteSpace(form.SourceChannelCode)
                ? null  // null => mapping kanonik prefix'i koyar
                : form.SourceChannelCode!.Trim().ToLowerInvariant(),
            Provider = form.Provider,
            OrderReference = form.OrderReference?.Trim() ?? string.Empty,
            ClientShipmentReference = string.IsNullOrWhiteSpace(form.ClientShipmentReference) ? null : form.ClientShipmentReference.Trim(),
            IdempotencyKey = string.IsNullOrWhiteSpace(form.IdempotencyKey) ? null : form.IdempotencyKey.Trim(),
            CollectionAmount = form.CollectionAmount,
            CurrencyCode = string.IsNullOrWhiteSpace(form.CurrencyCode) ? "TRY" : form.CurrencyCode.Trim().ToUpperInvariant(),
            Sender = new AddressDto
            {
                Name = form.SenderName?.Trim() ?? string.Empty,
                CompanyName = string.IsNullOrWhiteSpace(form.SenderCompany) ? null : form.SenderCompany.Trim(),
                Phone = string.IsNullOrWhiteSpace(form.SenderPhone) ? null : form.SenderPhone.Trim(),
                Email = string.IsNullOrWhiteSpace(form.SenderEmail) ? null : form.SenderEmail.Trim(),
                City = form.SenderCity?.Trim() ?? string.Empty,
                District = string.IsNullOrWhiteSpace(form.SenderDistrict) ? null : form.SenderDistrict.Trim(),
                AddressLine1 = form.SenderAddress?.Trim() ?? string.Empty
            },
            Recipient = new AddressDto
            {
                Name = form.RecipientName?.Trim() ?? string.Empty,
                Phone = string.IsNullOrWhiteSpace(form.RecipientPhone) ? null : form.RecipientPhone.Trim(),
                Email = string.IsNullOrWhiteSpace(form.RecipientEmail) ? null : form.RecipientEmail.Trim(),
                City = form.RecipientCity?.Trim() ?? string.Empty,
                District = string.IsNullOrWhiteSpace(form.RecipientDistrict) ? null : form.RecipientDistrict.Trim(),
                AddressLine1 = form.RecipientAddress?.Trim() ?? string.Empty
            },
            Packages = Enumerable.Range(0, pieceCount).Select(i => new PackageDto
            {
                PackageSequence = i + 1,
                Weight = perPieceWeight,
                Desi   = perPieceDesi,
                Description = string.IsNullOrWhiteSpace(form.PackageDescription) ? null : form.PackageDescription.Trim(),
                CashOnDeliveryAmount = form.CollectionAmount
            }).ToList(),
            Metadata = metadata
        };
    }
}
