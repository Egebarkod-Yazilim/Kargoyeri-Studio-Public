using System.ComponentModel.DataAnnotations;
using Kargoyeri.Contracts.Dtos;
using Kargoyeri.Contracts.Enums;

namespace Kargoyeri.Studio.Core.Models;

public sealed class PhaseCardViewModel
{
    public string Phase { get; set; } = string.Empty;
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public IEnumerable<string> Highlights { get; set; } = Array.Empty<string>();
    public string Status { get; set; } = string.Empty;
    public string Outcome { get; set; } = string.Empty;
    public IEnumerable<string> Deliverables { get; set; } = Array.Empty<string>();
    public IEnumerable<string> NextSteps { get; set; } = Array.Empty<string>();
}

public sealed class DashboardViewModel
{
    public string WorkspaceCode { get; set; } = string.Empty;
    public string WorkspaceName { get; set; } = string.Empty;
    public int ProviderCount { get; set; }
    public int ReadyProviderCount { get; set; }
    public int ShipmentCount { get; set; }
    public int NotificationCount { get; set; }
    public string CurrentPhaseLabel { get; set; } = string.Empty;
    public int CompletedChecklistItems { get; set; }
    public int TotalChecklistItems { get; set; }
    public IReadOnlyCollection<ProviderIntegrationStatusDto> ProviderStatuses { get; set; } = Array.Empty<ProviderIntegrationStatusDto>();
    public IReadOnlyCollection<PhaseCardViewModel> PhaseCards { get; set; } = Array.Empty<PhaseCardViewModel>();
    public IReadOnlyCollection<ChecklistItemViewModel> Checklist { get; set; } = Array.Empty<ChecklistItemViewModel>();
    public IReadOnlyCollection<ShipmentListItemResponse> RecentShipments { get; set; } = Array.Empty<ShipmentListItemResponse>();
    public IReadOnlyCollection<NotificationMessageDto> RecentNotifications { get; set; } = Array.Empty<NotificationMessageDto>();
    public Kargoyeri.Studio.Core.Infrastructure.WorkerCycleStatus WorkerStatus { get; set; } = new();

    // ── Grafik verileri ────────────────────────────────────────────────────────
    /// <summary>Durum adı → gönderi sayısı (Donut chart)</summary>
    public Dictionary<string, int> ShipmentsByStatus { get; set; } = new();

    /// <summary>Provider adı → gönderi sayısı (Bar chart)</summary>
    public Dictionary<string, int> ShipmentsByProvider { get; set; } = new();

    /// <summary>Son 7 günün tarihleri (dd.MM) sıralı</summary>
    public List<string> TrendLabels { get; set; } = new();

    /// <summary>Son 7 günün günlük gönderi sayıları (TrendLabels ile eşleşir)</summary>
    public List<int> TrendCounts { get; set; } = new();

    /// <summary>Bildirim kanal → (toplam, başarılı, başarısız) tuple</summary>
    public Dictionary<string, (int Total, int Delivered, int Failed)> NotificationsByChannel { get; set; } = new();
    public IReadOnlyCollection<string> VisibleWidgets { get; set; } = Array.Empty<string>();
}

/// <summary>
/// P3-#2 — Musteri kullanicilara gosterilen sade, gorsel dashboard.
/// HomeController.Index non-admin icin bu modeli besler.
/// </summary>
public sealed class CustomerDashboardViewModel
{
    public string WorkspaceCode { get; set; } = string.Empty;
    public string WorkspaceName { get; set; } = string.Empty;
    public string? BrandDisplayName { get; set; }
    public string? PartnerLabel { get; set; }
    public string? AccentColor { get; set; }

    /// <summary>Bugun olusturulan gonderi sayisi (KPI #1)</summary>
    public int TodayCount { get; set; }
    /// <summary>Son 7 gun toplam (KPI #2)</summary>
    public int Last7DaysCount { get; set; }
    /// <summary>Yolda olan toplam (KPI #3)</summary>
    public int InTransitCount { get; set; }
    /// <summary>Bu ayki teslim edilenler (KPI #4)</summary>
    public int DeliveredThisMonth { get; set; }

    /// <summary>Son 7 gunun gunluk sayilari (mini bar chart).</summary>
    public List<int> TrendCounts { get; set; } = new();
    /// <summary>TrendCounts ile eslesen tarih etiketleri (dd.MM).</summary>
    public List<string> TrendLabels { get; set; } = new();
    /// <summary>Trend penceresinin pik degeri (yuzde hesabi icin).</summary>
    public int TrendPeak { get; set; }

    /// <summary>Status -> count (donut/list).</summary>
    public Dictionary<string, int> ShipmentsByStatus { get; set; } = new();
    /// <summary>Provider -> count (bar list).</summary>
    public Dictionary<string, int> ShipmentsByProvider { get; set; } = new();

    /// <summary>Son 6 gonderi (recent activity feed).</summary>
    public IReadOnlyCollection<ShipmentListItemResponse> RecentShipments { get; set; } = Array.Empty<ShipmentListItemResponse>();
}

public sealed class ChecklistItemViewModel
{
    public string Title { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public bool IsDone { get; set; }
    public string ActionText { get; set; } = string.Empty;
    public string ActionUrl { get; set; } = string.Empty;
}

public sealed class WorkspaceSetupInput
{
    [Required]
    [Display(Name = "Musteri Kodu")]
    public string CustomerCode { get; set; } = "studio-demo";

    [Required]
    [Display(Name = "Musteri Adi")]
    public string CustomerName { get; set; } = "Kargoyeri Demo";

    [Display(Name = "Webhook Bildirimi")]
    public string? NotificationWebhook { get; set; }

    [Display(Name = "Webhook Secret (HMAC imzalama)")]
    public string? WebhookSecret { get; set; }

    [Display(Name = "E-Posta Bildirimi")]
    public string? NotificationEmail { get; set; }

    [Display(Name = "Telefon Bildirimi")]
    public string? NotificationPhone { get; set; }

    [Display(Name = "Kaynak Sistem")]
    public string? SourceSystemName { get; set; }

    [Display(Name = "Kaynak API Adresi")]
    public string? SourceApiBaseUrl { get; set; }

    [Display(Name = "Kaynak API Key")]
    public string? SourceApiKey { get; set; }

    [Display(Name = "Kaynak Secret")]
    public string? SourceApiSecret { get; set; }

    [Display(Name = "Siparis Kanal Kodu")]
    public string? SourceChannelCode { get; set; }

    [Display(Name = "Marketplace Platformu")]
    public string? MarketplacePlatform { get; set; }

    [Display(Name = "Marketplace API Adresi")]
    public string? MarketplaceApiBaseUrl { get; set; }

    [Display(Name = "Marketplace API Key")]
    public string? MarketplaceApiKey { get; set; }

    [Display(Name = "Marketplace API Secret")]
    public string? MarketplaceApiSecret { get; set; }

    [Display(Name = "Store / Seller Id")]
    public string? MarketplaceStoreId { get; set; }

    [Display(Name = "Marketplace Kanal Kodu")]
    public string? MarketplaceChannelCode { get; set; }

    [Display(Name = "Auto Sync Acik")]
    public bool MarketplaceAutoSyncEnabled { get; set; }

    [Range(5, 720)]
    [Display(Name = "Sync Araligi (dk)")]
    public int MarketplaceAutoSyncIntervalMinutes { get; set; } = 30;

    [Display(Name = "Varsayilan Provider")]
    public string? MarketplaceDefaultProvider { get; set; }

    [Display(Name = "Gonderici Adi")]
    public string? MarketplaceSenderName { get; set; }

    [Display(Name = "Gonderici Telefon")]
    public string? MarketplaceSenderPhone { get; set; }

    [Display(Name = "Gonderici Il")]
    public string? MarketplaceSenderCity { get; set; }

    [Display(Name = "Gonderici Ilce")]
    public string? MarketplaceSenderDistrict { get; set; }

    [Display(Name = "Gonderici Adres")]
    public string? MarketplaceSenderAddress { get; set; }

    [Display(Name = "Adres Dogrulama Acik")]
    public bool AddressValidationEnabled { get; set; }

    [Display(Name = "Strict Mod")]
    public bool AddressValidationStrictMode { get; set; } = true;

    [Display(Name = "Adres Dogrulama API")]
    public string? AddressValidationApiBaseUrl { get; set; }

    [Display(Name = "Adres Dogrulama Key")]
    public string? AddressValidationApiKey { get; set; }

    [Display(Name = "Adres Dogrulama Secret")]
    public string? AddressValidationApiSecret { get; set; }

    [Display(Name = "Marka Adi")]
    public string? BrandDisplayName { get; set; }

    [Display(Name = "Logo URL")]
    public string? BrandLogoUrl { get; set; }

    [Display(Name = "Vurgu Rengi")]
    public string? BrandAccentColor { get; set; }

    [Display(Name = "Partner Etiketi")]
    public string? BrandPartnerLabel { get; set; }

    [Display(Name = "Alt Yazi")]
    public string? BrandFooterText { get; set; }

    [Display(Name = "Powered By Gizle")]
    public bool BrandHidePoweredBy { get; set; }

    // ── P5-#11/#12 — Multi-channel marketplace + multi-provider cargo selections ─
    // Form binding: Marketplaces[trendyol].Enabled=true, Marketplaces[trendyol].Fields[apiKey]=...
    // Bos veya Enabled=false olanlar pas geçilir.
    public Dictionary<string, MarketplaceSelectionInput> Marketplaces { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    // Form binding: Providers[surat].Enabled=true, Providers[surat].ClientCode=...
    public Dictionary<string, ProviderSelectionInput> Providers { get; set; } = new(StringComparer.OrdinalIgnoreCase);
}

/// <summary>Çoklu pazaryeri girişi — kanal başına aktif/credential set.</summary>
public sealed class MarketplaceSelectionInput
{
    public bool Enabled { get; set; }
    public string? DefaultProvider { get; set; }
    /// <summary>Descriptor'dan gelen field key'lerine göre kullanıcının girdiği değerler.</summary>
    public Dictionary<string, string> Fields { get; set; } = new(StringComparer.OrdinalIgnoreCase);
}

/// <summary>Çoklu kargo firması girişi — provider başına aktif/credential set.</summary>
public sealed class ProviderSelectionInput
{
    public bool Enabled { get; set; }
    public string? ClientCode { get; set; }
    public string? Username { get; set; }
    public string? Password { get; set; }
    public string? ApiKey { get; set; }
    public string? EndpointBase { get; set; }
}

public sealed class WorkspaceViewModel
{
    public string CurrentWorkspaceCode { get; set; } = string.Empty;
    public string CurrentWorkspaceName { get; set; } = string.Empty;
    public WorkspaceSetupInput Input { get; set; } = new();
    public CustomerProfileDto? Profile { get; set; }
    public IReadOnlyCollection<ChecklistItemViewModel> SetupChecklist { get; set; } = Array.Empty<ChecklistItemViewModel>();
    public bool SourceConfigured { get; set; }

    // Bildirim altyapısı uyarıları
    public bool IsSmtpConfigured { get; set; }
    public bool IsNetgsmConfigured { get; set; }

    // Provider yetkilendirme
    public IReadOnlyCollection<ProviderPermissionViewModel> AllProviders { get; set; } = Array.Empty<ProviderPermissionViewModel>();
    public IReadOnlyCollection<MarketplaceSyncRunViewModel> MarketplaceRuns { get; set; } = Array.Empty<MarketplaceSyncRunViewModel>();
}

public sealed class ProviderPermissionViewModel
{
    public CargoProviderTypeDto Provider { get; set; }
    public string DisplayName { get; set; } = string.Empty;
    public bool IsAllowed { get; set; }
}

public sealed class ProviderCardViewModel
{
    public ProviderProfileDto Profile { get; set; } = new();
    public ProviderIntegrationStatusDto Status { get; set; } = new();
    public ProviderVisualViewModel Visual { get; set; } = new();
    public ProviderHealthSnapshotViewModel Health { get; set; } = new();
}

public sealed class ProviderIndexViewModel
{
    public string WorkspaceCode { get; set; } = string.Empty;
    public string WorkspaceName { get; set; } = string.Empty;
    public IReadOnlyCollection<ProviderCardViewModel> Providers { get; set; } = Array.Empty<ProviderCardViewModel>();
    public IReadOnlyCollection<ProviderHealthSnapshotViewModel> HealthSnapshots { get; set; } = Array.Empty<ProviderHealthSnapshotViewModel>();
}

public sealed class ProviderHealthSnapshotViewModel
{
    public CargoProviderTypeDto Provider { get; set; }
    public string ProviderName { get; set; } = string.Empty;
    public int ProbeCount { get; set; }
    public int SuccessCount { get; set; }
    public int FailureCount { get; set; }
    public double SuccessRate { get; set; }
    public double ErrorRate { get; set; }
    public int? P95LatencyMs { get; set; }
    public DateTimeOffset? LastCheckedAtUtc { get; set; }
    public string? LastError { get; set; }
    public bool HasData => ProbeCount > 0;
}

public sealed class ProviderSettingsEditorViewModel
{
    public string WorkspaceCode { get; set; } = string.Empty;
    public string WorkspaceName { get; set; } = string.Empty;
    public string ProviderCode { get; set; } = string.Empty;
    public string ProviderName { get; set; } = string.Empty;
    public ProviderProfileDto Profile { get; set; } = new();
    public ProviderIntegrationStatusDto Status { get; set; } = new();
    public ProviderVisualViewModel Visual { get; set; } = new();

    [Display(Name = "Aktif")]
    public bool IsEnabled { get; set; } = true;

    public string? ClientCode { get; set; }
    public string? Username { get; set; }
    public string? Password { get; set; }
    public string? ApiKey { get; set; }
    public string? EndpointBase { get; set; }
    public string? AdditionalSettingsText { get; set; }

    /// <summary>GET sirasinda formu doldururken gercek degerleri HTML'e yazmamak icin flag.</summary>
    public bool HasStoredPassword { get; set; }
    public bool HasStoredApiKey { get; set; }
}

public readonly record struct ProviderOptionViewModel(string Code, string DisplayName);

public sealed class ShipmentCreateFormViewModel
{
    [Required]
    public CargoProviderTypeDto Provider { get; set; } = CargoProviderTypeDto.Sandbox;

    [Required]
    public string OrderReference { get; set; } = string.Empty;

    public string? ClientShipmentReference { get; set; }
    public string? IdempotencyKey { get; set; }
    public string CurrencyCode { get; set; } = "TRY";
    public decimal? CollectionAmount { get; set; }
    public bool RecipientPaysShipping { get; set; }
    public string SenderName { get; set; } = "Kargoyeri";
    public string SenderCompany { get; set; } = "Kargoyeri";
    public string SenderPhone { get; set; } = "05550000000";
    public string SenderEmail { get; set; } = "studio@kargoyeri.local";
    public string SenderCity { get; set; } = "Istanbul";
    public string SenderDistrict { get; set; } = "Umraniye";
    public string SenderAddress { get; set; } = "Inkilap Mah. No:10";

    [Required]
    public string RecipientName { get; set; } = string.Empty;

    [Required]
    public string RecipientPhone { get; set; } = string.Empty;

    public string? RecipientEmail { get; set; }

    /// <summary>Sipariş kaynağı — operatör manuel oluştururken seçer. null = Manual default.</summary>
    [Display(Name = "Sipariş Kaynağı")]
    public OrderSourceChannelDto? SourceChannel { get; set; }

    /// <summary>Enum dışı kanal kodu (Shopify-acme, Custom-API vs.). SourceChannel dolduysa görmezden gelinir.</summary>
    public string? SourceChannelCode { get; set; }

    [Required]
    public string RecipientCity { get; set; } = string.Empty;

    [Required]
    public string RecipientDistrict { get; set; } = string.Empty;

    [Required]
    public string RecipientAddress { get; set; } = string.Empty;

    public decimal Weight { get; set; } = 1;
    public decimal Desi { get; set; } = 1;
    public string PackageDescription { get; set; } = "Tek Paket";

    // ── Ortak: Parça sayısı (Sürat, MNG, Aras, PTT) ─────────────────────────
    [Range(1, 99)]
    public int PieceCount { get; set; } = 1;

    // ── Fatura (Sürat, MNG, Yurtiçi) ─────────────────────────────────────────
    public string? InvoiceSerial { get; set; }
    public string? InvoiceSequence { get; set; }

    // ── Ödeme tipi — kapıda tahsilat yöntemi (Aras, MNG, Sürat) ─────────────
    // "cod_cash" = Kapıda nakit | "cod_card" = Kapıda kredi kartı | "" = Ön ödemeli
    public string PaymentType { get; set; } = string.Empty;

    // ── Servis Seviyesi (Provider-bagimsiz) ─────────────────────────────────
    // Tier secilirse ServiceTypeCatalog provider'in dogru metadata key/value'sine cevirir.
    // "Standard" varsayilan; provider-spesifik alan (UpsServiceCode, HepsiJetDeliveryType vb.)
    // hala destekleniyor; tier eslesirse onun degeri kullanilir.
    public Kargoyeri.Studio.Core.Infrastructure.KargoyeriServiceTier ServiceTier { get; set; }
        = Kargoyeri.Studio.Core.Infrastructure.KargoyeriServiceTier.Standard;

    // ── HepsiJet ──────────────────────────────────────────────────────────────
    // STANDARD | SAME_DAY | NEXT_DAY
    public string HepsiJetDeliveryType { get; set; } = "STANDARD";

    // ── UPS ───────────────────────────────────────────────────────────────────
    // 11=Standard | 65=Express Saver | 07=Express | 08=Expedited
    public string UpsServiceCode { get; set; } = "11";

    // ── PTT ───────────────────────────────────────────────────────────────────
    // NORMAL | ACELE | GECE
    public string PttGonderiTip { get; set; } = "NORMAL";

    // ── TrendyolExpress ───────────────────────────────────────────────────────
    public string? TrendyolPackageId { get; set; }
    public string? TrendyolWarehouseId { get; set; }
    public string? TrendyolCargoCompanyId { get; set; }
}

public sealed class ShipmentProviderChoiceViewModel
{
    public CargoProviderTypeDto Provider { get; set; }
    public string ProviderCode { get; set; } = string.Empty;
    public string DisplayName { get; set; } = string.Empty;
    public bool IsReady { get; set; }
    public bool LiveTransportImplemented { get; set; }
    public bool SimulationEnabled { get; set; }
}

public sealed class PaginationViewModel
{
    public int Page { get; set; } = 1;
    public int PageSize { get; set; } = 20;
    public int TotalCount { get; set; }
    public int TotalPages => TotalCount == 0 ? 1 : (int)Math.Ceiling((double)TotalCount / PageSize);
    public bool HasPrev => Page > 1;
    public bool HasNext => Page < TotalPages;
}

public sealed class ShipmentFilterViewModel
{
    [Display(Name = "Arama")]
    public string? Search { get; set; }

    [Display(Name = "Durum")]
    public string? Status { get; set; }

    [Display(Name = "Kargo Firmasi")]
    public string? Provider { get; set; }

    /// <summary>Sipariş kaynağı filtresi — OrderSourceChannelDto adı (ör. "Trendyol") veya string kod.</summary>
    [Display(Name = "Sipariş Kaynağı")]
    public string? Source { get; set; }

    /// <summary>P2-#6 — true ise arsivlenmis (soft-delete) gonderiler de gosterilir.</summary>
    [Display(Name = "Arsivlenenleri Goster")]
    public bool ShowArchived { get; set; }
}

public sealed class ShipmentWorkspaceViewModel
{
    public string WorkspaceCode { get; set; } = string.Empty;
    public string WorkspaceName { get; set; } = string.Empty;
    public IReadOnlyCollection<ShipmentListItemResponse> Shipments { get; set; } = Array.Empty<ShipmentListItemResponse>();
    public bool SourceConfigured { get; set; }
    public string SourceSystemName { get; set; } = string.Empty;
    public string? SourceApiBaseUrl { get; set; }
    public int ReadyProviderCount { get; set; }
    public ShipmentCreateFormViewModel CreateForm { get; set; } = new();
    public IReadOnlyCollection<ShipmentProviderChoiceViewModel> ProviderOptions { get; set; } = Array.Empty<ShipmentProviderChoiceViewModel>();
    public ProviderRequestPreviewResponse? Preview { get; set; }
    public bool HasReadyProvider => ProviderOptions.Any(x => x.IsReady);
    public PaginationViewModel Pagination { get; set; } = new();
    public ShipmentFilterViewModel Filter { get; set; } = new();
    public int TotalVisibleShipments { get; set; }

    /// <summary>Yönetici mi yoksa müşteri mi? View bu değere göre ek bölümler gösterir.</summary>
    public bool IsAdmin { get; set; }
    public AddressValidationSummaryViewModel? AddressValidation { get; set; }
    public ShipmentAnomalyReportViewModel? AnomalyReport { get; set; }

    /// <summary>Müşterinin inline olarak kargo firması seçebileceği hazır firmalar.</summary>
    public IReadOnlyCollection<ShipmentProviderChoiceViewModel> ReadyProviders =>
        ProviderOptions.Where(x => x.IsReady).ToArray();

    /// <summary>P2-#6 — bu sayfadaki arsivlenmis gonderi referans seti.</summary>
    public HashSet<string> ArchivedReferences { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>P2-#6 — workspace genelinde arsivlenmis kayit sayisi (UI badge).</summary>
    public int TotalArchived { get; set; }

    /// <summary>
    /// Sipariş kaynağı chip-filtre sayımları. Key = OrderSourceChannelDto adı veya "_none";
    /// Value = bu kaynaktan gelen toplam (filtresiz) sipariş sayısı. View chip render eder.
    /// </summary>
    public Dictionary<string, int> SourceCounts { get; set; } = new(StringComparer.OrdinalIgnoreCase);

    /// <summary>Şu anda seçili olan kaynak filtresi. null = tümü.</summary>
    public string? SelectedSource { get; set; }
}

public sealed class ShipmentDetailPageViewModel
{
    public string WorkspaceCode { get; set; } = string.Empty;
    public string WorkspaceName { get; set; } = string.Empty;
    public ShipmentDetailResponse Shipment { get; set; } = new();
    public IReadOnlyCollection<ShipmentOperationLogDto> Logs { get; set; } = Array.Empty<ShipmentOperationLogDto>();
    public IReadOnlyCollection<NotificationMessageDto> Notifications { get; set; } = Array.Empty<NotificationMessageDto>();
    public ShipmentActionFormViewModel ActionForm { get; set; } = new();
    public ShipmentAnomalyReportViewModel? AnomalyReport { get; set; }
}

public sealed class NotificationCenterViewModel
{
    public string WorkspaceCode { get; set; } = string.Empty;
    public string WorkspaceName { get; set; } = string.Empty;
    public NotificationFilterViewModel Filter { get; set; } = new();
    public IReadOnlyCollection<NotificationMessageDto> Notifications { get; set; } = Array.Empty<NotificationMessageDto>();
    public PaginationViewModel Pagination { get; set; } = new();
}

public sealed class NotificationTemplatePageViewModel
{
    public string WorkspaceCode { get; set; } = string.Empty;
    public string WorkspaceName { get; set; } = string.Empty;
    public List<NotificationTemplateEditorItemViewModel> Templates { get; set; } = new();
}

public sealed class NotificationTemplateEditorItemViewModel
{
    public Kargoyeri.Contracts.Enums.NotificationEventTypeDto EventType { get; set; }
    public Kargoyeri.Contracts.Enums.NotificationChannelDto Channel { get; set; }
    public bool IsEnabled { get; set; } = true;
    public string SubjectTemplate { get; set; } = string.Empty;
    public string BodyTemplate { get; set; } = string.Empty;
}

public sealed class ProviderPingResultViewModel
{
    public string ProviderCode { get; set; } = string.Empty;
    public string ProviderName { get; set; } = string.Empty;
    public bool Success { get; set; }
    public string Message { get; set; } = string.Empty;
    public int? ResponseMs { get; set; }
    public string? EndpointTested { get; set; }
}

public sealed class ShipmentActionFormViewModel
{
    public string ShipmentReference { get; set; } = string.Empty;

    [Display(Name = "Iptal Nedeni")]
    [StringLength(500, MinimumLength = 5, ErrorMessage = "Iptal nedeni en az 5, en fazla 500 karakter olmalidir.")]
    public string? CancelReason { get; set; }
}

public sealed class NotificationFilterViewModel
{
    [Display(Name = "Gonderi Referansi")]
    public string? ShipmentReference { get; set; }

    [Display(Name = "Durum")]
    public NotificationDeliveryStatusDto? Status { get; set; }

    [Display(Name = "Kanal")]
    public NotificationChannelDto? Channel { get; set; }
}

public sealed class ProviderVisualViewModel
{
    public string ProviderCode { get; set; } = string.Empty;
    public string ProviderName { get; set; } = string.Empty;
    public string AccentClass { get; set; } = string.Empty;
    public string Headline { get; set; } = string.Empty;
    public string Caption { get; set; } = string.Empty;
    public string? ImageUrl { get; set; }
    public string SourceLabel { get; set; } = string.Empty;
    public string? SourceUrl { get; set; }
}

public sealed class RoadmapViewModel
{
    public IReadOnlyCollection<string> HostModes { get; set; } = Array.Empty<string>();
    public IReadOnlyCollection<string> ArchitectureLayers { get; set; } = Array.Empty<string>();
    public IReadOnlyCollection<PhaseCardViewModel> Phases { get; set; } = Array.Empty<PhaseCardViewModel>();
    public string CurrentFocus { get; set; } = string.Empty;
    public IReadOnlyCollection<ChecklistItemViewModel> DeliveryChecklist { get; set; } = Array.Empty<ChecklistItemViewModel>();
    public IReadOnlyCollection<ProviderVisualViewModel> ProviderVisuals { get; set; } = Array.Empty<ProviderVisualViewModel>();
}

public sealed class AuditLogViewModel
{
    public string WorkspaceCode { get; set; } = string.Empty;
    public string WorkspaceName { get; set; } = string.Empty;
    public string? ShipmentRef { get; set; }
    public IReadOnlyCollection<ShipmentOperationLogDto> Logs { get; set; } = Array.Empty<ShipmentOperationLogDto>();
    public IReadOnlyCollection<ShipmentListItemResponse> RecentShipments { get; set; } = Array.Empty<ShipmentListItemResponse>();
    public PaginationViewModel Pagination { get; set; } = new();
}

public sealed class AccessLoginViewModel
{
    [Required]
    [Display(Name = "Erisim Kodu")]
    public string AccessCode { get; set; } = string.Empty;
    public string? ErrorMessage { get; set; }

    /// <summary>Admin sekmesinde hata mesaji (UI'da ayri gosterilebilir).</summary>
    public string? AdminErrorMessage { get; set; }

    /// <summary>Hangi sekme aktif: "code" (varsayilan) veya "admin".</summary>
    public string ActiveTab { get; set; } = "code";
}

public sealed class PickupIndexViewModel
{
    public string TenantKey { get; set; } = string.Empty;
    public string TenantName { get; set; } = string.Empty;
    public IReadOnlyList<Kargoyeri.Studio.Core.Infrastructure.PickupRequest> Items { get; set; }
        = Array.Empty<Kargoyeri.Studio.Core.Infrastructure.PickupRequest>();
    public IReadOnlyList<(string TenantKey, string Name)> Tenants { get; set; }
        = Array.Empty<(string, string)>();
    public bool IsAdmin { get; set; }
    public string? StatusFilter { get; set; }
}

public sealed class PickupCreateViewModel
{
    public string TenantKey { get; set; } = string.Empty;
    public string TenantName { get; set; } = string.Empty;

    [Required, Display(Name = "Kargo Firmasi")]
    public string ProviderType { get; set; } = string.Empty;

    [Required, Display(Name = "Alim Tarihi")]
    [DataType(DataType.Date)]
    public DateTime PickupDate { get; set; } = DateTime.Today.AddDays(1);

    [Required, Display(Name = "Saat Araligi")]
    public string TimeWindow { get; set; } = "09:00-18:00";

    [Required, Display(Name = "Iletisim Kisisi")]
    public string ContactName { get; set; } = string.Empty;

    [Required, Phone, Display(Name = "Telefon")]
    public string ContactPhone { get; set; } = string.Empty;

    [Required, Display(Name = "Adres")]
    public string AddressLine { get; set; } = string.Empty;

    [Required, Display(Name = "Il")]
    public string City { get; set; } = string.Empty;

    [Required, Display(Name = "Ilce")]
    public string District { get; set; } = string.Empty;

    [Range(1, 9999), Display(Name = "Koli Sayisi")]
    public int ParcelCount { get; set; } = 1;

    [Display(Name = "Toplam Agirlik (kg)")]
    public decimal? TotalWeightKg { get; set; }

    [Display(Name = "Notlar")]
    public string? Notes { get; set; }

    public IReadOnlyList<string> AvailableProviders { get; set; } = Array.Empty<string>();
}

public sealed class AccessLoginAdminViewModel
{
    [Required, EmailAddress, Display(Name = "E-posta")]
    public string Email { get; set; } = string.Empty;

    [Required, DataType(DataType.Password), Display(Name = "Sifre")]
    public string Password { get; set; } = string.Empty;

    public string? ErrorMessage { get; set; }
}

public sealed class AccessLoginUserViewModel
{
    public string TenantKey  { get; set; } = string.Empty;
    public string TenantName { get; set; } = string.Empty;

    [Required, Display(Name = "Kullanici Adi")]
    public string Username { get; set; } = string.Empty;

    [Required, DataType(DataType.Password), Display(Name = "Sifre")]
    public string Password { get; set; } = string.Empty;

    public string? ErrorMessage { get; set; }
}

public sealed class AccessLoginTotpViewModel
{
    public string TenantKey  { get; set; } = string.Empty;
    public string TenantName { get; set; } = string.Empty;
    public string Username   { get; set; } = string.Empty;
    public string? DisplayName { get; set; }
    public bool IsAdmin { get; set; }

    [Required, Display(Name = "Dogrulama Kodu (6 hane)")]
    [RegularExpression("^[0-9]{6}$", ErrorMessage = "6 haneli rakam girin.")]
    public string Code { get; set; } = string.Empty;

    public string? ErrorMessage { get; set; }
}

public sealed class TotpEnrollmentViewModel
{
    public string TenantKey       { get; set; } = string.Empty;
    public string Username        { get; set; } = string.Empty;
    public bool   AlreadyEnabled  { get; set; }
    public string? Secret         { get; set; }
    public string? ProvisioningUri{ get; set; }

    [Display(Name = "Authenticator Kodu")]
    [RegularExpression("^[0-9]{6}$", ErrorMessage = "6 haneli rakam girin.")]
    public string Code { get; set; } = string.Empty;

    public string? Message { get; set; }
    public string? ErrorMessage { get; set; }
}

public sealed class AccessResetPasswordViewModel
{
    [Required, Display(Name = "Musteri Kodu")]
    public string TenantKey { get; set; } = string.Empty;

    [Required, Display(Name = "Kullanici Adi")]
    public string Username { get; set; } = string.Empty;

    [Required, Display(Name = "Sifirlama Token")]
    public string Token { get; set; } = string.Empty;

    [Required, DataType(DataType.Password), Display(Name = "Yeni Sifre")]
    [MinLength(6)]
    public string NewPassword { get; set; } = string.Empty;

    [Required, DataType(DataType.Password), Display(Name = "Yeni Sifre (Tekrar)")]
    [Compare(nameof(NewPassword))]
    public string ConfirmPassword { get; set; } = string.Empty;

    public string? ErrorMessage { get; set; }
}

public sealed class TenantSwitchViewModel
{
    public string CurrentTenantKey { get; set; } = string.Empty;
    public string? Search { get; set; }
    public IReadOnlyCollection<TenantListItemViewModel> Tenants { get; set; } = Array.Empty<TenantListItemViewModel>();
    public NewCustomerInput NewCustomer { get; set; } = new();
}

public sealed class TenantListItemViewModel
{
    public string TenantKey { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public bool IsActive { get; set; }
    public bool IsCurrent { get; set; }
    public int ProviderCount { get; set; }
    public string? WebhookAddress { get; set; }
    public string? EmailAddress { get; set; }
    public DateTimeOffset? UpdatedAtUtc { get; set; }
    public Kargoyeri.Studio.Core.Infrastructure.LicenseInfo License { get; set; } = Kargoyeri.Studio.Core.Infrastructure.LicenseInfo.Empty;
    public int UserCount { get; set; }
    public IReadOnlyCollection<Kargoyeri.Contracts.Enums.CargoProviderTypeDto> AllowedProviders { get; set; }
        = Array.Empty<Kargoyeri.Contracts.Enums.CargoProviderTypeDto>();
}

public sealed class LicenseInput
{
    [Required]
    public string TenantKey { get; set; } = string.Empty;

    /// <summary>Boş bırakılırsa otomatik üretilir.</summary>
    public string? LicenseCode { get; set; }

    [Required]
    public string ExpiresAt { get; set; } = string.Empty;

    [Required]
    public string Plan { get; set; } = "custom";
}

public sealed class ActivityLogViewModel
{
    public string? TenantKey { get; set; }
    public string? Username { get; set; }
    public string? Controller { get; set; }
    public string? HttpMethod { get; set; }
    public DateTime? FromDate { get; set; }
    public DateTime? ToDate { get; set; }
    public bool ErrorsOnly { get; set; }
    public IReadOnlyList<Kargoyeri.Studio.Core.Infrastructure.StudioActivityLogEntry> Entries { get; set; }
        = Array.Empty<Kargoyeri.Studio.Core.Infrastructure.StudioActivityLogEntry>();
    public IReadOnlyList<string> AvailableTenants { get; set; } = Array.Empty<string>();
    public IReadOnlyList<string> AvailableControllers { get; set; } = Array.Empty<string>();
    public PaginationViewModel Pagination { get; set; } = new();
}

public sealed class NewCustomerInput
{
    [Required, Display(Name = "Musteri Kodu")]
    public string TenantKey { get; set; } = string.Empty;

    [Required, Display(Name = "Musteri Adi")]
    public string Name { get; set; } = string.Empty;

    [Display(Name = "Webhook URL")]
    public string? WebhookUrl { get; set; }

    [Display(Name = "E-posta")]
    public string? Email { get; set; }

    [Display(Name = "Telefon")]
    public string? Phone { get; set; }

    [Display(Name = "VKN / TCKN")]
    public string? Vkn { get; set; }

    /// <summary>Pazaryeri kanal kodları (örn: "trendyol", "hepsiburada").</summary>
    [Display(Name = "Pazaryerleri")]
    public List<string> SelectedChannels { get; set; } = new();

    /// <summary>NopCommerce, embedded shop gibi diğer kaynak kodları.</summary>
    [Display(Name = "Diğer kaynaklar")]
    public List<string> SelectedExtraSources { get; set; } = new();
}

public sealed class ReportFilterViewModel
{
    [Display(Name = "Baslangic Tarihi")]
    public string? From { get; set; }

    [Display(Name = "Bitis Tarihi")]
    public string? To { get; set; }
}

public sealed class ReportPageViewModel
{
    public string WorkspaceCode { get; set; } = string.Empty;
    public string WorkspaceName { get; set; } = string.Empty;
    public ReportFilterViewModel Filter { get; set; } = new();
    public Kargoyeri.Contracts.Dtos.ShipmentReportData? Report { get; set; }
}

// ── P0-#7: Kapida Odeme (COD) Mutabakat Raporu ─────────────────────────────
public sealed class CodReportPageViewModel
{
    public string WorkspaceCode { get; set; } = string.Empty;
    public string WorkspaceName { get; set; } = string.Empty;
    public ReportFilterViewModel Filter { get; set; } = new();
    public IReadOnlyList<CodReportRowViewModel> Rows { get; set; } = Array.Empty<CodReportRowViewModel>();
    public IReadOnlyList<CodProviderSummaryViewModel> ProviderSummary { get; set; } = Array.Empty<CodProviderSummaryViewModel>();
    public CodReportTotalsViewModel Totals { get; set; } = new();
}

public sealed class CodReportRowViewModel
{
    public DateTimeOffset CreatedAtUtc { get; set; }
    public string ShipmentReference { get; set; } = string.Empty;
    public string OrderReference { get; set; } = string.Empty;
    public string Provider { get; set; } = string.Empty;
    public string Status { get; set; } = string.Empty;
    public string? TrackingNumber { get; set; }
    public string? RecipientCity { get; set; }
    public decimal Amount { get; set; }
    public string Currency { get; set; } = "TRY";
    /// <summary>cod_cash | cod_card | "" (on odemeli — burada gozukmemeli).</summary>
    public string PaymentType { get; set; } = string.Empty;
    public bool IsDelivered { get; set; }
}

public sealed class CodProviderSummaryViewModel
{
    public string Provider { get; set; } = string.Empty;
    public int Count { get; set; }
    public int DeliveredCount { get; set; }
    public decimal TotalAmount { get; set; }
    public decimal CollectedAmount { get; set; }   // sadece teslim edilen
    public decimal PendingAmount => TotalAmount - CollectedAmount;
}

public sealed class CodReportTotalsViewModel
{
    public int Count { get; set; }
    public int DeliveredCount { get; set; }
    public decimal TotalAmount { get; set; }
    public decimal CollectedAmount { get; set; }
    public decimal PendingAmount => TotalAmount - CollectedAmount;
}

public sealed class WorkerDetailViewModel
{
    public string WorkspaceCode { get; set; } = string.Empty;
    public string WorkspaceName { get; set; } = string.Empty;
    public Kargoyeri.Studio.Core.Infrastructure.WorkerCycleStatus WorkerStatus { get; set; } = new();
    public int ReadyProviderCount { get; set; }
    public int ShipmentCount { get; set; }
    public int NotificationCount { get; set; }
    public IReadOnlyCollection<ShipmentListItemResponse> RecentShipments { get; set; } = Array.Empty<ShipmentListItemResponse>();
    public IReadOnlyCollection<NotificationMessageDto> RecentNotifications { get; set; } = Array.Empty<NotificationMessageDto>();
}

// ===========================================================================
// Public tracking page (KVKK uyumlu — herkese acik)
// ===========================================================================

public sealed class PublicTrackingFormViewModel
{
    [Required(ErrorMessage = "Takip numarasi zorunludur.")]
    [StringLength(64, MinimumLength = 4, ErrorMessage = "Takip numarasi 4-64 karakter araliginda olmali.")]
    [Display(Name = "Kargo Takip Numarasi")]
    public string TrackingNumber { get; set; } = string.Empty;

    public string? ErrorMessage { get; set; }
}

public sealed class PublicTrackingResultViewModel
{
    public string TrackingNumber { get; set; } = string.Empty;
    public bool Found { get; set; }

    /// <summary>Tenant marka adi (urun gondericisi).</summary>
    public string? BrandName { get; set; }
    public string? BrandLogoUrl { get; set; }

    public CargoProviderTypeDto? Provider { get; set; }
    public string ProviderText => Provider?.ToString() ?? "—";

    public ShipmentStatusDto? Status { get; set; }
    public string StatusText { get; set; } = "—";
    public string StatusBadge { get; set; } = "neutral"; // success | warning | danger | info | neutral

    public DateTimeOffset? CreatedAtUtc { get; set; }
    public DateTimeOffset? LastUpdatedAtUtc { get; set; }
    public string? EtaText { get; set; }
    public string? EtaHintText { get; set; }

    /// <summary>Sadece il (KVKK — adres detayi gizli).</summary>
    public string? RecipientCity { get; set; }
    public string? RecipientDistrict { get; set; }
    /// <summary>Maskelenmis alici adi (Ahmet Y****).</summary>
    public string? RecipientNameMasked { get; set; }
    public string? MapLabel { get; set; }
    public string? MapQuery { get; set; }
    public string? MapEmbedUrl { get; set; }
    public string? MapExternalUrl { get; set; }
    public string? MapPrivacyText { get; set; }
    public bool HasMap => !string.IsNullOrWhiteSpace(MapEmbedUrl);

    public IReadOnlyList<PublicTrackingEventViewModel> Timeline { get; set; } = Array.Empty<PublicTrackingEventViewModel>();
    public string? Message { get; set; }

    /// <summary>P3-#1 — Yatay durum cubugu icin 5 asamalik gorsel ilerleme.</summary>
    public IReadOnlyList<PublicTrackingStepViewModel> ProgressSteps { get; set; } = Array.Empty<PublicTrackingStepViewModel>();

    /// <summary>0..100 yuzde. Cubuk dolulugu icin (CSS width).</summary>
    public int ProgressPercent { get; set; }

    /// <summary>true ise iptal/basarisiz; cubuk kirmiziya doner.</summary>
    public bool IsTerminalFailure { get; set; }
}

public sealed record PublicTrackingEventViewModel(DateTimeOffset OccurredAtUtc, string Title, string? Note, string Severity);

/// <summary>
/// P3-#1 — Public tracking sayfasinda 5 adimlik gorsel durum cubugunun tek bir asamasi.
/// State: "completed" (✓), "active" (animasyonlu), "pending" (gri), "failed" (kirmizi).
/// </summary>
public sealed record PublicTrackingStepViewModel(string Key, string Label, string Icon, string State);

// ── KVKK md. 7 "Verilerimi Sil" akisi (P1-#2) ───────────────────────────────────
public sealed class AccountDataViewModel
{
    public string TenantKey { get; set; } = "";
    public string TenantName { get; set; } = "";
    public bool IsActive { get; set; }
    public int ProviderCount { get; set; }
    public int GracePeriodDays { get; set; }
    public Kargoyeri.Studio.Core.Infrastructure.KvkkDeletionRequest.State DeletionState { get; set; }
        = new(false, null, null, null, null, null, null, null);
}

// ── API Key yonetimi (P1-#4) ────────────────────────────────────────────────────
public sealed class ApiKeysPageViewModel
{
    public string TenantKey { get; set; } = "";
    public string TenantName { get; set; } = "";
    public IReadOnlyList<Kargoyeri.Studio.Core.Infrastructure.TenantApiKeyStore.ApiKeyRecord> Keys { get; set; }
        = Array.Empty<Kargoyeri.Studio.Core.Infrastructure.TenantApiKeyStore.ApiKeyRecord>();
    public string? NewlyCreatedPlainKey { get; set; }
    public string? NewlyCreatedName { get; set; }
}

// ── Provider Saglik Panosu (P1-#8) ──────────────────────────────────────────────
public sealed class ProviderHealthPageViewModel
{
    public string TenantKey { get; set; } = "";
    public string TenantName { get; set; } = "";
    public TimeSpan Window { get; set; }
    public string WindowText { get; set; } = "24h";
    public IReadOnlyCollection<Kargoyeri.Studio.Core.Infrastructure.ProviderHealthSummary> Summaries { get; set; }
        = Array.Empty<Kargoyeri.Studio.Core.Infrastructure.ProviderHealthSummary>();
}

// ── Bulk Shipment CSV (P1-#9) ───────────────────────────────────────────────────
public sealed class BulkShipmentPageViewModel
{
    public string TenantKey { get; set; } = "";
    public IReadOnlyCollection<Kargoyeri.Studio.Core.Infrastructure.CsvImportJob> Jobs { get; set; }
        = Array.Empty<Kargoyeri.Studio.Core.Infrastructure.CsvImportJob>();
}

// ── Outbound Webhook Delivery Dashboard (P2-#3) ─────────────────────────────────
public sealed class WebhookDeliveryRow
{
    public Guid NotificationId { get; set; }
    public string TenantKey { get; set; } = "";
    public string ShipmentReference { get; set; } = "";
    public NotificationChannelDto Channel { get; set; }
    public NotificationDeliveryStatusDto Status { get; set; }
    public NotificationEventTypeDto EventType { get; set; }
    public string Address { get; set; } = "";
    public DateTimeOffset CreatedAtUtc { get; set; }
    public string? ErrorMessage { get; set; }
    public int Attempts { get; set; }
    public DateTimeOffset? NextAttemptUtc { get; set; }
    public bool Abandoned { get; set; }
}

public sealed class WebhookDeliveryPageViewModel
{
    public string TenantKey { get; set; } = "";
    public string TenantName { get; set; } = "";
    public int TotalCount { get; set; }
    public int DeliveredCount { get; set; }
    public int FailedCount { get; set; }
    public int QueuedCount { get; set; }
    public int RetryPendingCount { get; set; }
    public int AbandonedCount { get; set; }
    public IReadOnlyCollection<WebhookDeliveryRow> Recent { get; set; } = Array.Empty<WebhookDeliveryRow>();
}
