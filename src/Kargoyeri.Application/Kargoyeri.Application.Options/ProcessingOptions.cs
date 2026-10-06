namespace Kargoyeri.Application.Options;

public sealed class ProcessingOptions
{
	public int RefreshIntervalSeconds { get; set; } = 30;


	public int MaxRefreshBatchSize { get; set; } = 50;


	public int MaxRetryCount { get; set; } = 10;


	public int ProviderTimeoutSeconds { get; set; } = 30;


	public int BatchItemDelayMs { get; set; } = 200;

}
