using System.Text.Json;
using Kargoyeri.Application.Services;
using Kargoyeri.Contracts.Enums;

namespace Kargoyeri.Studio.Core.Infrastructure;

public sealed class NotificationTemplateStore
{
    private const string MetadataKey = "notification.templates";
    private readonly CustomerService _customerService;

    public NotificationTemplateStore(CustomerService customerService)
    {
        _customerService = customerService;
    }

    public async Task<IReadOnlyList<NotificationTemplateDefinition>> GetAsync(string tenantKey, CancellationToken ct)
    {
        var profile = await _customerService.GetProfileAsync(tenantKey, ct);
        if (profile is null)
        {
            return BuildDefaults();
        }

        if (!profile.Metadata.TryGetValue(MetadataKey, out var json) || string.IsNullOrWhiteSpace(json))
        {
            return BuildDefaults();
        }

        try
        {
            var items = JsonSerializer.Deserialize<List<NotificationTemplateDefinition>>(json) ?? new();
            return MergeDefaults(items);
        }
        catch
        {
            return BuildDefaults();
        }
    }

    public async Task SaveAsync(string tenantKey, IReadOnlyCollection<NotificationTemplateDefinition> templates, CancellationToken ct)
    {
        var profile = await _customerService.GetProfileAsync(tenantKey, ct)
            ?? throw new InvalidOperationException($"Tenant bulunamadi: {tenantKey}");

        var metadata = new Dictionary<string, string>(profile.Metadata, StringComparer.OrdinalIgnoreCase)
        {
            [MetadataKey] = JsonSerializer.Serialize(templates.OrderBy(x => x.EventType).ThenBy(x => x.Channel))
        };

        await _customerService.UpsertAsync(profile.TenantKey, new Kargoyeri.Contracts.Dtos.UpsertCustomerRequest
        {
            TenantKey = profile.TenantKey,
            Name = profile.Name,
            IsActive = profile.IsActive,
            AllowedProviders = profile.AllowedProviders.ToList(),
            NotificationTargets = profile.NotificationTargets.ToList(),
            Metadata = metadata
        }, ct);
    }

    private static IReadOnlyList<NotificationTemplateDefinition> MergeDefaults(IEnumerable<NotificationTemplateDefinition> templates)
    {
        var map = templates.ToDictionary(
            x => $"{x.EventType}:{x.Channel}",
            x => x,
            StringComparer.OrdinalIgnoreCase);

        foreach (var item in BuildDefaults())
        {
            var key = $"{item.EventType}:{item.Channel}";
            if (!map.ContainsKey(key))
            {
                map[key] = item;
            }
        }

        return map.Values
            .OrderBy(x => x.EventType)
            .ThenBy(x => x.Channel)
            .ToArray();
    }

    private static IReadOnlyList<NotificationTemplateDefinition> BuildDefaults()
    {
        var templates = new List<NotificationTemplateDefinition>();
        foreach (var eventType in Enum.GetValues<NotificationEventTypeDto>())
        {
            templates.Add(new NotificationTemplateDefinition(
                eventType,
                NotificationChannelDto.Email,
                true,
                $"{{{{customerName}}}} - {{{{shipmentReference}}}} - {eventType}",
                DefaultBody(eventType, NotificationChannelDto.Email)));

            templates.Add(new NotificationTemplateDefinition(
                eventType,
                NotificationChannelDto.Sms,
                true,
                $"Kargo {eventType}",
                DefaultBody(eventType, NotificationChannelDto.Sms)));
        }

        return templates;
    }

    private static string DefaultBody(NotificationEventTypeDto eventType, NotificationChannelDto channel)
    {
        var prefix = channel == NotificationChannelDto.Sms ? "Kargo guncellemesi:" : "Merhaba,\n\n";
        return $"{prefix} {{customerName}} / {{shipmentReference}} / {{trackingNumber}} / {{status}}\n{{message}}";
    }
}

public sealed record NotificationTemplateDefinition(
    NotificationEventTypeDto EventType,
    NotificationChannelDto Channel,
    bool IsEnabled,
    string SubjectTemplate,
    string BodyTemplate);
