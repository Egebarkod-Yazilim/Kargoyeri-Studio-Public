// Generated from assembly metadata during repository recovery.
#nullable enable
namespace Kargoyeri.Application.Options;

public sealed partial class ProcessingOptions
{
    public int RefreshIntervalSeconds { get; set; }
    public int MaxRefreshBatchSize { get; set; }
    public int MaxRetryCount { get; set; }
    public int ProviderTimeoutSeconds { get; set; }
    public int BatchItemDelayMs { get; set; }
    public ProcessingOptions() { }
}
