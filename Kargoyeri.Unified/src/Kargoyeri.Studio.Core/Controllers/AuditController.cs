using System.Globalization;
using System.Text;
using Kargoyeri.Application.Services;
using Kargoyeri.Studio.Core.Infrastructure;
using Kargoyeri.Studio.Core.Models;
using Microsoft.AspNetCore.Mvc;

namespace Kargoyeri.Studio.Core.Controllers;

public sealed class AuditController : Controller
{
    private readonly CargoOrchestrator _orchestrator;
    private readonly CustomerService _customerService;
    private readonly IStudioWorkspaceContext _workspaceContext;
    private readonly AuditExportPackageService _auditExportPackageService;

    public AuditController(
        CargoOrchestrator orchestrator,
        CustomerService customerService,
        IStudioWorkspaceContext workspaceContext,
        AuditExportPackageService auditExportPackageService)
    {
        _orchestrator = orchestrator;
        _customerService = customerService;
        _workspaceContext = workspaceContext;
        _auditExportPackageService = auditExportPackageService;
    }

    [HttpGet]
    public async Task<IActionResult> Index([FromQuery] string? shipmentRef, [FromQuery] int page = 1, CancellationToken cancellationToken = default)
    {
        const int pageSize = 30;
        var workspace = await _workspaceContext.GetOrCreateAsync(_customerService, cancellationToken);

        if (!string.IsNullOrWhiteSpace(shipmentRef))
        {
            var logs = await _orchestrator.ListLogsAsync(shipmentRef, workspace.TenantKey, cancellationToken);
            var totalCount = logs.Count;
            var safePage = Math.Max(1, page);
            var paged = logs.Skip((safePage - 1) * pageSize).Take(pageSize).ToArray();

            return View(new AuditLogViewModel
            {
                WorkspaceCode = workspace.TenantKey,
                WorkspaceName = workspace.Name,
                ShipmentRef = shipmentRef,
                Logs = paged,
                Pagination = new PaginationViewModel { Page = safePage, PageSize = pageSize, TotalCount = totalCount }
            });
        }

        // Shipment bazinda ozet: son N gonderi
        var shipments = await _orchestrator.ListShipmentsAsync(workspace.TenantKey, cancellationToken);
        var safePageAll = Math.Max(1, page);
        var pagedShipments = shipments.Skip((safePageAll - 1) * pageSize).Take(pageSize).ToArray();

        return View(new AuditLogViewModel
        {
            WorkspaceCode = workspace.TenantKey,
            WorkspaceName = workspace.Name,
            ShipmentRef = null,
            Logs = Array.Empty<Kargoyeri.Contracts.Dtos.ShipmentOperationLogDto>(),
            RecentShipments = pagedShipments,
            Pagination = new PaginationViewModel { Page = safePageAll, PageSize = pageSize, TotalCount = shipments.Count }
        });
    }

    /// <summary>
    /// P2-#2 — Audit log CSV export. Tarih araligi (fromUtc..toUtc) verilirse o aralik;
    /// shipmentRef verilirse sadece o gonderinin loglari; ikisi de yoksa son 30 gunun
    /// tum tenant loglari (orchestrator-side filtre destegi olmadigi icin tenant tarama).
    /// </summary>
    [HttpGet("Audit/Export")]
    public async Task<IActionResult> Export(
        [FromQuery] string? shipmentRef,
        [FromQuery] DateTime? fromUtc,
        [FromQuery] DateTime? toUtc,
        CancellationToken cancellationToken)
    {
        var workspace = await _workspaceContext.GetOrCreateAsync(_customerService, cancellationToken);

        var fromBound = fromUtc ?? DateTime.UtcNow.AddDays(-30);
        var toBound   = toUtc   ?? DateTime.UtcNow.AddDays(1);

        var logs = new List<(string ShipmentRef, Kargoyeri.Contracts.Dtos.ShipmentOperationLogDto Log)>();

        if (!string.IsNullOrWhiteSpace(shipmentRef))
        {
            var single = await _orchestrator.ListLogsAsync(shipmentRef, workspace.TenantKey, cancellationToken);
            logs.AddRange(single
                .Where(l => l.OccurredAtUtc >= fromBound && l.OccurredAtUtc <= toBound)
                .Select(l => (shipmentRef, l)));
        }
        else
        {
            // Tum tenant gonderileri uzerinde gez — buyuk veri setlerinde dikkatli kullanin.
            var ships = await _orchestrator.ListShipmentsAsync(workspace.TenantKey, cancellationToken);
            foreach (var s in ships)
            {
                cancellationToken.ThrowIfCancellationRequested();
                var ll = await _orchestrator.ListLogsAsync(s.ShipmentReference, workspace.TenantKey, cancellationToken);
                foreach (var l in ll)
                {
                    if (l.OccurredAtUtc >= fromBound && l.OccurredAtUtc <= toBound)
                        logs.Add((s.ShipmentReference, l));
                }
            }
        }

        var sb = new StringBuilder();
        sb.AppendLine("OccurredAtUtc;ShipmentReference;Operation;Severity;Message;ProviderPayload");
        foreach (var (sref, log) in logs.OrderBy(x => x.Log.OccurredAtUtc))
        {
            sb.Append(log.OccurredAtUtc.ToString("o", CultureInfo.InvariantCulture)).Append(';');
            sb.Append(CsvEscape(sref)).Append(';');
            sb.Append(CsvEscape(log.Operation)).Append(';');
            sb.Append(CsvEscape(log.Severity)).Append(';');
            sb.Append(CsvEscape(log.Message)).Append(';');
            sb.Append(CsvEscape(log.ProviderPayload ?? "")).AppendLine();
        }

        var bytes = Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(sb.ToString())).ToArray();
        var filename = $"audit-{workspace.TenantKey}-{fromBound:yyyyMMdd}-{toBound:yyyyMMdd}.csv";
        return File(bytes, "text/csv; charset=utf-8", filename);
    }

    [HttpGet("Audit/ExportPackage")]
    public async Task<IActionResult> ExportPackage(
        [FromQuery] string? shipmentRef,
        [FromQuery] DateTime? fromUtc,
        [FromQuery] DateTime? toUtc,
        CancellationToken cancellationToken)
    {
        var workspace = await _workspaceContext.GetOrCreateAsync(_customerService, cancellationToken);
        var fromBound = fromUtc ?? DateTime.UtcNow.AddDays(-30);
        var toBound = toUtc ?? DateTime.UtcNow.AddDays(1);
        var package = await _auditExportPackageService.BuildAsync(
            workspace.TenantKey,
            fromBound,
            toBound,
            shipmentRef,
            cancellationToken);

        return File(package.Content, "application/zip", package.FileName);
    }

    private static string CsvEscape(string s)
    {
        if (string.IsNullOrEmpty(s)) return "";
        var needsQuotes = s.Contains(';') || s.Contains('"') || s.Contains('\n') || s.Contains('\r');
        var escaped = s.Replace("\"", "\"\"");
        return needsQuotes ? $"\"{escaped}\"" : escaped;
    }
}
