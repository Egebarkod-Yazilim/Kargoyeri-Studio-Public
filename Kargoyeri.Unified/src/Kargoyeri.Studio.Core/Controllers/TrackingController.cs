using Kargoyeri.Contracts.Dtos;
using Kargoyeri.Contracts.Enums;
using Kargoyeri.Studio.Core.Infrastructure;
using Kargoyeri.Studio.Core.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using System.Globalization;

namespace Kargoyeri.Studio.Core.Controllers;

[AllowAnonymous]
[Route("track")]
public sealed class TrackingController : Controller
{
    private readonly TrackingLookupService _lookup;

    public TrackingController(TrackingLookupService lookup)
    {
        _lookup = lookup;
    }

    [HttpGet("")]
    public IActionResult Index()
    {
        return View(new PublicTrackingFormViewModel());
    }

    [HttpPost("")]
    [ValidateAntiForgeryToken]
    public IActionResult Submit(PublicTrackingFormViewModel form)
    {
        if (!ModelState.IsValid)
        {
            return View("Index", form);
        }

        return RedirectToAction(nameof(Detail), new { trackingNumber = form.TrackingNumber.Trim() });
    }

    [HttpGet("{trackingNumber}")]
    public async Task<IActionResult> Detail(string trackingNumber, CancellationToken cancellationToken)
    {
        if (string.IsNullOrWhiteSpace(trackingNumber))
        {
            return RedirectToAction(nameof(Index));
        }

        var result = await _lookup.FindAsync(trackingNumber.Trim(), cancellationToken);
        var vm = new PublicTrackingResultViewModel
        {
            TrackingNumber = trackingNumber.Trim()
        };

        if (result is null)
        {
            vm.Found = false;
            vm.Message = "Bu takip numarasi sistemimizde bulunamadi. Numarayi kontrol ederek tekrar deneyin.";
            return View("Detail", vm);
        }

        vm.Found = true;
        var branding = WorkspaceFeatureMetadata.ReadBranding(result.Tenant);
        vm.BrandName = !string.IsNullOrWhiteSpace(branding.DisplayName)
            ? branding.DisplayName
            : string.IsNullOrWhiteSpace(result.Tenant.Name) ? result.Tenant.TenantKey : result.Tenant.Name;
        vm.BrandLogoUrl = branding.LogoUrl;
        ViewData["BrandName"] = vm.BrandName;
        ViewData["BrandLogoUrl"] = vm.BrandLogoUrl;
        ViewData["BrandAccentColor"] = branding.AccentColor;
        ViewData["BrandPartnerLabel"] = branding.PartnerLabel;
        ViewData["BrandFooterText"] = branding.FooterText;
        ViewData["BrandHidePoweredBy"] = branding.HidePoweredBy;

        var shipment = result.Shipment;
        vm.Provider = shipment.Provider;
        vm.Status = shipment.Status;
        vm.StatusText = StatusText(shipment.Status);
        vm.StatusBadge = StatusBadge(shipment.Status);
        vm.CreatedAtUtc = shipment.CreatedAtUtc;
        vm.LastUpdatedAtUtc = shipment.UpdatedAtUtc;
        vm.RecipientCity = shipment.Recipient?.City;
        vm.RecipientDistrict = shipment.Recipient?.District;
        vm.RecipientNameMasked = MaskName(shipment.Recipient?.Name);

        var eta = BuildEtaDetails(shipment.Metadata, shipment.Status);
        vm.EtaText = eta.DisplayText;
        vm.EtaHintText = eta.HintText;

        var map = BuildMapDetails(shipment.Metadata, shipment.Recipient?.District, shipment.Recipient?.City);
        vm.MapLabel = map.Label;
        vm.MapQuery = map.Query;
        vm.MapEmbedUrl = map.EmbedUrl;
        vm.MapExternalUrl = map.ExternalUrl;
        vm.MapPrivacyText = map.PrivacyText;

        vm.Timeline = BuildTimeline(shipment, result.Logs);

        // P3-#1 — Gorsel ilerleme cubugu (5 adim).
        var progress = BuildProgressSteps(shipment.Status);
        vm.ProgressSteps = progress.Steps;
        vm.ProgressPercent = progress.Percent;
        vm.IsTerminalFailure = progress.IsFailure;

        return View("Detail", vm);
    }

    /// <summary>
    /// P3-#1 — Durum DTO'sundan 5-adimli gorsel cubuk veri yapisi uretir.
    /// Adim sirasi: Hazirlandi -> Kargoda -> Yolda -> Dagitimda -> Teslim Edildi.
    /// Cancelled/Failed durumda IsFailure=true ile cubuk kirmiziya doner.
    /// </summary>
    private static (IReadOnlyList<PublicTrackingStepViewModel> Steps, int Percent, bool IsFailure)
        BuildProgressSteps(ShipmentStatusDto status)
    {
        // Status -> hangi adimda oldugu (0..4). -1 = pre-step (Pending/Queued henuz adim 0'da).
        // Bu projeksiyon kargo firmasi terminolojisini ortak bir 5-asama gorseline indirger.
        var idx = status switch
        {
            ShipmentStatusDto.Pending          => 0,  // Hazirlandi
            ShipmentStatusDto.Queued           => 0,
            ShipmentStatusDto.LabelReady       => 1,  // Kargoda (etiket basildi, firma bekliyor)
            ShipmentStatusDto.ProviderAccepted => 1,
            ShipmentStatusDto.InTransit        => 2,  // Yolda
            ShipmentStatusDto.Delivered        => 4,  // Teslim Edildi
            ShipmentStatusDto.Cancelled        => -1,
            ShipmentStatusDto.Failed           => -1,
            _                                  => 0
        };

        var isFailure = status is ShipmentStatusDto.Cancelled or ShipmentStatusDto.Failed;

        // Step tanimlari (anahtar, etiket, unicode ikon).
        var defs = new (string Key, string Label, string Icon)[]
        {
            ("ready",       "Hazirlandi",   "\U0001F4E6"), // 📦
            ("with-carrier","Kargoda",      "\U0001F3F7"), // 🏷
            ("in-transit",  "Yolda",        "\U0001F69A"), // 🚚
            ("out-delivery","Dagitimda",    "\U0001F4CD"), // 📍
            ("delivered",   "Teslim Edildi","\u2705")      // ✅
        };

        var steps = new List<PublicTrackingStepViewModel>(defs.Length);
        for (var i = 0; i < defs.Length; i++)
        {
            string state;
            if (isFailure)
            {
                state = i == 0 ? "failed" : "pending";
            }
            else if (i < idx) state = "completed";
            else if (i == idx) state = "active";
            else state = "pending";

            steps.Add(new PublicTrackingStepViewModel(defs[i].Key, defs[i].Label, defs[i].Icon, state));
        }

        // Yuzde — adim 0'da %5 (gorsellik), adim 4'te %100. Failure'da 0.
        int percent = isFailure ? 0 : idx switch
        {
            <= 0 => 5,
            1    => 30,
            2    => 55,
            3    => 80,
            _    => 100
        };

        return (steps, percent, isFailure);
    }

    private static IReadOnlyList<PublicTrackingEventViewModel> BuildTimeline(
        ShipmentDetailResponse shipment,
        IReadOnlyCollection<ShipmentOperationLogDto> logs)
    {
        var items = new List<PublicTrackingEventViewModel>
        {
            new(shipment.CreatedAtUtc, "Gonderi olusturuldu", null, "info")
        };

        var publicOps = new HashSet<string>(StringComparer.OrdinalIgnoreCase)
        {
            "StatusChanged", "Refresh", "Cancelled", "Delivered", "InTransit", "LabelReady", "ProviderAccepted"
        };

        foreach (var log in logs.OrderBy(x => x.OccurredAtUtc))
        {
            if (!publicOps.Contains(log.Operation))
            {
                continue;
            }

            var note = log.Message.Length > 120 ? log.Message[..120] + "..." : log.Message;
            items.Add(new PublicTrackingEventViewModel(
                log.OccurredAtUtc,
                TitleForOperation(log.Operation),
                note,
                log.Severity));
        }

        return items.OrderBy(x => x.OccurredAtUtc).ToArray();
    }

    private static string TitleForOperation(string op) => op switch
    {
        "ProviderAccepted" => "Kargo firmasi tarafindan kabul edildi",
        "LabelReady" => "Etiket olusturuldu",
        "InTransit" => "Yolda",
        "Delivered" => "Teslim edildi",
        "Cancelled" => "Iptal edildi",
        "StatusChanged" => "Durum guncellendi",
        "Refresh" => "Durum sorgulandi",
        _ => op
    };

    private static string StatusText(ShipmentStatusDto status) => status switch
    {
        ShipmentStatusDto.Pending => "Hazirlaniyor",
        ShipmentStatusDto.Queued => "Kuyrukta",
        ShipmentStatusDto.ProviderAccepted => "Kargo firmasinda",
        ShipmentStatusDto.LabelReady => "Etiket hazir",
        ShipmentStatusDto.InTransit => "Yolda",
        ShipmentStatusDto.Delivered => "Teslim edildi",
        ShipmentStatusDto.Cancelled => "Iptal edildi",
        ShipmentStatusDto.Failed => "Olusturulamadi",
        _ => status.ToString()
    };

    private static string StatusBadge(ShipmentStatusDto status) => status switch
    {
        ShipmentStatusDto.Delivered => "success",
        ShipmentStatusDto.InTransit => "info",
        ShipmentStatusDto.Cancelled => "danger",
        ShipmentStatusDto.Failed => "danger",
        ShipmentStatusDto.LabelReady => "info",
        ShipmentStatusDto.ProviderAccepted => "info",
        ShipmentStatusDto.Pending => "warning",
        ShipmentStatusDto.Queued => "warning",
        _ => "neutral"
    };

    private static EtaDetails BuildEtaDetails(Dictionary<string, string>? metadata, ShipmentStatusDto status)
    {
        if (status is ShipmentStatusDto.Delivered or ShipmentStatusDto.Cancelled or ShipmentStatusDto.Failed)
        {
            return EtaDetails.Empty;
        }

        if (metadata is null || metadata.Count == 0)
        {
            return EtaDetails.Empty;
        }

        var etaValue = FirstMetadata(
            metadata,
            "delivery.eta",
            "delivery.etaUtc",
            "delivery.estimatedAt",
            "eta",
            "etaUtc",
            "estimatedDeliveryAt",
            "estimatedDeliveryAtUtc",
            "provider.eta",
            "provider.estimatedDeliveryAt");

        var etaDate = FirstMetadata(
            metadata,
            "delivery.etaDate",
            "eta.date",
            "estimatedDeliveryDate",
            "provider.etaDate");

        var etaWindow = NormalizeEtaWindow(FirstMetadata(
            metadata,
            "delivery.etaWindow",
            "delivery.etaPeriod",
            "delivery.slot",
            "eta.window",
            "eta.period",
            "provider.etaWindow",
            "provider.deliverySlot"));

        var etaText = FirstMetadata(
            metadata,
            "delivery.etaText",
            "eta.text",
            "provider.etaText");

        var etaAt = TryParseEta(etaValue);
        var etaDateOnly = TryParseEtaDate(etaDate);
        if (etaAt is null && etaDateOnly is null && string.IsNullOrWhiteSpace(etaWindow) && string.IsNullOrWhiteSpace(etaText))
        {
            return EtaDetails.Empty;
        }

        if (etaAt is not null)
        {
            var local = etaAt.Value.ToLocalTime();
            etaWindow ??= GuessEtaWindow(local);
            var datePart = local.ToString("dd.MM", CultureInfo.InvariantCulture);

            if (!string.IsNullOrWhiteSpace(etaWindow))
            {
                return new EtaDetails(
                    $"Tahmini teslim: {datePart} {etaWindow}",
                    "Teslimat penceresi kargo firmasinin sagladigi tahmine gore gosteriliyor.");
            }

            return new EtaDetails(
                $"Tahmini teslim: {local:dd.MM HH:mm}",
                "Saat bilgisi kargo firmasinin son durum verisinden alindi.");
        }

        if (etaDateOnly is not null)
        {
            var datePart = etaDateOnly.Value.ToString("dd.MM", CultureInfo.InvariantCulture);
            var display = string.IsNullOrWhiteSpace(etaWindow)
                ? $"Tahmini teslim: {datePart}"
                : $"Tahmini teslim: {datePart} {etaWindow}";

            return new EtaDetails(
                display,
                "Tarih bilgisi kargo firmasinin planlanan teslim gunune gore gosteriliyor.");
        }

        if (!string.IsNullOrWhiteSpace(etaText))
        {
            return new EtaDetails(
                $"Tahmini teslim: {etaText.Trim()}",
                "Teslimat penceresi kargo firmasinin tahmini bilgisine dayaniyor.");
        }

        return new EtaDetails(
            $"Tahmini teslim: {etaWindow}",
            "Teslimat penceresi kargo firmasinin tahmini bilgisine dayaniyor.");
    }

    private static MapDetails BuildMapDetails(Dictionary<string, string>? metadata, string? district, string? city)
    {
        if (TryParseCoordinates(metadata, out var latitude, out var longitude))
        {
            var coordinateQuery = string.Create(
                CultureInfo.InvariantCulture,
                $"{latitude:0.######},{longitude:0.######}");

            return new MapDetails(
                coordinateQuery,
                BuildLocationLabel(
                    FirstMetadata(
                        metadata!,
                        "delivery.locationLabel",
                        "location.label",
                        "provider.locationLabel",
                        "provider.branchName",
                        "delivery.branchName",
                        "lastBranch",
                        "lastHub"),
                    district,
                    city),
                $"https://www.google.com/maps?q={Uri.EscapeDataString(coordinateQuery)}&z=13&output=embed",
                $"https://www.google.com/maps/search/?api=1&query={Uri.EscapeDataString(coordinateQuery)}",
                "Konum, provider tarafindan paylasilan bolgesel koordinata gore gosterilir.");
        }

        var query = FirstNonEmpty(
            FirstMetadata(
                metadata,
                "delivery.locationLabel",
                "location.label",
                "provider.locationLabel",
                "provider.branchName",
                "delivery.branchName",
                "lastBranch",
                "lastHub"),
            BuildMapQuery(district, city));

        if (string.IsNullOrWhiteSpace(query))
        {
            return MapDetails.Empty;
        }

        return new MapDetails(
            query,
            BuildLocationLabel(query, district, city),
            $"https://www.google.com/maps?q={Uri.EscapeDataString(query)}&output=embed",
            $"https://www.google.com/maps/search/?api=1&query={Uri.EscapeDataString(query)}",
            "Tam adres yerine sadece bolgesel konum gosterilir.");
    }

    private static string? BuildMapQuery(string? district, string? city)
    {
        var parts = new[] { district?.Trim(), city?.Trim() }
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .ToArray();

        return parts.Length == 0 ? null : string.Join(", ", parts);
    }

    private static string? BuildLocationLabel(string? primaryLabel, string? district, string? city)
    {
        var region = BuildMapQuery(district, city);
        if (string.IsNullOrWhiteSpace(primaryLabel))
        {
            return region;
        }

        if (string.IsNullOrWhiteSpace(region) ||
            primaryLabel.Contains(region, StringComparison.OrdinalIgnoreCase))
        {
            return primaryLabel;
        }

        return $"{primaryLabel} - {region}";
    }

    private static string? FirstMetadata(Dictionary<string, string>? metadata, params string[] keys)
    {
        if (metadata is null || metadata.Count == 0)
        {
            return null;
        }

        foreach (var key in keys)
        {
            var match = metadata.FirstOrDefault(x => string.Equals(x.Key, key, StringComparison.OrdinalIgnoreCase));
            if (!string.IsNullOrWhiteSpace(match.Value))
            {
                return match.Value.Trim();
            }
        }

        return null;
    }

    private static string? FirstNonEmpty(params string?[] values)
    {
        return values.FirstOrDefault(x => !string.IsNullOrWhiteSpace(x))?.Trim();
    }

    private static DateTimeOffset? TryParseEta(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        if (long.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var unixValue))
        {
            try
            {
                return unixValue > 9_999_999_999
                    ? DateTimeOffset.FromUnixTimeMilliseconds(unixValue)
                    : DateTimeOffset.FromUnixTimeSeconds(unixValue);
            }
            catch (ArgumentOutOfRangeException)
            {
                return null;
            }
        }

        return DateTimeOffset.TryParse(
            value,
            CultureInfo.InvariantCulture,
            DateTimeStyles.AssumeUniversal | DateTimeStyles.AllowWhiteSpaces,
            out var parsed)
            ? parsed
            : null;
    }

    private static DateOnly? TryParseEtaDate(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var formats = new[] { "yyyy-MM-dd", "dd.MM.yyyy", "dd.MM", "yyyy/MM/dd" };
        if (DateOnly.TryParseExact(value, formats, CultureInfo.InvariantCulture, DateTimeStyles.AllowWhiteSpaces, out var parsedExact))
        {
            return parsedExact;
        }

        return DateOnly.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.AllowWhiteSpaces, out var parsed)
            ? parsed
            : null;
    }

    private static string? NormalizeEtaWindow(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var normalized = value.Trim().ToLowerInvariant();
        return normalized switch
        {
            "morning" or "sabah" => "sabah",
            "afternoon" or "ogle" or "ogleden-sonra" or "ogleden sonra" => "ogleden sonra",
            "evening" or "aksam" => "aksam",
            _ => value.Trim()
        };
    }

    private static string GuessEtaWindow(DateTimeOffset value)
    {
        var hour = value.Hour;
        return hour switch
        {
            < 12 => "sabah",
            < 17 => "ogleden sonra",
            _ => "aksam"
        };
    }

    private static bool TryParseCoordinates(
        Dictionary<string, string>? metadata,
        out decimal latitude,
        out decimal longitude)
    {
        latitude = 0;
        longitude = 0;
        if (metadata is null || metadata.Count == 0)
        {
            return false;
        }

        var latitudeValue = FirstMetadata(
            metadata,
            "delivery.latitude",
            "delivery.lat",
            "location.latitude",
            "location.lat",
            "recipient.latitude",
            "recipient.lat",
            "provider.latitude",
            "provider.lat",
            "last.latitude",
            "last.lat",
            "latitude",
            "lat");

        var longitudeValue = FirstMetadata(
            metadata,
            "delivery.longitude",
            "delivery.lng",
            "delivery.lon",
            "location.longitude",
            "location.lng",
            "location.lon",
            "recipient.longitude",
            "recipient.lng",
            "recipient.lon",
            "provider.longitude",
            "provider.lng",
            "provider.lon",
            "last.longitude",
            "last.lng",
            "last.lon",
            "longitude",
            "lng",
            "lon");

        if (!decimal.TryParse(latitudeValue, NumberStyles.Float, CultureInfo.InvariantCulture, out latitude) ||
            !decimal.TryParse(longitudeValue, NumberStyles.Float, CultureInfo.InvariantCulture, out longitude))
        {
            return false;
        }

        return latitude is >= -90 and <= 90 &&
               longitude is >= -180 and <= 180;
    }

    private static string? MaskName(string? fullName)
    {
        if (string.IsNullOrWhiteSpace(fullName))
        {
            return null;
        }

        var parts = fullName.Trim().Split(' ', StringSplitOptions.RemoveEmptyEntries);
        if (parts.Length == 1)
        {
            var part = parts[0];
            return part.Length <= 1 ? part : part[..1] + new string('*', Math.Min(4, part.Length - 1));
        }

        var first = parts[0];
        var lastInitial = parts[^1].Length > 0 ? parts[^1][..1] : string.Empty;
        return $"{first} {lastInitial}****";
    }

    private readonly record struct EtaDetails(string? DisplayText, string? HintText)
    {
        public static EtaDetails Empty => new(null, null);
    }

    private readonly record struct MapDetails(
        string? Query,
        string? Label,
        string? EmbedUrl,
        string? ExternalUrl,
        string? PrivacyText)
    {
        public static MapDetails Empty => new(null, null, null, null, null);
    }
}
