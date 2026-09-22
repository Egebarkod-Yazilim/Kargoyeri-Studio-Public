// Generated from assembly metadata during repository recovery.
#nullable enable
namespace Kargoyeri.Application.Options;

public sealed partial class SmtpOptions
{
    public string Host { get; set; }
    public int Port { get; set; }
    public bool EnableSsl { get; set; }
    public string Username { get; set; }
    public string Password { get; set; }
    public string FromAddress { get; set; }
    public string FromName { get; set; }
    public bool IsConfigured { get; }
    public SmtpOptions() { }
}
