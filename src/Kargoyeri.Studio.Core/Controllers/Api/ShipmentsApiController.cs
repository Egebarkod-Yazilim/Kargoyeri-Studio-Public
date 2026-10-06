using Kargoyeri.Application.Services;
using Kargoyeri.Contracts.Dtos;
using Kargoyeri.Studio.Core.Infrastructure;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.RateLimiting;

namespace Kargoyeri.Studio.Core.Controllers.Api;

/// <summary>
/// Dis sistemlerin gonderi verisi gondermesi ve sorgulamasi icin REST API.
/// Swagger/OpenAPI desteği icin Swashbuckle eklenebilir.
/// </summary>
[ApiController]
[Route("api/v1/shipments")]
[Produces("application/json")]
[AllowAnonymous] // Auth icin ApiKey middleware veya JWT eklenebilir
[EnableRateLimiting("api-tenant")] // P2-#1 — tenant basina 1000 req/dk
public sealed class ShipmentsApiController : ControllerBase
{
    private readonly CargoOrchestrator _orchestrator;
    private readonly CustomerService _customerService;
    private readonly IStudioWorkspaceContext _workspaceContext;

    public ShipmentsApiController(
        CargoOrchestrator orchestrator,
        CustomerService customerService,
        IStudioWorkspaceContext workspaceContext)
    {
        _orchestrator = orchestrator;
        _customerService = customerService;
        _workspaceContext = workspaceContext;
    }

    /// <summary>Workspace'e ait gonderi listesini dondurur (sayfalanmis).</summary>
    /// <param name="workspace">Tenant kimligi (zorunlu).</param>
    /// <param name="page">Sayfa numarasi (1'den baslar).</param>
    /// <param name="pageSize">Sayfa basina kayit (1-100).</param>
    [HttpGet]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> List(
        [FromQuery] string workspace,
        [FromQuery] int page = 1,
        [FromQuery] int pageSize = 50,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(workspace))
            return BadRequest(new { error = "workspace parametresi zorunludur." });

        var all = await _orchestrator.ListShipmentsAsync(workspace, cancellationToken);
        var safePage = Math.Max(1, page);
        var safeSize = Math.Clamp(pageSize, 1, 100);
        var paged = all.Skip((safePage - 1) * safeSize).Take(safeSize).ToArray();

        return Ok(new
        {
            page = safePage,
            pageSize = safeSize,
            totalCount = all.Count,
            items = paged
        });
    }

    /// <summary>Tek gonderi detayini dondurur.</summary>
    [HttpGet("{shipmentReference}")]
    [ProducesResponseType(typeof(ShipmentDetailResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    [ProducesResponseType(StatusCodes.Status404NotFound)]
    public async Task<IActionResult> Get(
        string shipmentReference,
        [FromQuery] string workspace,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(workspace))
            return BadRequest(new { error = "workspace parametresi zorunludur." });

        var shipment = await _orchestrator.GetShipmentAsync(shipmentReference, workspace, cancellationToken);
        if (shipment is null)
            return NotFound(new { error = $"{shipmentReference} bulunamadi." });

        return Ok(shipment);
    }

    /// <summary>Gonderi durumunu kargo firmasindan yeniler.</summary>
    [HttpPost("{shipmentReference}/refresh")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Refresh(
        string shipmentReference,
        [FromQuery] string workspace,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(workspace))
            return BadRequest(new { error = "workspace parametresi zorunludur." });

        var result = await _orchestrator.RefreshShipmentAsync(shipmentReference, workspace, cancellationToken);
        return Ok(new { shipmentReference = result.ShipmentReference, status = result.Status, trackingNumber = result.TrackingNumber });
    }

    /// <summary>Gonderiyi iptal eder. Iptal sebebi (Reason) en az 5 karakter olmalidir.</summary>
    [HttpPost("{shipmentReference}/cancel")]
    [ProducesResponseType(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Cancel(
        string shipmentReference,
        [FromQuery] string workspace,
        [FromBody] CancelApiRequest? body,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(workspace))
            return BadRequest(new { error = "workspace parametresi zorunludur." });

        var result = await _orchestrator.CancelShipmentAsync(
            shipmentReference, workspace, body?.Reason, cancellationToken);

        return Ok(new { shipmentReference = result.ShipmentReference, status = result.Status, message = result.Message });
    }

    /// <summary>Dis sistemden gelen siparis bilgisi ile yeni kargo gonderisi olusturur.</summary>
    /// <param name="request">Gonderici/alici adresi, paket bilgisi vb. icerir.</param>
    [HttpPost]
    [ProducesResponseType(typeof(CreateShipmentResponse), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Create(
        [FromBody] CreateShipmentRequest request,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(request?.TenantKey))
            return BadRequest(new { error = "TenantKey zorunludur." });

        // P0-#6: dis sistem "kargoyeri.serviceTier" anahtarini gonderirse
        // (orn. "Express"), bunu provider-spesifik metadata key'ine ceviriyoruz.
        // Manuel "ups.serviceCode" vb. gonderilmisse o korunur (override etmez).
        if (request.Metadata is not null &&
            request.Metadata.TryGetValue("kargoyeri.serviceTier", out var tierRaw) &&
            Enum.TryParse<KargoyeriServiceTier>(tierRaw, ignoreCase: true, out var tier))
        {
            var mapping = ServiceTypeCatalog.Resolve(request.Provider, tier);
            if (mapping is not null && !request.Metadata.ContainsKey(mapping.MetadataKey))
            {
                request.Metadata[mapping.MetadataKey] = mapping.MetadataValue;
            }
        }

        try
        {
            var result = await _orchestrator.CreateShipmentAsync(request, cancellationToken);
            return Ok(new
            {
                shipmentReference = result.ShipmentReference,
                trackingNumber = result.TrackingNumber,
                status = result.Status,
                message = result.Message
            });
        }
        catch (InvalidOperationException ex)
        {
            return BadRequest(new { error = ex.Message });
        }
    }

    /// <summary>Gonderiye ait operasyon log kayitlarini dondurur (provider istek/yanit ozetleri).</summary>
    [HttpGet("{shipmentReference}/logs")]
    [ProducesResponseType(typeof(IEnumerable<ShipmentOperationLogDto>), StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status400BadRequest)]
    public async Task<IActionResult> Logs(
        string shipmentReference,
        [FromQuery] string workspace,
        CancellationToken cancellationToken = default)
    {
        if (string.IsNullOrWhiteSpace(workspace))
            return BadRequest(new { error = "workspace parametresi zorunludur." });

        var logs = await _orchestrator.ListLogsAsync(shipmentReference, workspace, cancellationToken);
        return Ok(logs);
    }

    public sealed record CancelApiRequest(string? Reason);
}
