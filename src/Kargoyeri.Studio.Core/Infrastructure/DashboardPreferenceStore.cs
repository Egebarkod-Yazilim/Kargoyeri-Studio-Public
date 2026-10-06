using System.Text.Json;
using Kargoyeri.Application.Services;
using Kargoyeri.Contracts.Dtos;

namespace Kargoyeri.Studio.Core.Infrastructure;

public sealed class DashboardPreferenceStore
{
    private const string MetadataKey = "dashboard.widgets";
    private static readonly string[] DefaultWidgets = ["overview", "charts", "notifications", "checklist"];

    private readonly CustomerService _customerService;

    public DashboardPreferenceStore(CustomerService customerService)
    {
        _customerService = customerService;
    }

    public async Task<IReadOnlyList<string>> GetAsync(string tenantKey, CancellationToken cancellationToken)
    {
        var profile = await _customerService.GetProfileAsync(tenantKey, cancellationToken);
        if (profile is null)
        {
            return DefaultWidgets;
        }

        if (!profile.Metadata.TryGetValue(MetadataKey, out var json) || string.IsNullOrWhiteSpace(json))
        {
            return DefaultWidgets;
        }

        try
        {
            var values = JsonSerializer.Deserialize<List<string>>(json) ?? new();
            return values.Where(x => !string.IsNullOrWhiteSpace(x))
                .Select(x => x.Trim())
                .Distinct(StringComparer.OrdinalIgnoreCase)
                .ToArray();
        }
        catch
        {
            return DefaultWidgets;
        }
    }

    public async Task SaveAsync(string tenantKey, IEnumerable<string> widgets, CancellationToken cancellationToken)
    {
        var profile = await _customerService.GetProfileAsync(tenantKey, cancellationToken)
            ?? throw new InvalidOperationException($"Tenant '{tenantKey}' bulunamadi.");

        var selected = widgets
            .Where(x => !string.IsNullOrWhiteSpace(x))
            .Select(x => x.Trim())
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .ToArray();

        if (selected.Length == 0)
        {
            selected = DefaultWidgets;
        }

        var metadata = new Dictionary<string, string>(profile.Metadata, StringComparer.OrdinalIgnoreCase)
        {
            [MetadataKey] = JsonSerializer.Serialize(selected)
        };

        await _customerService.UpsertAsync(tenantKey, new UpsertCustomerRequest
        {
            Name = profile.Name,
            IsActive = profile.IsActive,
            AllowedProviders = profile.AllowedProviders.ToList(),
            NotificationTargets = profile.NotificationTargets.ToList(),
            Metadata = metadata
        }, cancellationToken);
    }

    public static IReadOnlyList<string> Defaults => DefaultWidgets;
}
