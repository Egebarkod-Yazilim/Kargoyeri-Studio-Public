using System.Collections.Concurrent;
using System.Text.Json;

namespace Kargoyeri.Studio.Core.Infrastructure;

public sealed class WorkflowExecutionStore
{
    private readonly ConcurrentDictionary<string, byte> _keys = new(StringComparer.OrdinalIgnoreCase);
    private readonly string _directory;

    public WorkflowExecutionStore(IWebHostEnvironment env)
    {
        _directory = Path.Combine(env.ContentRootPath, "workflow-executions");
        Directory.CreateDirectory(_directory);
        Rehydrate();
    }

    public bool HasFired(string tenantKey, Guid ruleId, string shipmentReference)
    {
        return _keys.ContainsKey(BuildKey(tenantKey, ruleId, shipmentReference));
    }

    public async Task MarkFiredAsync(string tenantKey, Guid ruleId, string shipmentReference, CancellationToken cancellationToken)
    {
        var key = BuildKey(tenantKey, ruleId, shipmentReference);
        if (!_keys.TryAdd(key, 0))
        {
            return;
        }

        var payload = JsonSerializer.Serialize(new WorkflowExecutionRecord
        {
            TenantKey = tenantKey,
            RuleId = ruleId,
            ShipmentReference = shipmentReference,
            FiredAtUtc = DateTimeOffset.UtcNow
        });

        var file = Path.Combine(_directory, $"workflow-{DateTime.UtcNow:yyyyMMdd}.jsonl");
        await File.AppendAllTextAsync(file, payload + Environment.NewLine, cancellationToken);
    }

    private void Rehydrate()
    {
        foreach (var file in Directory.GetFiles(_directory, "workflow-*.jsonl").OrderByDescending(x => x).Take(14))
        {
            foreach (var line in File.ReadLines(file))
            {
                if (string.IsNullOrWhiteSpace(line))
                {
                    continue;
                }

                try
                {
                    var item = JsonSerializer.Deserialize<WorkflowExecutionRecord>(line);
                    if (item is null)
                    {
                        continue;
                    }

                    _keys.TryAdd(BuildKey(item.TenantKey, item.RuleId, item.ShipmentReference), 0);
                }
                catch
                {
                    // Bozuk satirlar rehydrate sirasinda yoksayilir.
                }
            }
        }
    }

    private static string BuildKey(string tenantKey, Guid ruleId, string shipmentReference)
        => $"{tenantKey}:{ruleId}:{shipmentReference}";

    private sealed class WorkflowExecutionRecord
    {
        public string TenantKey { get; set; } = string.Empty;
        public Guid RuleId { get; set; }
        public string ShipmentReference { get; set; } = string.Empty;
        public DateTimeOffset FiredAtUtc { get; set; }
    }
}
