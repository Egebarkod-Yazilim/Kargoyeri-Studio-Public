using System.Globalization;
using System.Text;
using Kargoyeri.Application.Abstractions.Persistence;
using Kargoyeri.Application.Services;
using Kargoyeri.Contracts.Enums;
using Kargoyeri.Domain.Entities;
using Kargoyeri.Domain.Enums;
using Kargoyeri.Studio.Core.Infrastructure;
using Kargoyeri.Studio.Core.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Kargoyeri.Studio.Core.Controllers;

public sealed class ReportsController : Controller
{
    private readonly CargoOrchestrator _orchestrator;
    private readonly IShipmentRepository _shipmentRepository;
    private readonly CustomerService _customerService;
    private readonly IStudioWorkspaceContext _workspaceContext;
    private readonly DashboardPreferenceStore _dashboardPreferenceStore;

    public ReportsController(
        CargoOrchestrator orchestrator,
        IShipmentRepository shipmentRepository,
        CustomerService customerService,
        IStudioWorkspaceContext workspaceContext,
        DashboardPreferenceStore dashboardPreferenceStore)
    {
        _orchestrator = orchestrator;
        _shipmentRepository = shipmentRepository;
        _customerService = customerService;
        _workspaceContext = workspaceContext;
        _dashboardPreferenceStore = dashboardPreferenceStore;
    }

    [HttpGet]
    public async Task<IActionResult> Index([FromQuery] ReportFilterViewModel filter, CancellationToken cancellationToken = default)
    {
        var workspace = await _workspaceContext.GetOrCreateAsync(_customerService, cancellationToken);
        var (fromUtc, toUtc) = ParseDateRange(filter);
        var report = await _orchestrator.GetShipmentReportAsync(workspace.TenantKey, fromUtc, toUtc, cancellationToken);

        filter.From = fromUtc.ToString("yyyy-MM-dd");
        filter.To = toUtc.Date.ToString("yyyy-MM-dd");

        return View(new ReportPageViewModel
        {
            WorkspaceCode = workspace.TenantKey,
            WorkspaceName = workspace.Name,
            Filter = filter,
            Report = report
        });
    }

    [HttpGet]
    public async Task<IActionResult> ExportCsv([FromQuery] ReportFilterViewModel filter, CancellationToken cancellationToken = default)
    {
        var workspace = await _workspaceContext.GetOrCreateAsync(_customerService, cancellationToken);
        var (fromUtc, toUtc) = ParseDateRange(filter);
        var report = await _orchestrator.GetShipmentReportAsync(workspace.TenantKey, fromUtc, toUtc, cancellationToken);

        var sb = new StringBuilder();
        sb.AppendLine("Provider,Toplam,Teslim Edildi,Basarisiz/Iptal,Transitte,Beklemede,Basari Orani (%)");
        foreach (var row in report.ByProvider)
        {
            sb.AppendLine(string.Join(",",
                row.Provider,
                row.Total,
                row.Delivered,
                row.Failed,
                row.InTransit,
                row.Pending,
                row.SuccessRate.ToString("F1", CultureInfo.InvariantCulture)));
        }

        var fileName = $"rapor_{fromUtc:yyyyMMdd}_{toUtc:yyyyMMdd}.csv";
        var bytes = Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(sb.ToString())).ToArray();
        return File(bytes, "text/csv", fileName);
    }

    [HttpGet]
    public async Task<IActionResult> Pivot(
        [FromQuery] ReportFilterViewModel filter,
        [FromQuery] string groupBy = "provider",
        [FromQuery] string metric = "count",
        CancellationToken cancellationToken = default)
    {
        var workspace = await _workspaceContext.GetOrCreateAsync(_customerService, cancellationToken);
        var (fromUtc, toUtc) = ParseDateRange(filter);
        var shipments = await _shipmentRepository.ListByDateRangeAsync(workspace.TenantKey, fromUtc, toUtc, cancellationToken);

        filter.From = fromUtc.ToString("yyyy-MM-dd");
        filter.To = toUtc.Date.ToString("yyyy-MM-dd");

        return View(new PivotReportPageViewModel
        {
            WorkspaceCode = workspace.TenantKey,
            WorkspaceName = workspace.Name,
            Filter = filter,
            GroupBy = groupBy,
            Metric = metric,
            Rows = BuildPivotRows(shipments, groupBy)
        });
    }

    [Authorize(Roles = StudioRoles.Admin)]
    [HttpGet]
    public async Task<IActionResult> Widgets(CancellationToken cancellationToken = default)
    {
        var workspace = await _workspaceContext.GetOrCreateAsync(_customerService, cancellationToken);
        var selected = await _dashboardPreferenceStore.GetAsync(workspace.TenantKey, cancellationToken);
        return View(new DashboardPreferencePageViewModel
        {
            WorkspaceCode = workspace.TenantKey,
            WorkspaceName = workspace.Name,
            Widgets = BuildWidgetOptions(selected)
        });
    }

    [Authorize(Roles = StudioRoles.Admin)]
    [HttpPost]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> Widgets([FromForm] string[] widgets, CancellationToken cancellationToken = default)
    {
        var workspace = await _workspaceContext.GetOrCreateAsync(_customerService, cancellationToken);
        await _dashboardPreferenceStore.SaveAsync(workspace.TenantKey, widgets, cancellationToken);
        TempData["StudioMessage"] = "Dashboard widget tercihleri kaydedildi.";
        return RedirectToAction(nameof(Widgets));
    }

    [HttpGet]
    public async Task<IActionResult> Cod([FromQuery] ReportFilterViewModel filter, CancellationToken cancellationToken = default)
    {
        var workspace = await _workspaceContext.GetOrCreateAsync(_customerService, cancellationToken);
        var (fromUtc, toUtc) = ParseDateRange(filter);
        var data = await BuildCodReportAsync(workspace.TenantKey, fromUtc, toUtc, cancellationToken);

        filter.From = fromUtc.ToString("yyyy-MM-dd");
        filter.To = toUtc.Date.ToString("yyyy-MM-dd");

        return View(new CodReportPageViewModel
        {
            WorkspaceCode = workspace.TenantKey,
            WorkspaceName = workspace.Name,
            Filter = filter,
            Rows = data.rows,
            ProviderSummary = data.summary,
            Totals = data.totals
        });
    }

    [HttpGet]
    public async Task<IActionResult> CodExportCsv([FromQuery] ReportFilterViewModel filter, CancellationToken cancellationToken = default)
    {
        var workspace = await _workspaceContext.GetOrCreateAsync(_customerService, cancellationToken);
        var (fromUtc, toUtc) = ParseDateRange(filter);
        var data = await BuildCodReportAsync(workspace.TenantKey, fromUtc, toUtc, cancellationToken);

        var sb = new StringBuilder();
        sb.AppendLine("Tarih,Gonderi No,Siparis No,Provider,Durum,Takip No,Sehir,Tutar,Para,Odeme Tipi,Teslim");
        var inv = CultureInfo.InvariantCulture;
        foreach (var row in data.rows)
        {
            sb.AppendLine(string.Join(",",
                row.CreatedAtUtc.ToLocalTime().ToString("yyyy-MM-dd HH:mm"),
                Csv(row.ShipmentReference),
                Csv(row.OrderReference),
                Csv(row.Provider),
                Csv(row.Status),
                Csv(row.TrackingNumber ?? string.Empty),
                Csv(row.RecipientCity ?? string.Empty),
                row.Amount.ToString("F2", inv),
                Csv(row.Currency),
                Csv(row.PaymentType),
                row.IsDelivered ? "Evet" : "Hayir"));
        }

        sb.AppendLine();
        sb.AppendLine("PROVIDER OZET");
        sb.AppendLine("Provider,Adet,Teslim Edilen,Toplam Tutar,Tahsil Edilen,Bekleyen");
        foreach (var item in data.summary)
        {
            sb.AppendLine(string.Join(",",
                Csv(item.Provider),
                item.Count,
                item.DeliveredCount,
                item.TotalAmount.ToString("F2", inv),
                item.CollectedAmount.ToString("F2", inv),
                item.PendingAmount.ToString("F2", inv)));
        }

        sb.AppendLine();
        sb.AppendLine("GENEL TOPLAM");
        sb.AppendLine($"Adet,{data.totals.Count}");
        sb.AppendLine($"Teslim Edilen,{data.totals.DeliveredCount}");
        sb.AppendLine($"Toplam Tutar,{data.totals.TotalAmount.ToString("F2", inv)}");
        sb.AppendLine($"Tahsil Edilen,{data.totals.CollectedAmount.ToString("F2", inv)}");
        sb.AppendLine($"Bekleyen,{data.totals.PendingAmount.ToString("F2", inv)}");

        var fileName = $"cod_raporu_{fromUtc:yyyyMMdd}_{toUtc:yyyyMMdd}.csv";
        var bytes = Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(sb.ToString())).ToArray();
        return File(bytes, "text/csv", fileName);
    }

    private static string Csv(string value) =>
        value.Contains(',') || value.Contains('"') || value.Contains('\n')
            ? "\"" + value.Replace("\"", "\"\"") + "\""
            : value;

    private async Task<(IReadOnlyList<CodReportRowViewModel> rows,
                       IReadOnlyList<CodProviderSummaryViewModel> summary,
                       CodReportTotalsViewModel totals)>
        BuildCodReportAsync(string tenantKey, DateTime fromUtc, DateTime toUtc, CancellationToken ct)
    {
        var all = await _orchestrator.ListShipmentsAsync(tenantKey, ct);
        var inRange = all.Where(x => x.CreatedAtUtc >= fromUtc && x.CreatedAtUtc <= toUtc).ToArray();

        var sem = new SemaphoreSlim(8);
        var detailTasks = inRange.Select(async item =>
        {
            await sem.WaitAsync(ct);
            try
            {
                return await _orchestrator.GetShipmentAsync(item.ShipmentReference, tenantKey, ct);
            }
            finally
            {
                sem.Release();
            }
        });

        var details = (await Task.WhenAll(detailTasks))
            .Where(x => x is not null && x.CollectionAmount.HasValue && x.CollectionAmount.Value > 0)
            .Select(x => x!)
            .ToArray();

        var rows = details
            .OrderByDescending(x => x.CreatedAtUtc)
            .Select(x =>
            {
                var paymentType = x.Metadata.TryGetValue("payment.type", out var pt) ? pt : "cod_cash";
                return new CodReportRowViewModel
                {
                    CreatedAtUtc = x.CreatedAtUtc,
                    ShipmentReference = x.ShipmentReference,
                    OrderReference = x.OrderReference,
                    Provider = x.Provider.ToString(),
                    Status = x.Status.ToString(),
                    TrackingNumber = x.TrackingNumber,
                    RecipientCity = x.Recipient?.City,
                    Amount = x.CollectionAmount!.Value,
                    Currency = string.IsNullOrWhiteSpace(x.CurrencyCode) ? "TRY" : x.CurrencyCode,
                    PaymentType = paymentType,
                    IsDelivered = x.Status == ShipmentStatusDto.Delivered
                };
            })
            .ToArray();

        var summary = rows
            .GroupBy(x => x.Provider)
            .Select(g => new CodProviderSummaryViewModel
            {
                Provider = g.Key,
                Count = g.Count(),
                DeliveredCount = g.Count(x => x.IsDelivered),
                TotalAmount = g.Sum(x => x.Amount),
                CollectedAmount = g.Where(x => x.IsDelivered).Sum(x => x.Amount)
            })
            .OrderByDescending(x => x.TotalAmount)
            .ToArray();

        var totals = new CodReportTotalsViewModel
        {
            Count = rows.Length,
            DeliveredCount = rows.Count(x => x.IsDelivered),
            TotalAmount = rows.Sum(x => x.Amount),
            CollectedAmount = rows.Where(x => x.IsDelivered).Sum(x => x.Amount)
        };

        return (rows, summary, totals);
    }

    private static (DateTime FromUtc, DateTime ToUtc) ParseDateRange(ReportFilterViewModel filter)
    {
        var today = DateTime.UtcNow.Date;
        var from = !string.IsNullOrWhiteSpace(filter.From) &&
                   DateTime.TryParseExact(filter.From, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsedFrom)
            ? parsedFrom
            : today.AddDays(-6);

        var to = !string.IsNullOrWhiteSpace(filter.To) &&
                 DateTime.TryParseExact(filter.To, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsedTo)
            ? parsedTo
            : today;

        return (from, to.AddDays(1).AddTicks(-1));
    }

    private static IReadOnlyList<PivotReportRowViewModel> BuildPivotRows(IReadOnlyCollection<CargoShipment> shipments, string groupBy)
    {
        static string ResolveKey(CargoShipment shipment, string grouping) => grouping.ToLowerInvariant() switch
        {
            "status" => shipment.Status.ToString(),
            "city" => string.IsNullOrWhiteSpace(shipment.Recipient?.City) ? "-" : shipment.Recipient.City,
            "day" => shipment.CreatedAtUtc.ToLocalTime().ToString("dd.MM.yyyy"),
            _ => shipment.Provider.ToString()
        };

        return shipments
            .GroupBy(x => ResolveKey(x, groupBy))
            .Select(g => new PivotReportRowViewModel
            {
                Key = g.Key,
                ShipmentCount = g.Count(),
                DeliveredCount = g.Count(x => x.Status == ShipmentStatus.Delivered),
                InTransitCount = g.Count(x => x.Status == ShipmentStatus.InTransit),
                FailedCount = g.Count(x => x.Status == ShipmentStatus.Failed),
                CancelledCount = g.Count(x => x.Status == ShipmentStatus.Cancelled),
                CodAmount = g.Sum(x => x.CollectionAmount ?? 0),
                SuccessRate = g.Any() ? Math.Round(g.Count(x => x.Status == ShipmentStatus.Delivered) * 100d / g.Count(), 1) : 0
            })
            .OrderByDescending(x => x.ShipmentCount)
            .ToArray();
    }

    private static IReadOnlyList<DashboardWidgetOptionViewModel> BuildWidgetOptions(IReadOnlyList<string> selected)
    {
        var set = selected.ToHashSet(StringComparer.OrdinalIgnoreCase);
        return
        [
            new() { Key = "overview", Title = "Ust Metrikler", Description = "Toplam gonderi, bildirim ve worker ozeti.", IsSelected = set.Contains("overview") },
            new() { Key = "charts", Title = "Grafikler", Description = "Durum, provider ve trend chart bloklari.", IsSelected = set.Contains("charts") },
            new() { Key = "notifications", Title = "Bildirim Kanallari", Description = "Kanal bazli teslimat performansi.", IsSelected = set.Contains("notifications") },
            new() { Key = "checklist", Title = "Kurulum Checklist", Description = "Onboarding ilerleme listesi.", IsSelected = set.Contains("checklist") }
        ];
    }
}
