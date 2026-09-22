using System;
using System.Collections.Generic;

namespace Kargoyeri.Contracts.Dtos;

public sealed class ShipmentReportData
{
	public DateTime FromUtc { get; set; }

	public DateTime ToUtc { get; set; }

	public int TotalCount { get; set; }

	public Dictionary<string, int> ByStatus { get; set; } = new Dictionary<string, int>();


	public IReadOnlyCollection<ProviderReportRow> ByProvider { get; set; } = (IReadOnlyCollection<ProviderReportRow>)(object)Array.Empty<ProviderReportRow>();

}
