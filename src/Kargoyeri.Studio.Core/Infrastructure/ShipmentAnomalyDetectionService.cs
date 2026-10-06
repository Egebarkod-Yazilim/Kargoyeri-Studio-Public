using System.Text.Json;
using Kargoyeri.Application.Abstractions.Persistence;
using Kargoyeri.Studio.Core.Models;

namespace Kargoyeri.Studio.Core.Infrastructure;

public sealed class ShipmentAnomalyDetectionService
{
    private const string MetadataDetectedKey = "studio.anomalyDetected";
    private const string MetadataSummaryKey = "studio.anomalySummary";
    private const string MetadataFlagsKey = "studio.anomalyFlags";

    private readonly IShipmentRepository _shipmentRepository;

    public ShipmentAnomalyDetectionService(IShipmentRepository shipmentRepository)
    {
        _shipmentRepository = shipmentRepository;
    }

    public async Task<ShipmentAnomalyReportViewModel> AnalyzeAsync(
        string tenantKey,
        ShipmentAnomalyInput input,
        CancellationToken cancellationToken)
    {
        var history = await _shipmentRepository.ListByDateRangeAsync(
            tenantKey,
            DateTime.UtcNow.Date.AddDays(-90),
            DateTime.UtcNow,
            cancellationToken);

        var flags = new List<ShipmentAnomalyFlagViewModel>();
        if (history.Count < 5)
        {
            return new ShipmentAnomalyReportViewModel
            {
                Summary = "AI anomali kontrolu icin yeterli gecmis veri henuz yok."
            };
        }

        var amounts = history
            .Where(x => x.CollectionAmount.HasValue && x.CollectionAmount.Value > 0)
            .Select(x => x.CollectionAmount!.Value)
            .OrderBy(x => x)
            .ToArray();

        if (input.CollectionAmount.HasValue && amounts.Length >= 5)
        {
            var medianAmount = Median(amounts);
            if (medianAmount > 0 && input.CollectionAmount.Value >= medianAmount * 3)
            {
                flags.Add(new ShipmentAnomalyFlagViewModel
                {
                    Severity = "warning",
                    Title = "Tutar tipik araligin ustunde",
                    Message = $"Bu siparisin tutari gecmis ortanca degerin yaklasik {Math.Round(input.CollectionAmount.Value / medianAmount, 1)} kati."
                });
            }
            else if (medianAmount > 50 && input.CollectionAmount.Value <= medianAmount * 0.3m)
            {
                flags.Add(new ShipmentAnomalyFlagViewModel
                {
                    Severity = "info",
                    Title = "Tutar tipik araligin altinda",
                    Message = "Bu siparisin tahsilat tutari gecmis siparislere gore oldukca dusuk gorunuyor."
                });
            }
        }

        var desis = history.SelectMany(x => x.Packages).Select(x => x.Desi).Where(x => x > 0).OrderBy(x => x).ToArray();
        if (input.Desi > 0 && desis.Length >= 5)
        {
            var medianDesi = Median(desis);
            if (medianDesi > 0 && input.Desi >= medianDesi * 2.5m)
            {
                flags.Add(new ShipmentAnomalyFlagViewModel
                {
                    Severity = "warning",
                    Title = "Desi tipik araligin ustunde",
                    Message = $"Girilen desi degeri gecmis ortanca degerin yaklasik {Math.Round(input.Desi / medianDesi, 1)} kati."
                });
            }
        }

        var weights = history.SelectMany(x => x.Packages).Select(x => x.Weight).Where(x => x > 0).OrderBy(x => x).ToArray();
        if (input.Weight > 0 && weights.Length >= 5)
        {
            var medianWeight = Median(weights);
            if (medianWeight > 0 && input.Weight >= medianWeight * 2.5m)
            {
                flags.Add(new ShipmentAnomalyFlagViewModel
                {
                    Severity = "warning",
                    Title = "Agirlik tipik araligin ustunde",
                    Message = "Girilen agirlik benzer gonderilere gore beklenenden yuksek."
                });
            }
        }

        if (!string.IsNullOrWhiteSpace(input.RecipientCity))
        {
            var citySet = history
                .Where(x => !string.IsNullOrWhiteSpace(x.Recipient.City))
                .Select(x => x.Recipient.City)
                .ToHashSet(StringComparer.OrdinalIgnoreCase);

            if (!citySet.Contains(input.RecipientCity.Trim()) && history.Count >= 12)
            {
                flags.Add(new ShipmentAnomalyFlagViewModel
                {
                    Severity = "info",
                    Title = "Yeni sehir paterni",
                    Message = "Bu alici sehri son 90 gunluk gonderi gecmisinde nadir veya hic gorulmuyor."
                });
            }
        }

        if (!string.IsNullOrWhiteSpace(input.Provider))
        {
            var providerFrequency = history
                .GroupBy(x => x.Provider.ToString(), StringComparer.OrdinalIgnoreCase)
                .ToDictionary(g => g.Key, g => g.Count(), StringComparer.OrdinalIgnoreCase);

            if (providerFrequency.TryGetValue(input.Provider, out var providerCount) &&
                history.Count >= 20 &&
                providerCount * 100 / history.Count < 10)
            {
                flags.Add(new ShipmentAnomalyFlagViewModel
                {
                    Severity = "info",
                    Title = "Nadir provider secimi",
                    Message = "Bu provider secimi son donemde az kullanildigi icin manuel kontrol onerilir."
                });
            }
        }

        return new ShipmentAnomalyReportViewModel
        {
            Summary = flags.Count == 0
                ? "Gecmis veri icinde belirgin bir anomali gorulmedi."
                : $"{flags.Count} potansiyel anomali sinyali bulundu.",
            Flags = flags
        };
    }

    public static void ApplyMetadata(IDictionary<string, string> metadata, ShipmentAnomalyReportViewModel report)
    {
        metadata[MetadataDetectedKey] = report.HasFindings ? "true" : "false";
        metadata[MetadataSummaryKey] = report.Summary;
        metadata[MetadataFlagsKey] = JsonSerializer.Serialize(report.Flags);
    }

    public static ShipmentAnomalyReportViewModel? TryReadMetadata(IReadOnlyDictionary<string, string> metadata)
    {
        if (!metadata.TryGetValue(MetadataDetectedKey, out var detected) ||
            !bool.TryParse(detected, out _))
        {
            return null;
        }

        var summary = metadata.TryGetValue(MetadataSummaryKey, out var summaryValue) ? summaryValue : string.Empty;

        if (!metadata.TryGetValue(MetadataFlagsKey, out var flagsJson) || string.IsNullOrWhiteSpace(flagsJson))
        {
            return new ShipmentAnomalyReportViewModel { Summary = summary };
        }

        try
        {
            var flags = JsonSerializer.Deserialize<List<ShipmentAnomalyFlagViewModel>>(flagsJson) ?? new();
            return new ShipmentAnomalyReportViewModel
            {
                Summary = summary,
                Flags = flags
            };
        }
        catch
        {
            return new ShipmentAnomalyReportViewModel { Summary = summary };
        }
    }

    private static decimal Median(IReadOnlyList<decimal> values)
    {
        if (values.Count == 0)
        {
            return 0;
        }

        return values.Count % 2 == 1
            ? values[values.Count / 2]
            : (values[(values.Count / 2) - 1] + values[values.Count / 2]) / 2m;
    }
}
