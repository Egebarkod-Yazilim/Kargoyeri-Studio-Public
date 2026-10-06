namespace Kargoyeri.Domain.ValueObjects;

public sealed class PackageInfo
{
	public int PackageSequence { get; set; } = 1;


	public decimal Weight { get; set; }

	public decimal Desi { get; set; }

	public decimal? Width { get; set; }

	public decimal? Height { get; set; }

	public decimal? Length { get; set; }

	public string? Description { get; set; }

	public decimal? CashOnDeliveryAmount { get; set; }
}
