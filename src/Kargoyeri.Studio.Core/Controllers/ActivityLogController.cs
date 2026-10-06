using Kargoyeri.Studio.Core.Infrastructure;
using Kargoyeri.Studio.Core.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;

namespace Kargoyeri.Studio.Core.Controllers;

[Authorize(Roles = StudioRoles.Admin)]
public sealed class ActivityLogController : Controller
{
    private readonly IStudioActivityLogService _logService;
    private const int PageSize = 50;

    public ActivityLogController(IStudioActivityLogService logService)
    {
        _logService = logService;
    }

    [HttpGet]
    public IActionResult Index(
        [FromQuery] string? tenantKey,
        [FromQuery] string? username,
        [FromQuery] string? controller,
        [FromQuery] string? httpMethod,
        [FromQuery] DateTime? fromDate,
        [FromQuery] DateTime? toDate,
        [FromQuery] bool errorsOnly = false,
        [FromQuery] int page = 1)
    {
        // Hafizadaki tum kayitlari al, sonra client-side filtrele.
        var all = _logService.GetRecent(tenantKey, username, max: 5000);

        IEnumerable<StudioActivityLogEntry> q = all;

        if (!string.IsNullOrWhiteSpace(controller))
            q = q.Where(e => string.Equals(e.Controller, controller, StringComparison.OrdinalIgnoreCase));

        if (!string.IsNullOrWhiteSpace(httpMethod))
            q = q.Where(e => string.Equals(e.HttpMethod, httpMethod, StringComparison.OrdinalIgnoreCase));

        if (fromDate.HasValue)
        {
            var from = DateTime.SpecifyKind(fromDate.Value.Date, DateTimeKind.Local).ToUniversalTime();
            q = q.Where(e => e.OccurredAtUtc >= from);
        }

        if (toDate.HasValue)
        {
            var to = DateTime.SpecifyKind(toDate.Value.Date.AddDays(1), DateTimeKind.Local).ToUniversalTime();
            q = q.Where(e => e.OccurredAtUtc < to);
        }

        if (errorsOnly)
            q = q.Where(e => e.StatusCode >= 400);

        var filtered = q.ToList();
        var total    = filtered.Count;
        var paged    = filtered.Skip((page - 1) * PageSize).Take(PageSize).ToList();

        var controllers = all
            .Select(e => e.Controller)
            .Where(c => !string.IsNullOrWhiteSpace(c))
            .Distinct(StringComparer.OrdinalIgnoreCase)
            .OrderBy(c => c)
            .ToList();

        return View(new ActivityLogViewModel
        {
            TenantKey            = tenantKey,
            Username             = username,
            Controller           = controller,
            HttpMethod           = httpMethod,
            FromDate             = fromDate,
            ToDate               = toDate,
            ErrorsOnly           = errorsOnly,
            Entries              = paged,
            AvailableTenants     = _logService.GetTenantKeys(),
            AvailableControllers = controllers,
            Pagination           = new PaginationViewModel
            {
                Page       = page,
                PageSize   = PageSize,
                TotalCount = total
            }
        });
    }
}
