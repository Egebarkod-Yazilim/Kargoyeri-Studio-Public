using System.Collections.Generic;
using Kargoyeri.Contracts.Dtos;
using Kargoyeri.Contracts.Enums;

namespace Kargoyeri.Application.Services;

public sealed class ApiDocumentationService
{
	public QuickStartGuideDto BuildQuickStart()
	{
		return new QuickStartGuideDto
		{
			Title = "Kargoyeri Hızlı Başlangıç",
			Summary = "Bu servis ile bir müşteri kodu kullanarak kargo ayarı yapabilir, kargo oluşturabilir, log ve bildirimleri takip edebilirsin.",
			Steps = new List<string> { "Önce Swagger ekranını aç: /swagger", "Hangi kargo firmalarının hazır olduğunu görmek için /api/kargo/providers endpointine bak.", "Kullanacağın firmaya ait ayar alanlarını görmek için /api/kargo/{firma}/blueprint endpointini aç.", "Aynı firmaya ait kullanıcı adı, şifre, api key gibi bilgileri /api/kargo/{firma}/settings ile kaydet.", "Gerçek gönderimden önce /api/kargo/{firma}/preview/create ile örnek isteğin nasıl oluştuğunu kontrol et.", "Her şey doğruysa /api/kargo/{firma}/shipments veya /api/kargo/shipments ile kargoyu oluştur.", "Durum, log ve bildirimleri shipment ve notifications endpointlerinden takip et." },
			RequiredHeaders = new Dictionary<string, string>
			{
				["X-Customer-Code"] = "Zorunlu. Her müşteriyi ayıran kısa kod. Örnek: acme-marketplace",
				["X-Customer-Name"] = "İsteğe bağlı. Ekranlarda görünecek müşteri adı",
				["X-Notification-Webhook"] = "İsteğe bağlı. Bildirimleri bir URL'ye göndermek için kullanılır",
				["X-Notification-Email"] = "İsteğe bağlı. E-posta hedefi saklanır",
				["X-Notification-Phone"] = "İsteğe bağlı. Telefon hedefi saklanır"
			},
			SuggestedOrder = new Dictionary<string, string>
			{
				["1"] = "GET /api/kargo/providers",
				["2"] = "GET /api/kargo/{firma}/blueprint",
				["3"] = "PUT /api/kargo/{firma}/settings",
				["4"] = "POST /api/kargo/{firma}/preview/create",
				["5"] = "POST /api/kargo/{firma}/shipments",
				["6"] = "GET /api/kargo/shipments",
				["7"] = "GET /api/kargo/shipments/{shipmentReference}/logs",
				["8"] = "GET /api/kargo/notifications"
			},
			Tips = new List<string> { "İlk istek atıldığında müşteri kaydı otomatik oluşabilir.", "Body içine customer code yazılmaz; header yeterlidir.", "Aynı müşteri kodu ile atılan isteklerin log ve bildirimleri birlikte tutulur.", "Gerçek canlı taşıma şu an sadece Sandbox için tam bağlıdır." }
		};
	}

	public ApiIntegrationGuideDto BuildGuide()
	{
		return new ApiIntegrationGuideDto
		{
			ServiceName = "Kargoyeri",
			SwaggerUrl = "/swagger",
			OpenApiUrl = "/swagger/v1/swagger.json",
			HealthUrl = "/health",
			InfoUrl = "/info",
			HeaderName = "X-Customer-Code",
			AuthenticationModel = "Dogrudan customer code ile cagri modeli kullanilir. API key ve admin onboarding zorunlu degildir.",
			TenantResolutionModel = "Dis dunya sadece X-Customer-Code gonderir. Iceride loglar, bildirimler ve provider ayarlari bu koda gore ayrilir.",
			TenantKeyRules = new List<string> { "Customer code musteriye ait kalici ve sade bir kod olmalidir.", "Ornek: acme, acme-nop, acme-marketplace.", "Ilk istekte sistem bu kod icin kaydi otomatik olusturabilir." },
			OnboardingSteps = new List<string> { "Isteklerde X-Customer-Code header'i gonderilir.", "Istenirse X-Customer-Name header'i ile gorunen musteri adi set edilir.", "Webhook, e-posta veya telefon header'lari ile bildirim hedefleri tanimlanabilir.", "Provider bazli credential ve ayarlar ilgili settings endpointleri uzerinden girilir." },
			Notes = new List<string> { "CreateShipment body'sine tenantKey yazilmaz; sistem onu header'dan doldurur.", "Provider secimi body veya provider bazli route ile yapilabilir.", "Provider detaylari icin /docs/providers ve /api/kargo/{provider}/blueprint kullanilabilir.", "Log ve bildirim kayitlari customer code bazinda ayrilir.", "Canli transport bagli olmayan providerlarda AdditionalSettings.simulationMode=true ile uctan uca test yapilabilir." },
			SampleHeaders = new Dictionary<string, string>
			{
				["X-Customer-Code"] = "acme-marketplace",
				["X-Customer-Name"] = "Acme Marketplace",
				["X-Notification-Webhook"] = "https://example.com/webhooks/kargo",
				["Content-Type"] = "application/json"
			},
			ProviderRoutes = new Dictionary<string, string>
			{
				["CreateShipmentCommon"] = "POST /api/kargo/shipments",
				["CreateShipmentPerProvider"] = "POST /api/kargo/{provider}/shipments",
				["ProviderStatus"] = "GET /api/kargo/{provider}/status",
				["ProviderBlueprint"] = "GET /api/kargo/{provider}/blueprint",
				["ProviderPreview"] = "POST /api/kargo/{provider}/preview/create",
				["ProviderSettings"] = "GET|PUT /api/kargo/{provider}/settings",
				["Notifications"] = "GET /api/kargo/notifications"
			},
			SampleCreateShipmentRequest = new CreateShipmentRequest
			{
				Source = IntegrationSourceTypeDto.Api,
				Provider = CargoProviderTypeDto.Mng,
				OrderReference = "ORDER-1001",
				ClientShipmentReference = "CLIENT-SHP-1001",
				IdempotencyKey = "ORDER-1001-CREATE",
				CollectionAmount = default(decimal),
				CurrencyCode = "TRY",
				Sender = new AddressDto
				{
					Name = "Acme Depo",
					CompanyName = "Acme Ticaret",
					Phone = "05551234567",
					Email = "depo@acme.local",
					CountryCode = "TR",
					City = "Istanbul",
					District = "Umraniye",
					PostalCode = "34760",
					AddressLine1 = "Ornek Mahallesi No:1"
				},
				Recipient = new AddressDto
				{
					Name = "Ali Veli",
					Phone = "05559876543",
					Email = "ali@example.com",
					CountryCode = "TR",
					City = "Ankara",
					District = "Cankaya",
					PostalCode = "06680",
					AddressLine1 = "Deneme Sokak No:10"
				},
				Packages = new List<PackageDto>
				{
					new PackageDto
					{
						PackageSequence = 1,
						Weight = 1m,
						Desi = 1m,
						Description = "Tek paket"
					}
				},
				Metadata = new Dictionary<string, string>
				{
					["payment.type"] = "Prepaid",
					["shipping.payor"] = "sender"
				}
			}
		};
	}
}
