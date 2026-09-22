namespace Kargoyeri.Domain.Enums;

public static class OrderSourceChannelCodes
{
	public static string GetPrefix(OrderSourceChannel? channel)
	{
		if (1 == 0)
		{
		}
		string result = channel switch
		{
			OrderSourceChannel.Trendyol => "TY", 
			OrderSourceChannel.Hepsiburada => "HB", 
			OrderSourceChannel.N11 => "N11", 
			OrderSourceChannel.CicekSepeti => "CS", 
			OrderSourceChannel.GittiGidiyor => "GG", 
			OrderSourceChannel.Pazarama => "PZ", 
			OrderSourceChannel.Amazon => "AMZ", 
			OrderSourceChannel.PttAvm => "PTT", 
			OrderSourceChannel.Modanisa => "MD", 
			OrderSourceChannel.NopCommerce => "ET-NOP", 
			OrderSourceChannel.Shopify => "ET-SHP", 
			OrderSourceChannel.WooCommerce => "ET-WOO", 
			OrderSourceChannel.Ticimax => "ET-TCX", 
			OrderSourceChannel.IdeaSoft => "ET-IDS", 
			OrderSourceChannel.EmbeddedShop => "ET-EMB", 
			OrderSourceChannel.Manual => "MN", 
			OrderSourceChannel.Custom => "API", 
			_ => "API", 
		};
		if (1 == 0)
		{
		}
		return result;
	}

	public static string? GetCode(OrderSourceChannel? channel)
	{
		if (!channel.HasValue || channel == OrderSourceChannel.Unknown)
		{
			return null;
		}
		return GetPrefix(channel).ToLowerInvariant();
	}

	public static OrderSourceChannel? FromCodeOrPrefix(string? codeOrPrefix)
	{
		if (string.IsNullOrWhiteSpace(codeOrPrefix))
		{
			return null;
		}
		string text = codeOrPrefix.Trim().ToUpperInvariant();
		if (1 == 0)
		{
		}
		OrderSourceChannel? result = text switch
		{
			"TY" => OrderSourceChannel.Trendyol, 
			"HB" => OrderSourceChannel.Hepsiburada, 
			"N11" => OrderSourceChannel.N11, 
			"CS" => OrderSourceChannel.CicekSepeti, 
			"GG" => OrderSourceChannel.GittiGidiyor, 
			"PZ" => OrderSourceChannel.Pazarama, 
			"AMZ" => OrderSourceChannel.Amazon, 
			"PTT" => OrderSourceChannel.PttAvm, 
			"MD" => OrderSourceChannel.Modanisa, 
			"ET-NOP" => OrderSourceChannel.NopCommerce, 
			"ET-SHP" => OrderSourceChannel.Shopify, 
			"ET-WOO" => OrderSourceChannel.WooCommerce, 
			"ET-TCX" => OrderSourceChannel.Ticimax, 
			"ET-IDS" => OrderSourceChannel.IdeaSoft, 
			"ET-EMB" => OrderSourceChannel.EmbeddedShop, 
			"MN" => OrderSourceChannel.Manual, 
			"API" => OrderSourceChannel.Custom, 
			_ => null, 
		};
		if (1 == 0)
		{
		}
		return result;
	}
}
