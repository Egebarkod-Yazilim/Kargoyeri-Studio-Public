using System.Globalization;

namespace Kargoyeri.Studio.Core.Infrastructure;

/// <summary>
/// KVKK md. 7 — "Verilerimi Sil" talebinin tenant Metadata uzerinden saklanmasi.
/// JSON dosya modu ve SQL Server modu — ikisinde de calisir cunku Metadata bir
/// string-string sozlugudur (bkz. CustomerTenant.Metadata).
/// </summary>
public static class KvkkDeletionRequest
{
    // Metadata anahtarlari
    public const string KeyRequestedAt   = "kvkk.deletion.requestedAt";
    public const string KeyRequestedBy   = "kvkk.deletion.requestedBy";
    public const string KeyReason        = "kvkk.deletion.reason";
    public const string KeyStatus        = "kvkk.deletion.status";        // pending | approved | cancelled
    public const string KeyHardDeleteAt  = "kvkk.deletion.scheduledHardDeleteAt";
    public const string KeyApprovedAt    = "kvkk.deletion.approvedAt";
    public const string KeyApprovedBy    = "kvkk.deletion.approvedBy";

    public const string StatusPending    = "pending";
    public const string StatusApproved   = "approved";
    public const string StatusCancelled  = "cancelled";

    /// <summary>30 gun bekleme suresi (KVKK md. 7 + DATA-RETENTION.md §3).</summary>
    public const int GracePeriodDays = 30;

    public sealed record State(
        bool             HasRequest,
        string?          Status,
        DateTimeOffset?  RequestedAt,
        string?          RequestedBy,
        string?          Reason,
        DateTimeOffset?  HardDeleteAt,
        DateTimeOffset?  ApprovedAt,
        string?          ApprovedBy)
    {
        public bool IsPending  => string.Equals(Status, StatusPending,  StringComparison.OrdinalIgnoreCase);
        public bool IsApproved => string.Equals(Status, StatusApproved, StringComparison.OrdinalIgnoreCase);

        public int DaysRemaining
        {
            get
            {
                if (HardDeleteAt is null) return 0;
                var diff = (HardDeleteAt.Value - DateTimeOffset.UtcNow).TotalDays;
                return diff <= 0 ? 0 : (int)Math.Ceiling(diff);
            }
        }
    }

    public static State Read(IReadOnlyDictionary<string, string> metadata)
    {
        if (!metadata.TryGetValue(KeyStatus, out var status) || string.IsNullOrWhiteSpace(status))
            return new State(false, null, null, null, null, null, null, null);

        return new State(
            HasRequest:   true,
            Status:       status,
            RequestedAt:  ParseDate(metadata, KeyRequestedAt),
            RequestedBy:  Get(metadata, KeyRequestedBy),
            Reason:       Get(metadata, KeyReason),
            HardDeleteAt: ParseDate(metadata, KeyHardDeleteAt),
            ApprovedAt:   ParseDate(metadata, KeyApprovedAt),
            ApprovedBy:   Get(metadata, KeyApprovedBy));
    }

    /// <summary>Yeni silme talebi olustur — pending durumunda.</summary>
    public static Dictionary<string, string> ApplyRequest(
        IReadOnlyDictionary<string, string> metadata,
        string requestedBy,
        string? reason)
    {
        var now = DateTimeOffset.UtcNow;
        var copy = Copy(metadata);
        copy[KeyRequestedAt]  = now.ToString("o", CultureInfo.InvariantCulture);
        copy[KeyRequestedBy]  = requestedBy ?? "(unknown)";
        copy[KeyReason]       = (reason ?? "").Trim();
        copy[KeyStatus]       = StatusPending;
        copy[KeyHardDeleteAt] = now.AddDays(GracePeriodDays).ToString("o", CultureInfo.InvariantCulture);
        copy.Remove(KeyApprovedAt);
        copy.Remove(KeyApprovedBy);
        return copy;
    }

    /// <summary>Talebi iptal et — Metadata'dan tum kvkk.deletion.* anahtarlarini kaldirmak yerine
    /// status=cancelled isaretler (denetim izi icin).</summary>
    public static Dictionary<string, string> ApplyCancellation(IReadOnlyDictionary<string, string> metadata)
    {
        var copy = Copy(metadata);
        copy[KeyStatus] = StatusCancelled;
        return copy;
    }

    /// <summary>Admin onayi — hard-delete tarihini guncellemez (request sirasinda hesaplandi).</summary>
    public static Dictionary<string, string> ApplyApproval(
        IReadOnlyDictionary<string, string> metadata,
        string approvedBy)
    {
        var copy = Copy(metadata);
        copy[KeyStatus]      = StatusApproved;
        copy[KeyApprovedAt]  = DateTimeOffset.UtcNow.ToString("o", CultureInfo.InvariantCulture);
        copy[KeyApprovedBy]  = approvedBy ?? "(unknown)";
        return copy;
    }

    private static Dictionary<string, string> Copy(IReadOnlyDictionary<string, string> source)
        => new(source, StringComparer.OrdinalIgnoreCase);

    private static string? Get(IReadOnlyDictionary<string, string> m, string k)
        => m.TryGetValue(k, out var v) ? v : null;

    private static DateTimeOffset? ParseDate(IReadOnlyDictionary<string, string> m, string k)
        => m.TryGetValue(k, out var v) && DateTimeOffset.TryParse(v, CultureInfo.InvariantCulture, DateTimeStyles.RoundtripKind, out var dt)
           ? dt : null;
}
