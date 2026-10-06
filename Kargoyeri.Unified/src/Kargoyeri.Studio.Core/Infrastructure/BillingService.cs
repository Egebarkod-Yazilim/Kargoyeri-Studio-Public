using Microsoft.Extensions.Configuration;

namespace Kargoyeri.Studio.Core.Infrastructure;

/// <summary>
/// P4-#2 — Aylik kullanim faturalandirmasi.
///
/// Konfig (appsettings.json):
/// <code>
/// "Studio": {
///   "Billing": {
///     "Currency": "TRY",
///     "Tier": {
///       "MonthlyBaseFee": 199.00,
///       "IncludedShipments": 250,
///       "PricePerExtraShipment": 0.85,
///       "VatPercent": 20
///     }
///   }
/// }
/// </code>
///
/// Veri kaynagi: TenantMetricsAggregator gunluk snapshot dosyalari.
/// </summary>
public sealed class BillingService
{
    private readonly TenantMetricsAggregator _aggregator;
    private readonly BillingTier _tier;

    public BillingService(TenantMetricsAggregator aggregator, IConfiguration config)
    {
        _aggregator = aggregator;
        _tier = new BillingTier
        {
            Currency = config["Studio:Billing:Currency"] ?? "TRY",
            MonthlyBaseFee = TryParseDecimal(config["Studio:Billing:Tier:MonthlyBaseFee"]) ?? 199.00m,
            IncludedShipments = TryParseInt(config["Studio:Billing:Tier:IncludedShipments"]) ?? 250,
            PricePerExtraShipment = TryParseDecimal(config["Studio:Billing:Tier:PricePerExtraShipment"]) ?? 0.85m,
            VatPercent = TryParseDecimal(config["Studio:Billing:Tier:VatPercent"]) ?? 20m
        };
    }

    public BillingTier Tier => _tier;

    /// <summary>Computes a monthly invoice for the given (year, month).</summary>
    public async Task<IReadOnlyList<TenantInvoice>> ComputeMonthAsync(int year, int month, CancellationToken ct)
    {
        var first = new DateOnly(year, month, 1);
        var last = first.AddMonths(1).AddDays(-1);
        var aggregated = await _aggregator.AggregateRangeAsync(first, last, ct);

        var invoices = new List<TenantInvoice>();
        foreach (var (key, period) in aggregated)
        {
            var billable = period.CreatedTotal;
            var extras = Math.Max(0, billable - _tier.IncludedShipments);
            var subtotal = _tier.MonthlyBaseFee + (extras * _tier.PricePerExtraShipment);
            var vat = decimal.Round(subtotal * _tier.VatPercent / 100m, 2);
            var total = decimal.Round(subtotal + vat, 2);

            invoices.Add(new TenantInvoice
            {
                TenantKey = key,
                TenantName = period.TenantName,
                PeriodFrom = first,
                PeriodTo = last,
                ShipmentsCreated = billable,
                ShipmentsDelivered = period.DeliveredTotal,
                IncludedShipments = _tier.IncludedShipments,
                ExtraShipments = extras,
                BaseFee = _tier.MonthlyBaseFee,
                ExtraFee = decimal.Round(extras * _tier.PricePerExtraShipment, 2),
                Subtotal = decimal.Round(subtotal, 2),
                Vat = vat,
                Total = total,
                Currency = _tier.Currency,
                IssuedAtUtc = DateTimeOffset.UtcNow
            });
        }
        return invoices.OrderBy(i => i.TenantName, StringComparer.OrdinalIgnoreCase).ToList();
    }

    private static decimal? TryParseDecimal(string? s) =>
        decimal.TryParse(s, System.Globalization.NumberStyles.Any, System.Globalization.CultureInfo.InvariantCulture, out var v) ? v : null;
    private static int? TryParseInt(string? s) =>
        int.TryParse(s, out var v) ? v : null;
}

public sealed class BillingTier
{
    public string Currency { get; set; } = "TRY";
    public decimal MonthlyBaseFee { get; set; }
    public int IncludedShipments { get; set; }
    public decimal PricePerExtraShipment { get; set; }
    public decimal VatPercent { get; set; }
}

public sealed class TenantInvoice
{
    public string TenantKey { get; set; } = string.Empty;
    public string TenantName { get; set; } = string.Empty;
    public DateOnly PeriodFrom { get; set; }
    public DateOnly PeriodTo { get; set; }
    public int ShipmentsCreated { get; set; }
    public int ShipmentsDelivered { get; set; }
    public int IncludedShipments { get; set; }
    public int ExtraShipments { get; set; }
    public decimal BaseFee { get; set; }
    public decimal ExtraFee { get; set; }
    public decimal Subtotal { get; set; }
    public decimal Vat { get; set; }
    public decimal Total { get; set; }
    public string Currency { get; set; } = "TRY";
    public DateTimeOffset IssuedAtUtc { get; set; }

    public string InvoiceNumber =>
        $"KY-{PeriodFrom:yyyyMM}-{TenantKey.GetHashCode():X8}";
}
