using System;
using System.Net;
using System.Net.Http;
using System.Threading;
using System.Threading.Tasks;

namespace Kargoyeri.Infrastructure.Providers;

internal sealed class WebhookRetryHandler : DelegatingHandler
{
	private readonly int _maxRetries;

	public WebhookRetryHandler(int maxRetries = 3)
	{
		_maxRetries = Math.Max(0, maxRetries);
	}

	protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
	{
		HttpResponseMessage response = null;
		Exception lastError = null;
		TimeSpan retryAfter = default(TimeSpan);
		for (int attempt = 0; attempt <= _maxRetries; attempt++)
		{
			if (attempt > 0)
			{
				int baseDelayMs = (int)Math.Pow(2.0, attempt - 1) * 500;
				int jitter = Random.Shared.Next(0, 250);
				int delayMs = baseDelayMs + jitter;
				HttpResponseMessage httpResponseMessage = response;
				int num;
				if (httpResponseMessage != null && httpResponseMessage.StatusCode == HttpStatusCode.TooManyRequests)
				{
					TimeSpan? timeSpan = response.Headers.RetryAfter?.Delta;
					if (timeSpan.HasValue)
					{
						retryAfter = timeSpan.GetValueOrDefault();
						num = 1;
					}
					else
					{
						num = 0;
					}
				}
				else
				{
					num = 0;
				}
				if (num != 0)
				{
					delayMs = Math.Max(delayMs, (int)retryAfter.TotalMilliseconds);
				}
				response?.Dispose();
				response = null;
				await Task.Delay(delayMs, cancellationToken);
			}
			try
			{
				response = await base.SendAsync(request, cancellationToken);
				if (!ShouldRetry(response))
				{
					return response;
				}
			}
			catch (HttpRequestException ex)
			{
				lastError = ex;
			}
			catch (TaskCanceledException ex2) when (!cancellationToken.IsCancellationRequested)
			{
				lastError = ex2;
			}
		}
		if (response != null)
		{
			return response;
		}
		throw lastError ?? new HttpRequestException("Webhook retry limit asildi.");
	}

	private static bool ShouldRetry(HttpResponseMessage response)
	{
		int statusCode = (int)response.StatusCode;
		if (statusCode >= 500)
		{
			return true;
		}
		if (response.StatusCode == HttpStatusCode.RequestTimeout)
		{
			return true;
		}
		if (response.StatusCode == HttpStatusCode.TooManyRequests)
		{
			return true;
		}
		return false;
	}
}
