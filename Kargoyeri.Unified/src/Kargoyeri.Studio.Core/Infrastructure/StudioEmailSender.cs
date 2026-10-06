using System.Net;
using System.Net.Mail;
using Kargoyeri.Application.Options;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Kargoyeri.Studio.Core.Infrastructure;

/// <summary>
/// Sistem-ici e-posta gonderimi (sifre sifirlama, yeni kullanici davet, lisans uyarisi).
/// Mevcut SmtpOptions'i kullanir; CompositeNotificationDispatcher ile ayni SMTP konfigine baglanir.
/// </summary>
public interface IStudioEmailSender
{
    Task<bool> SendAsync(string toAddress, string subject, string body, CancellationToken cancellationToken);
    bool IsConfigured { get; }
}

public sealed class StudioEmailSender : IStudioEmailSender
{
    private readonly SmtpOptions _smtp;
    private readonly ILogger<StudioEmailSender> _logger;

    public StudioEmailSender(IOptions<SmtpOptions> smtpOptions, ILogger<StudioEmailSender> logger)
    {
        _smtp   = smtpOptions.Value;
        _logger = logger;
    }

    public bool IsConfigured => _smtp.IsConfigured;

    public async Task<bool> SendAsync(string toAddress, string subject, string body, CancellationToken cancellationToken)
    {
        if (!_smtp.IsConfigured)
        {
            _logger.LogWarning("StudioEmailSender: SMTP yapilandirilmamis, mail gonderilemiyor. To={To} Subject={Subject}", toAddress, subject);
            return false;
        }

        if (string.IsNullOrWhiteSpace(toAddress))
        {
            _logger.LogWarning("StudioEmailSender: Hedef adres bos. Subject={Subject}", subject);
            return false;
        }

        try
        {
            var fromAddress = string.IsNullOrWhiteSpace(_smtp.FromAddress) ? _smtp.Username : _smtp.FromAddress;

            using var message = new MailMessage
            {
                From       = new MailAddress(fromAddress, _smtp.FromName),
                Subject    = subject,
                Body       = body,
                IsBodyHtml = false
            };
            message.To.Add(new MailAddress(toAddress));

            using var smtp = new SmtpClient(_smtp.Host, _smtp.Port)
            {
                EnableSsl      = _smtp.EnableSsl,
                Credentials    = new NetworkCredential(_smtp.Username, _smtp.Password),
                DeliveryMethod = SmtpDeliveryMethod.Network
            };

            await smtp.SendMailAsync(message, cancellationToken);
            _logger.LogInformation("StudioEmailSender: Mail iletildi. To={To} Subject={Subject}", toAddress, subject);
            return true;
        }
        catch (Exception ex)
        {
            _logger.LogError(ex, "StudioEmailSender: Mail gonderilemedi. To={To} Subject={Subject}", toAddress, subject);
            return false;
        }
    }
}
