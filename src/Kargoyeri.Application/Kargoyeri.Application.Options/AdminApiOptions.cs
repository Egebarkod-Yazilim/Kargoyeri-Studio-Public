namespace Kargoyeri.Application.Options;

public sealed class AdminApiOptions
{
	public bool Enabled { get; set; } = true;


	public string AdminKey { get; set; } = "kargoyeri-admin-dev-key";

}
