using System.Text.Json;
using Kargoyeri.Application.Services;
using Kargoyeri.Contracts.Dtos;

namespace Kargoyeri.Studio.Core.Infrastructure;

/// <summary>
/// Kurye/pickup randevularini tenant Metadata["pickups"] anahtarinda JSON olarak saklar.
/// Boylece mevcut storage (JSON file / SQL Server) ile otomatik persist olur.
/// </summary>
public sealed class PickupRequestService
{
    private const string MetaKey = "pickups";
    private readonly CustomerService _customerService;

    public PickupRequestService(CustomerService customerService)
    {
        _customerService = customerService;
    }

    // ── Okuma ──────────────────────────────────────────────────────────────────

    public async Task<List<PickupRequest>> GetAllAsync(string tenantKey, CancellationToken ct)
    {
        var profile = await _customerService.GetProfileAsync(tenantKey, ct);
        return Deserialize(profile?.Metadata).OrderByDescending(p => p.CreatedAtUtc).ToList();
    }

    public async Task<PickupRequest?> GetByIdAsync(string tenantKey, string id, CancellationToken ct)
    {
        var all = await GetAllAsync(tenantKey, ct);
        return all.FirstOrDefault(p => string.Equals(p.Id, id, StringComparison.OrdinalIgnoreCase));
    }

    // ── Olusturma ──────────────────────────────────────────────────────────────

    public async Task<PickupRequest> CreateAsync(
        string tenantKey,
        PickupRequest draft,
        string createdBy,
        CancellationToken ct)
    {
        var profile = await _customerService.GetProfileAsync(tenantKey, ct)
            ?? throw new InvalidOperationException($"Tenant bulunamadi: {tenantKey}");

        var list = Deserialize(profile.Metadata);

        var newItem = draft with
        {
            Id            = Guid.NewGuid().ToString("N"),
            CreatedBy     = string.IsNullOrWhiteSpace(createdBy) ? "system" : createdBy,
            CreatedAtUtc  = DateTimeOffset.UtcNow,
            Status        = string.IsNullOrWhiteSpace(draft.Status) ? PickupStatus.Draft : draft.Status,
        };

        list.Add(newItem);
        await SaveAsync(profile, list, ct);
        return newItem;
    }

    // ── Durum gecisleri ────────────────────────────────────────────────────────

    public async Task<bool> MarkRequestedAsync(
        string tenantKey, string id, string? confirmationCode, CancellationToken ct)
    {
        return await UpdateAsync(tenantKey, id, p => p with
        {
            Status            = PickupStatus.Requested,
            ConfirmationCode  = string.IsNullOrWhiteSpace(confirmationCode) ? p.ConfirmationCode : confirmationCode.Trim(),
            ScheduledAtUtc    = p.ScheduledAtUtc ?? DateTimeOffset.UtcNow
        }, ct);
    }

    public async Task<bool> MarkConfirmedAsync(
        string tenantKey, string id, string? confirmationCode, CancellationToken ct)
    {
        return await UpdateAsync(tenantKey, id, p => p with
        {
            Status            = PickupStatus.Confirmed,
            ConfirmationCode  = string.IsNullOrWhiteSpace(confirmationCode) ? p.ConfirmationCode : confirmationCode.Trim()
        }, ct);
    }

    public async Task<bool> MarkCompletedAsync(string tenantKey, string id, CancellationToken ct)
    {
        return await UpdateAsync(tenantKey, id, p => p with
        {
            Status         = PickupStatus.Completed,
            CompletedAtUtc = DateTimeOffset.UtcNow
        }, ct);
    }

    public async Task<bool> CancelAsync(string tenantKey, string id, string? reason, CancellationToken ct)
    {
        return await UpdateAsync(tenantKey, id, p => p with
        {
            Status         = PickupStatus.Cancelled,
            CancelReason   = string.IsNullOrWhiteSpace(reason) ? null : reason.Trim(),
            CancelledAtUtc = DateTimeOffset.UtcNow
        }, ct);
    }

    public async Task<bool> DeleteDraftAsync(string tenantKey, string id, CancellationToken ct)
    {
        var profile = await _customerService.GetProfileAsync(tenantKey, ct);
        if (profile is null) return false;

        var list = Deserialize(profile.Metadata);
        var idx  = list.FindIndex(p => string.Equals(p.Id, id, StringComparison.OrdinalIgnoreCase));
        if (idx < 0) return false;
        if (!string.Equals(list[idx].Status, PickupStatus.Draft, StringComparison.OrdinalIgnoreCase))
            return false; // sadece taslak silinebilir

        list.RemoveAt(idx);
        await SaveAsync(profile, list, ct);
        return true;
    }

    // ── Yardimcilar ────────────────────────────────────────────────────────────

    private async Task<bool> UpdateAsync(
        string tenantKey, string id, Func<PickupRequest, PickupRequest> mutate, CancellationToken ct)
    {
        var profile = await _customerService.GetProfileAsync(tenantKey, ct);
        if (profile is null) return false;

        var list = Deserialize(profile.Metadata);
        var idx  = list.FindIndex(p => string.Equals(p.Id, id, StringComparison.OrdinalIgnoreCase));
        if (idx < 0) return false;

        list[idx] = mutate(list[idx]);
        await SaveAsync(profile, list, ct);
        return true;
    }

    private async Task SaveAsync(CustomerProfileDto profile, List<PickupRequest> list, CancellationToken ct)
    {
        var metadata = new Dictionary<string, string>(profile.Metadata, StringComparer.OrdinalIgnoreCase)
        {
            [MetaKey] = JsonSerializer.Serialize(list)
        };

        await _customerService.UpsertAsync(profile.TenantKey, new UpsertCustomerRequest
        {
            TenantKey           = profile.TenantKey,
            Name                = profile.Name,
            IsActive            = profile.IsActive,
            AllowedProviders    = profile.AllowedProviders.ToList(),
            NotificationTargets = profile.NotificationTargets.ToList(),
            Metadata            = metadata
        }, ct);
    }

    private static List<PickupRequest> Deserialize(Dictionary<string, string>? metadata)
    {
        if (metadata is null || !metadata.TryGetValue(MetaKey, out var json) || string.IsNullOrWhiteSpace(json))
            return new List<PickupRequest>();
        try
        {
            return JsonSerializer.Deserialize<List<PickupRequest>>(json) ?? new();
        }
        catch
        {
            return new();
        }
    }

    /// <summary>Profile zaten yuklenmisken hizli sayim.</summary>
    public static int CountActive(Dictionary<string, string>? metadata)
    {
        if (metadata is null || !metadata.TryGetValue(MetaKey, out var json) || string.IsNullOrWhiteSpace(json))
            return 0;
        try
        {
            var list = JsonSerializer.Deserialize<List<PickupRequest>>(json);
            return list?.Count(p =>
                p.Status is PickupStatus.Draft or PickupStatus.Requested or PickupStatus.Confirmed) ?? 0;
        }
        catch { return 0; }
    }
}

/// <summary>Pickup durumlari.</summary>
public static class PickupStatus
{
    public const string Draft     = "Draft";       // Yerel taslak; henuz kargo firmasina iletilmedi
    public const string Requested = "Requested";   // Kargo firmasina iletildi (manuel arama veya API)
    public const string Confirmed = "Confirmed";   // Firma randevuyu konfirme etti
    public const string Completed = "Completed";   // Kurye geldi, paketleri aldi
    public const string Cancelled = "Cancelled";

    public static IReadOnlyList<string> All => new[] { Draft, Requested, Confirmed, Completed, Cancelled };

    public static bool IsTerminal(string? status) =>
        status is Completed or Cancelled;
}

public sealed record PickupRequest(
    string          Id,
    string          ProviderType,
    string          PickupDate,         // "yyyy-MM-dd" formatinda yerel tarih
    string          TimeWindow,         // serbest, "09:00-12:00" / "Sabah" gibi
    string          ContactName,
    string          ContactPhone,
    string          AddressLine,
    string          City,
    string          District,
    int             ParcelCount,
    decimal?        TotalWeightKg,
    string?         Notes,
    string          Status,
    string?         ConfirmationCode,
    string          CreatedBy,
    DateTimeOffset  CreatedAtUtc,
    DateTimeOffset? ScheduledAtUtc      = null,
    DateTimeOffset? CompletedAtUtc      = null,
    DateTimeOffset? CancelledAtUtc      = null,
    string?         CancelReason        = null,
    List<string>?   ShipmentReferences  = null);
