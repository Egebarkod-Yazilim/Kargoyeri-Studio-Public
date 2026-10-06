using System.Text.Json;
using Kargoyeri.Application.Services;
using Kargoyeri.Contracts.Dtos;
using Kargoyeri.Studio.Core.Models;

namespace Kargoyeri.Studio.Core.Infrastructure;

public sealed class WorkflowRuleStore
{
    private const string MetadataKey = "workflow.rules";
    private readonly CustomerService _customerService;

    public WorkflowRuleStore(CustomerService customerService)
    {
        _customerService = customerService;
    }

    public async Task<IReadOnlyList<WorkflowRuleDefinition>> GetAsync(string tenantKey, CancellationToken cancellationToken)
    {
        var profile = await _customerService.GetProfileAsync(tenantKey, cancellationToken);
        if (profile is null)
        {
            return Array.Empty<WorkflowRuleDefinition>();
        }

        if (!profile.Metadata.TryGetValue(MetadataKey, out var json) || string.IsNullOrWhiteSpace(json))
        {
            return Array.Empty<WorkflowRuleDefinition>();
        }

        try
        {
            var rules = JsonSerializer.Deserialize<List<WorkflowRuleDefinition>>(json) ?? new();
            return rules.OrderByDescending(x => x.CreatedAtUtc).ToArray();
        }
        catch
        {
            return Array.Empty<WorkflowRuleDefinition>();
        }
    }

    public async Task AddAsync(string tenantKey, WorkflowRuleDefinition rule, CancellationToken cancellationToken)
    {
        var rules = (await GetAsync(tenantKey, cancellationToken)).ToList();
        rules.Add(rule);
        await SaveAsync(tenantKey, rules, cancellationToken);
    }

    public async Task RemoveAsync(string tenantKey, Guid ruleId, CancellationToken cancellationToken)
    {
        var rules = (await GetAsync(tenantKey, cancellationToken))
            .Where(x => x.Id != ruleId)
            .ToArray();
        await SaveAsync(tenantKey, rules, cancellationToken);
    }

    public async Task SaveAsync(string tenantKey, IEnumerable<WorkflowRuleDefinition> rules, CancellationToken cancellationToken)
    {
        var profile = await _customerService.GetProfileAsync(tenantKey, cancellationToken)
            ?? throw new InvalidOperationException($"Tenant '{tenantKey}' bulunamadi.");

        var metadata = new Dictionary<string, string>(profile.Metadata, StringComparer.OrdinalIgnoreCase)
        {
            [MetadataKey] = JsonSerializer.Serialize(rules.OrderByDescending(x => x.CreatedAtUtc))
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
}
