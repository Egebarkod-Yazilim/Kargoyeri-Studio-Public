using System.ComponentModel.DataAnnotations;
using Kargoyeri.Contracts.Dtos;
using Kargoyeri.Contracts.Enums;

namespace Kargoyeri.Studio.Core.Models;

public sealed class AuditExportPackageViewModel
{
    public string WorkspaceCode { get; set; } = string.Empty;
    public string WorkspaceName { get; set; } = string.Empty;
    public DateTimeOffset GeneratedAtUtc { get; set; }
    public int ShipmentCount { get; set; }
    public int NotificationCount { get; set; }
    public int AuditLogCount { get; set; }
    public int ActivityLogCount { get; set; }
}

public sealed class WorkflowRulePageViewModel
{
    public string WorkspaceCode { get; set; } = string.Empty;
    public string WorkspaceName { get; set; } = string.Empty;
    public WorkflowRuleEditorViewModel Editor { get; set; } = new();
    public IReadOnlyList<WorkflowRuleDefinition> Rules { get; set; } = Array.Empty<WorkflowRuleDefinition>();
}

public sealed class WorkflowRuleEditorViewModel
{
    [Required]
    [Display(Name = "Kural Adi")]
    public string Name { get; set; } = "Transit hatirlatmasi";

    [Required]
    [Display(Name = "Takip Edilen Durum")]
    public ShipmentStatusDto Status { get; set; } = ShipmentStatusDto.InTransit;

    [Range(1, 720)]
    [Display(Name = "Esik (Saat)")]
    public int ThresholdHours { get; set; } = 48;

    [Display(Name = "Bildirim Kanali")]
    public NotificationChannelDto Channel { get; set; } = NotificationChannelDto.Sms;

    [Display(Name = "Sablon Metni")]
    public string MessageTemplate { get; set; } =
        "Kargonuz halen {Status} durumunda. Takip no: {TrackingNumber}. Guncel durum icin paneli kontrol edebilirsiniz.";

    [Display(Name = "Tek Seferlik")]
    public bool OnlyOncePerShipment { get; set; } = true;

    [Display(Name = "Aktif")]
    public bool IsEnabled { get; set; } = true;
}

public sealed class WorkflowRuleDefinition
{
    public Guid Id { get; set; } = Guid.NewGuid();
    public string Name { get; set; } = string.Empty;
    public ShipmentStatusDto Status { get; set; } = ShipmentStatusDto.InTransit;
    public int ThresholdHours { get; set; } = 48;
    public NotificationChannelDto Channel { get; set; } = NotificationChannelDto.Sms;
    public string MessageTemplate { get; set; } = string.Empty;
    public bool OnlyOncePerShipment { get; set; } = true;
    public bool IsEnabled { get; set; } = true;
    public DateTimeOffset CreatedAtUtc { get; set; } = DateTimeOffset.UtcNow;
}

public sealed class PivotReportPageViewModel
{
    public string WorkspaceCode { get; set; } = string.Empty;
    public string WorkspaceName { get; set; } = string.Empty;
    public ReportFilterViewModel Filter { get; set; } = new();
    public string GroupBy { get; set; } = "provider";
    public string Metric { get; set; } = "count";
    public IReadOnlyList<PivotReportRowViewModel> Rows { get; set; } = Array.Empty<PivotReportRowViewModel>();
}

public sealed class PivotReportRowViewModel
{
    public string Key { get; set; } = string.Empty;
    public int ShipmentCount { get; set; }
    public int DeliveredCount { get; set; }
    public int InTransitCount { get; set; }
    public int FailedCount { get; set; }
    public int CancelledCount { get; set; }
    public decimal CodAmount { get; set; }
    public double SuccessRate { get; set; }
}

public sealed class DashboardPreferencePageViewModel
{
    public string WorkspaceCode { get; set; } = string.Empty;
    public string WorkspaceName { get; set; } = string.Empty;
    public IReadOnlyList<DashboardWidgetOptionViewModel> Widgets { get; set; } = Array.Empty<DashboardWidgetOptionViewModel>();
}

public sealed class DashboardWidgetOptionViewModel
{
    public string Key { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public bool IsSelected { get; set; }
}

public sealed class MobileOpsPageViewModel
{
    public string WorkspaceCode { get; set; } = string.Empty;
    public string WorkspaceName { get; set; } = string.Empty;
    public int ActiveShipmentCount { get; set; }
    public int DeliveredTodayCount { get; set; }
    public int AlertingShipmentCount { get; set; }
    public IReadOnlyCollection<ShipmentListItemResponse> RecentShipments { get; set; } = Array.Empty<ShipmentListItemResponse>();
    public IReadOnlyCollection<ProviderHealthSnapshotViewModel> ProviderHealth { get; set; } = Array.Empty<ProviderHealthSnapshotViewModel>();
}
