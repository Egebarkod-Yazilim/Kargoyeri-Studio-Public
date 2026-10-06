using System.Collections.Generic;

namespace Kargoyeri.Contracts.Dtos;

public sealed class QuickStartGuideDto
{
	public string Title { get; set; } = string.Empty;


	public string Summary { get; set; } = string.Empty;


	public List<string> Steps { get; set; } = new List<string>();


	public Dictionary<string, string> RequiredHeaders { get; set; } = new Dictionary<string, string>();


	public Dictionary<string, string> SuggestedOrder { get; set; } = new Dictionary<string, string>();


	public List<string> Tips { get; set; } = new List<string>();

}
