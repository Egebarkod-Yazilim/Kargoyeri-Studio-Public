using System;

namespace Kargoyeri.Contracts.Dtos;

public sealed class ProviderReportRow
{
	public string Provider { get; set; } = string.Empty;


	public int Total { get; set; }

	public int Delivered { get; set; }

	public int Failed { get; set; }

	public int InTransit { get; set; }

	public int Pending { get; set; }

	public double SuccessRate => (Total == 0) ? 0.0 : Math.Round((double)Delivered / (double)Total * 100.0, 1);
}
