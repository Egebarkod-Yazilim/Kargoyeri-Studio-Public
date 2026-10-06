namespace Kargoyeri.Application.Options;

public sealed class NetgsmOptions
{
	public string UserCode { get; set; } = string.Empty;


	public string Password { get; set; } = string.Empty;


	public string Header { get; set; } = "KARGOYERI";


	public bool IsConfigured => !string.IsNullOrWhiteSpace(UserCode) && !string.IsNullOrWhiteSpace(Password);
}
