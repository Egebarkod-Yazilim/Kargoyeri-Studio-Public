using Kargoyeri.Studio.Core.Infrastructure;
using Kargoyeri.Studio.Core.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Kargoyeri.Studio.Core.Controllers;

/// <summary>
/// CSV ile toplu gonderi yukleme (P1-#9). Job CsvImportJobService uzerinde
/// arka planda calistirilir, sayfa progress + sonuclari gosterir.
/// </summary>
[Authorize(Policy = StudioRoles.CanWrite)]
[Route("bulk")]
public sealed class BulkShipmentController : Controller
{
    private const long MaxFileBytes = 5 * 1024 * 1024; // 5 MB

    private readonly CsvImportJobService _jobs;
    private readonly IStudioWorkspaceContext _workspace;
    private readonly ILogger<BulkShipmentController> _logger;

    public BulkShipmentController(
        CsvImportJobService jobs,
        IStudioWorkspaceContext workspace,
        ILogger<BulkShipmentController> logger)
    {
        _jobs      = jobs;
        _workspace = workspace;
        _logger    = logger;
    }

    [HttpGet("")]
    public IActionResult Index()
    {
        var tenantKey = _workspace.GetCurrentCode();
        return View(new BulkShipmentPageViewModel
        {
            TenantKey = tenantKey,
            Jobs      = _jobs.ListByTenant(tenantKey)
        });
    }

    [HttpPost("upload")]
    [ValidateAntiForgeryToken]
    [RequestSizeLimit(6 * 1024 * 1024)]
    public async Task<IActionResult> Upload(IFormFile? file, CancellationToken ct)
    {
        if (file is null || file.Length == 0)
        {
            StudioFlash.Error(TempData, "Lutfen bir CSV dosyasi secin.");
            return RedirectToAction(nameof(Index));
        }

        if (file.Length > MaxFileBytes)
        {
            StudioFlash.Error(TempData, $"Dosya cok buyuk. Maks {MaxFileBytes / (1024 * 1024)} MB.");
            return RedirectToAction(nameof(Index));
        }

        if (!file.FileName.EndsWith(".csv", StringComparison.OrdinalIgnoreCase))
        {
            StudioFlash.Error(TempData, "Sadece .csv dosyalari kabul edilir.");
            return RedirectToAction(nameof(Index));
        }

        string content;
        using (var reader = new StreamReader(file.OpenReadStream()))
            content = await reader.ReadToEndAsync(ct);

        var tenantKey = _workspace.GetCurrentCode();
        var job = _jobs.Enqueue(tenantKey, file.FileName, content);

        _logger.LogInformation("CSV import enqueued: tenant={Tenant} file={File} jobId={Id}",
            tenantKey, file.FileName, job.Id);

        StudioFlash.Success(TempData, $"Dosya kuyruga alindi (Job #{job.Id.ToString()[..8]}). Asagidan ilerlemeyi izleyebilirsiniz.");
        return RedirectToAction(nameof(Index));
    }

    [HttpGet("status/{id:guid}")]
    public IActionResult Status(Guid id)
    {
        var job = _jobs.Get(id);
        if (job is null) return NotFound();
        return Json(new
        {
            id            = job.Id,
            state         = job.State.ToString(),
            total         = job.TotalRows,
            success       = job.SuccessCount,
            failed        = job.FailedCount,
            progress      = job.ProgressPercent,
            errors        = job.Errors.Take(20),
            fatal         = job.FatalError,
            enqueuedAtUtc = job.EnqueuedAtUtc,
            startedAtUtc  = job.StartedAtUtc,
            completedAtUtc = job.CompletedAtUtc
        });
    }
}
