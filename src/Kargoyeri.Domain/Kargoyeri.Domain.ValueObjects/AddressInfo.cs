namespace Kargoyeri.Domain.ValueObjects;

public sealed class AddressInfo
{
	public string Name { get; set; } = string.Empty;


	public string? CompanyName { get; set; }

	public string? Phone { get; set; }

	public string? Email { get; set; }

	public string CountryCode { get; set; } = "TR";


	public string City { get; set; } = string.Empty;


	public string? District { get; set; }

	public string? PostalCode { get; set; }

	public string AddressLine1 { get; set; } = string.Empty;


	public string? AddressLine2 { get; set; }
}
