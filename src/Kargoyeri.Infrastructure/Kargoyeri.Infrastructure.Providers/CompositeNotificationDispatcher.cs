using System;
using System.Linq;
using System.Net;
using System.Net.Http;
using System.Net.Mail;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;
using System.Web;
using Kargoyeri.Application.Abstractions.Providers;
using Kargoyeri.Application.Options;
using Kargoyeri.Domain.Entities;
using Kargoyeri.Domain.Enums;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Kargoyeri.Infrastructure.Providers;

internal sealed class CompositeNotificationDispatcher : INotificationDispatcher
{
	private readonly IHttpClientFactory _httpClientFactory;

	private readonly SmtpOptions _smtp;

	private readonly NetgsmOptions _netgsm;

	private readonly ILogger<CompositeNotificationDispatcher> _logger;

	public CompositeNotificationDispatcher(IHttpClientFactory httpClientFactory, IOptions<SmtpOptions> smtpOptions, IOptions<NetgsmOptions> netgsmOptions, ILogger<CompositeNotificationDispatcher> logger)
	{
		_httpClientFactory = httpClientFactory;
		_smtp = smtpOptions.Value;
		_netgsm = netgsmOptions.Value;
		_logger = logger;
	}

	public async Task DispatchAsync(CustomerTenant customer, NotificationMessage notification, CancellationToken cancellationToken)
	{
		switch (notification.Channel)
		{
		case NotificationChannel.Internal:
			_logger.LogInformation("Tenant {TenantKey} internal notification {EventType} for shipment {ShipmentReference}: {Message}", customer.TenantKey, notification.EventType, notification.ShipmentReference, notification.Body);
			notification.MarkDelivered();
			break;
		case NotificationChannel.Webhook:
			await DispatchWebhookAsync(customer, notification, cancellationToken);
			break;
		case NotificationChannel.Email:
			await DispatchEmailAsync(customer, notification, cancellationToken);
			break;
		case NotificationChannel.Sms:
			await DispatchSmsAsync(customer, notification, cancellationToken);
			break;
		default:
			_logger.LogWarning("Tenant {TenantKey} bilinmeyen bildirim kanalı {Channel}. Shipment: {ShipmentReference}", customer.TenantKey, notification.Channel, notification.ShipmentReference);
			notification.MarkIgnored("Bilinmeyen bildirim kanalı.");
			break;
		}
	}

	private async Task DispatchWebhookAsync(CustomerTenant customer, NotificationMessage notification, CancellationToken cancellationToken)
	{
		try
		{
			var payload = new
			{
				tenantKey = notification.TenantKey,
				shipmentReference = notification.ShipmentReference,
				eventType = notification.EventType.ToString(),
				subject = notification.Subject,
				body = notification.Body,
				createdAtUtc = notification.CreatedAtUtc
			};
			string payloadJson = JsonSerializer.Serialize(payload);
			byte[] payloadBytes = Encoding.UTF8.GetBytes(payloadJson);
			HttpClient client = _httpClientFactory.CreateClient("CompositeNotificationDispatcher");
			using HttpRequestMessage request = new HttpRequestMessage(HttpMethod.Post, notification.Address)
			{
				Content = new StringContent(payloadJson, Encoding.UTF8, "application/json")
			};
			if (customer.Metadata.TryGetValue("webhook.secret", out string secret) && !string.IsNullOrWhiteSpace(secret))
			{
				byte[] keyBytes = Encoding.UTF8.GetBytes(secret);
				byte[] hash = HMACSHA256.HashData(keyBytes, payloadBytes);
				string hexSignature = Convert.ToHexString(hash).ToLowerInvariant();
				request.Headers.Add("X-Kargoyeri-Signature", "sha256=" + hexSignature);
			}
			HttpResponseMessage response = await client.SendAsync(request, cancellationToken);
			if (response.IsSuccessStatusCode)
			{
				_logger.LogInformation("Webhook bildirimi iletildi. Tenant: {TenantKey} Shipment: {ShipmentReference} -> {Address}", customer.TenantKey, notification.ShipmentReference, notification.Address);
				notification.MarkDelivered();
			}
			else
			{
				_logger.LogWarning("Webhook bildirimi basarisiz. Tenant: {TenantKey} Shipment: {ShipmentReference}. HTTP {StatusCode}", customer.TenantKey, notification.ShipmentReference, (int)response.StatusCode);
				notification.MarkFailed($"Webhook HTTP {response.StatusCode}");
			}
		}
		catch (Exception ex2)
		{
			Exception ex = ex2;
			_logger.LogError(ex, "Webhook bildirimi exception. Tenant: {TenantKey} Shipment: {ShipmentReference}", customer.TenantKey, notification.ShipmentReference);
			notification.MarkFailed(ex.Message);
		}
	}

	private async Task DispatchEmailAsync(CustomerTenant customer, NotificationMessage notification, CancellationToken cancellationToken)
	{
		if (!_smtp.IsConfigured)
		{
			_logger.LogWarning("Email bildirimi yapilandirilmamis (SMTP ayari eksik). Tenant: {TenantKey} Shipment: {ShipmentReference}", customer.TenantKey, notification.ShipmentReference);
			notification.MarkIgnored("SMTP yapılandırması eksik.");
			return;
		}
		if (string.IsNullOrWhiteSpace(notification.Address))
		{
			notification.MarkIgnored("Hedef email adresi boş.");
			return;
		}
		try
		{
			string fromAddress = (string.IsNullOrWhiteSpace(_smtp.FromAddress) ? _smtp.Username : _smtp.FromAddress);
			using MailMessage message = new MailMessage
			{
				From = new MailAddress(fromAddress, _smtp.FromName),
				Subject = notification.Subject,
				Body = notification.Body,
				IsBodyHtml = false
			};
			message.To.Add(new MailAddress(notification.Address));
			using SmtpClient smtp = new SmtpClient(_smtp.Host, _smtp.Port)
			{
				EnableSsl = _smtp.EnableSsl,
				Credentials = new NetworkCredential(_smtp.Username, _smtp.Password),
				DeliveryMethod = SmtpDeliveryMethod.Network
			};
			await smtp.SendMailAsync(message, cancellationToken);
			_logger.LogInformation("Email bildirimi iletildi. Tenant: {TenantKey} Shipment: {ShipmentReference} -> {Address}", customer.TenantKey, notification.ShipmentReference, notification.Address);
			notification.MarkDelivered();
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "Email bildirimi gonderilemedi. Tenant: {TenantKey} Shipment: {ShipmentReference} -> {Address}", customer.TenantKey, notification.ShipmentReference, notification.Address);
			notification.MarkFailed("SMTP hatası: " + ex.Message);
		}
	}

	private async Task DispatchSmsAsync(CustomerTenant customer, NotificationMessage notification, CancellationToken cancellationToken)
	{
		if (!_netgsm.IsConfigured)
		{
			_logger.LogWarning("SMS bildirimi yapilandirilmamis (Netgsm ayari eksik). Tenant: {TenantKey} Shipment: {ShipmentReference}", customer.TenantKey, notification.ShipmentReference);
			notification.MarkIgnored("Netgsm yapılandırması eksik.");
			return;
		}
		if (string.IsNullOrWhiteSpace(notification.Address))
		{
			notification.MarkIgnored("Hedef telefon numarası boş.");
			return;
		}
		string phone = new string(notification.Address.Where(char.IsDigit).ToArray());
		if (phone.StartsWith("0"))
		{
			string text = phone;
			phone = text.Substring(1, text.Length - 1);
		}
		string message = ((notification.Body.Length > 155) ? (notification.Body.Substring(0, 155) + "...") : notification.Body);
		try
		{
			HttpClient client = _httpClientFactory.CreateClient("CompositeNotificationDispatcher");
			string url = $"https://api.netgsm.com.tr/sms/send/get/?usercode={HttpUtility.UrlEncode(_netgsm.UserCode)}&password={HttpUtility.UrlEncode(_netgsm.Password)}&gsmno={phone}&message={HttpUtility.UrlEncode(message)}&msgheader={HttpUtility.UrlEncode(_netgsm.Header)}&encoding=TR";
			HttpResponseMessage response = await client.GetAsync(url, cancellationToken);
			string responseBody = await response.Content.ReadAsStringAsync(cancellationToken);
			if (response.IsSuccessStatusCode && (responseBody.StartsWith("00") || responseBody.StartsWith("01")))
			{
				_logger.LogInformation("SMS bildirimi iletildi. Tenant: {TenantKey} Shipment: {ShipmentReference} -> {Phone}", customer.TenantKey, notification.ShipmentReference, phone);
				notification.MarkDelivered();
				return;
			}
			string errorCode = responseBody.Trim();
			if (1 == 0)
			{
			}
			string text = errorCode switch
			{
				"20" => "Mesaj metni boş", 
				"21" => "Alıcı numarası hatalı", 
				"22" => "Mesaj başlığı (header) yetkisiz", 
				"30" => "Geçersiz kullanıcı adı/şifre", 
				"40" => "Mesaj gönderim limiti aşıldı", 
				"70" => "Hatalı sorgulama", 
				_ => "Netgsm kod: " + errorCode, 
			};
			if (1 == 0)
			{
			}
			string errorMsg = text;
			_logger.LogWarning("SMS bildirimi basarisiz. Tenant: {TenantKey} Shipment: {ShipmentReference}. Hata: {Error}", customer.TenantKey, notification.ShipmentReference, errorMsg);
			notification.MarkFailed("SMS hatası: " + errorMsg);
		}
		catch (Exception ex)
		{
			_logger.LogError(ex, "SMS bildirimi gonderilemedi. Tenant: {TenantKey} Shipment: {ShipmentReference} -> {Phone}", customer.TenantKey, notification.ShipmentReference, phone);
			notification.MarkFailed("SMS exception: " + ex.Message);
		}
	}
}
