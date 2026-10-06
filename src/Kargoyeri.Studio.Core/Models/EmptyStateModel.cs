namespace Kargoyeri.Studio.Core.Models;

/// <summary>
/// P3-#4 — Tekrar kullanilabilir bos durum (empty state) bileseni icin model.
/// </summary>
public sealed class EmptyStateModel
{
    public string? Icon { get; set; }
    public string Title { get; set; } = string.Empty;
    public string? Message { get; set; }
    public string? CtaText { get; set; }
    public string? CtaUrl { get; set; }
}
