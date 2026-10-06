namespace Kargoyeri.Studio.Core.Models;

public sealed class MarketplaceSyncRunViewModel
{
    public string Platform { get; set; } = string.Empty;
    public DateTimeOffset StartedAtUtc { get; set; }
    public DateTimeOffset? CompletedAtUtc { get; set; }
    public int PulledCount { get; set; }
    public int ImportedCount { get; set; }
    public int SkippedCount { get; set; }
    public int FailedCount { get; set; }
    public bool Success { get; set; }
    public string? Message { get; set; }
}

public sealed class AddressValidationIssueViewModel
{
    public string Severity { get; set; } = "info";
    public string Message { get; set; } = string.Empty;
    public bool IsBlocking { get; set; }
}

public sealed class AddressValidationSummaryViewModel
{
    public bool Enabled { get; set; }
    public bool IsValid { get; set; } = true;
    public bool HasBlockingIssues => Issues.Any(x => x.IsBlocking);
    public bool UsedRemoteProvider { get; set; }
    public string StatusText { get; set; } = string.Empty;
    public string? NormalizedPhone { get; set; }
    public string? NormalizedCity { get; set; }
    public string? NormalizedDistrict { get; set; }
    public string? NormalizedAddressLine1 { get; set; }
    public string? NormalizedSummary =>
        string.Join(" / ", new[] { NormalizedDistrict, NormalizedCity, NormalizedAddressLine1 }.Where(x => !string.IsNullOrWhiteSpace(x)));
    public IReadOnlyCollection<AddressValidationIssueViewModel> Issues { get; set; } = Array.Empty<AddressValidationIssueViewModel>();
}

public sealed class ShipmentAnomalyInput
{
    public string Provider { get; set; } = string.Empty;
    public string? RecipientCity { get; set; }
    public decimal? CollectionAmount { get; set; }
    public decimal Weight { get; set; }
    public decimal Desi { get; set; }
}

public sealed class ShipmentAnomalyFlagViewModel
{
    public string Severity { get; set; } = "info";
    public string Title { get; set; } = string.Empty;
    public string Message { get; set; } = string.Empty;
}

public sealed class ShipmentAnomalyReportViewModel
{
    public string Summary { get; set; } = string.Empty;
    public IReadOnlyCollection<ShipmentAnomalyFlagViewModel> Flags { get; set; } = Array.Empty<ShipmentAnomalyFlagViewModel>();
    public bool HasFindings => Flags.Count > 0;
}
