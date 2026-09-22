using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Kargoyeri.Studio.Core.Controllers.Api;

/// <summary>
/// REST API rotalarini ve kullanim orneklerini listeler.
/// GET /api-docs ile erisebilirsiniz.
/// Swashbuckle kurulabilirse bu controller kaldirilabilir.
/// </summary>
[AllowAnonymous]
[ApiController]
[Route("api-docs")]
[Produces("application/json")]
public sealed class ApiDocsController : ControllerBase
{
    [HttpGet]
    public IActionResult Index()
    {
        return Ok(new
        {
            title = "Kargoyeri Studio REST API",
            version = "v1",
            baseUrl = "/api/v1",
            auth = new
            {
                type = "ApiKey",
                header = "X-Api-Key",
                note = "appsettings StudioApi:ApiKey degerini gonderin. Development'ta bos birakilabilir."
            },
            endpoints = new object[]
            {
                new { method = "GET",  path = "/api/v1/shipments",                          description = "Gonderi listesi. ?workspace=kod&page=1&pageSize=50" },
                new { method = "GET",  path = "/api/v1/shipments/{ref}",                    description = "Tek gonderi detayi. ?workspace=kod" },
                new { method = "POST", path = "/api/v1/shipments",                          description = "Yeni gonderi olustur. Body: CreateShipmentRequest" },
                new { method = "POST", path = "/api/v1/shipments/{ref}/refresh",            description = "Gonderi durumunu yenile. ?workspace=kod" },
                new { method = "POST", path = "/api/v1/shipments/{ref}/cancel",             description = "Gonderiyi iptal et. ?workspace=kod  Body: {reason}" },
                new { method = "GET",  path = "/api/v1/shipments/{ref}/logs",               description = "Gonderi log kayitlari. ?workspace=kod" },
                new { method = "GET",  path = "/api/v1/providers",                          description = "Tum kargo firmalari ve yetenekleri" },
                new { method = "GET",  path = "/api/v1/providers/status",                   description = "Bir musteri icin provider hazirlik durumu. ?workspace=kod" },
                new { method = "GET",  path = "/health",                                    description = "Servis saglik kontrolu" },
                new { method = "GET",  path = "/api-docs",                                  description = "Bu dokuman" }
            },
            exampleCreateShipment = new
            {
                tenantKey = "musteri-kodu",
                source = "Manual",
                provider = "Sandbox",
                orderReference = "ORD-001",
                currencyCode = "TRY",
                sender = new { name = "Gonderici", city = "Istanbul", district = "Umraniye", addressLine1 = "Ornek Sok No:1", phone = "05550000000" },
                recipient = new { name = "Alici", city = "Ankara", district = "Cankaya", addressLine1 = "Ornek Cad No:2", phone = "05551111111" },
                packages = new[] { new { packageSequence = 1, weight = 1.0, desi = 1.0 } }
            }
        });
    }
}
