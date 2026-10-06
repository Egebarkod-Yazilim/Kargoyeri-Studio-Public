namespace Kargoyeri.Application.Options;

public sealed class SmtpOptions
{
	public string Host { get; set; } = string.Empty;


	public int Port { get; set; } = 587;


	public bool EnableSsl { get; set; } = true;


	public string Username { get; set; } = string.Empty;


	public string Password { get; set; } = string.Empty;


	public string FromAddress { get; set; } = string.Empty;


	public string FromName { get; set; } = "Kargoyeri Studio";


	public bool IsConfigured => !string.IsNullOrWhiteSpace(Host) && !string.IsNullOrWhiteSpace(Username) && !string.IsNullOrWhiteSpace(Password);
}
