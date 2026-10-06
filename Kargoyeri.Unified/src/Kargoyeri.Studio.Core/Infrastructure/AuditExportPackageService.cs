using System.Globalization;
using System.IO.Compression;
using System.Text;
using System.Text.Json;
using Kargoyeri.Application.Abstractions.Persistence;
using Kargoyeri.Application.Services;
using Kargoyeri.Contracts.Dtos;
using Kargoyeri.Studio.Core.Models;

namespace Kargoyeri.Studio.Core.Infrastructure;

public sealed class AuditExportPackageService
{
    private readonly CustomerService _customerService;
    private readonly IShipmentRepository _shipmentRepository;
    private readonly NotificationService _notificationService;
    private readonly CargoOrchestrator _orchestrator;
    private readonly IStudioActivityLogService _activityLogService;

    public AuditExportPackageService(
        CustomerService customerService,
        IShipmentRepository shipmentRepository,
        NotificationService notificationService,
        CargoOrchestrator orchestrator,
        IStudioActivityLogService activityLogService)
    {
        _customerService = customerService;
        _shipmentRepository = shipmentRepository;
        _notificationService = notificationService;
        _orchestrator = orchestrator;
        _activityLogService = activityLogService;
    }

    public async Task<AuditExportPackage> BuildAsync(
        string tenantKey,
        DateTime fromUtc,
        DateTime toUtc,
        string? shipmentReference,
        CancellationToken cancellationToken)
    {
        var profile = await _customerService.GetProfileAsync(tenantKey, cancellationToken)
            ?? throw new InvalidOperationException($"Tenant '{tenantKey}' bulunamadi.");

        var shipments = string.IsNullOrWhiteSpace(shipmentReference)
            ? await _shipmentRepository.ListByDateRangeAsync(tenantKey, fromUtc, toUtc, cancellationToken)
            : await LoadSingleShipmentAsync(tenantKey, shipmentReference, cancellationToken);

        var shipmentRefs = shipments.Select(x => x.ShipmentReference).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var notifications = (await _notificationService.ListAsync(tenantKey, null, cancellationToken))
            .Where(x => shipmentRefs.Contains(x.ShipmentReference))
            .Where(x => x.CreatedAtUtc.UtcDateTime >= fromUtc && x.CreatedAtUtc.UtcDateTime <= toUtc)
            .ToArray();

        var auditLogs = new List<object>();
        foreach (var shipment in shipments.OrderBy(x => x.CreatedAtUtc))
        {
            cancellationToken.ThrowIfCancellationRequested();
            var logs = await _orchestrator.ListLogsAsync(shipment.ShipmentReference, tenantKey, cancellationToken);
            foreach (var log in logs.Where(x => x.OccurredAtUtc >= fromUtc && x.OccurredAtUtc <= toUtc))
            {
                auditLogs.Add(new
                {
                    shipmentReference = shipment.ShipmentReference,
                    log.OccurredAtUtc,
                    log.Operation,
                    log.Severity,
                    log.Message,
                    log.ProviderPayload
                });
            }
        }

        var activityLogs = _activityLogService.GetRecent(tenantKey, max: 5000)
            .Where(x => x.OccurredAtUtc.UtcDateTime >= fromUtc && x.OccurredAtUtc.UtcDateTime <= toUtc)
            .OrderByDescending(x => x.OccurredAtUtc)
            .ToArray();

        var summary = new
        {
            tenant = new
            {
                profile.TenantKey,
                profile.Name,
                profile.IsActive,
                profile.AllowedProviders,
                profile.NotificationTargets,
                profile.UpdatedAtUtc
            },
            range = new
            {
                fromUtc,
                toUtc,
                shipmentReference
            },
            counts = new
            {
                shipments = shipments.Count,
                notifications = notifications.Length,
                auditLogs = auditLogs.Count,
                activityLogs = activityLogs.Length
            },
            generatedAtUtc = DateTimeOffset.UtcNow
        };

        using var stream = new MemoryStream();
        using (var archive = new ZipArchive(stream, ZipArchiveMode.Create, leaveOpen: true))
        {
            WriteEntry(archive, "summary.json", Json(summary));
            WriteEntry(archive, "shipments.json", Json(shipments));
            WriteEntry(archive, "notifications.json", Json(notifications));
            WriteEntry(archive, "audit-logs.json", Json(auditLogs));
            WriteEntry(archive, "activity-logs.json", Json(activityLogs));
            WriteEntry(archive, "summary.pdf", BuildPdf(profile.Name, tenantKey, fromUtc, toUtc, shipments.Count, notifications.Length, auditLogs.Count, activityLogs.Length));
        }

        return new AuditExportPackage(
            stream.ToArray(),
            $"kvkk-export-{tenantKey}-{DateTime.UtcNow:yyyyMMddHHmmss}.zip",
            new AuditExportPackageViewModel
            {
                WorkspaceCode = tenantKey,
                WorkspaceName = profile.Name,
                GeneratedAtUtc = DateTimeOffset.UtcNow,
                ShipmentCount = shipments.Count,
                NotificationCount = notifications.Length,
                AuditLogCount = auditLogs.Count,
                ActivityLogCount = activityLogs.Length
            });
    }

    private static string Json(object payload) => JsonSerializer.Serialize(payload, new JsonSerializerOptions
    {
        WriteIndented = true
    });

    private async Task<IReadOnlyCollection<Kargoyeri.Domain.Entities.CargoShipment>> LoadSingleShipmentAsync(
        string tenantKey,
        string shipmentReference,
        CancellationToken cancellationToken)
    {
        var shipment = await _shipmentRepository.GetByReferenceAsync(shipmentReference, cancellationToken);
        if (shipment is null || !string.Equals(shipment.TenantKey, tenantKey, StringComparison.OrdinalIgnoreCase))
        {
            return Array.Empty<Kargoyeri.Domain.Entities.CargoShipment>();
        }

        return [shipment];
    }

    private static void WriteEntry(ZipArchive archive, string name, string content)
    {
        var entry = archive.CreateEntry(name, CompressionLevel.Fastest);
        using var writer = new StreamWriter(entry.Open(), new UTF8Encoding(encoderShouldEmitUTF8Identifier: false));
        writer.Write(content);
    }

    private static void WriteEntry(ZipArchive archive, string name, byte[] content)
    {
        var entry = archive.CreateEntry(name, CompressionLevel.Fastest);
        using var target = entry.Open();
        target.Write(content, 0, content.Length);
    }

    private static byte[] BuildPdf(
        string workspaceName,
        string tenantKey,
        DateTime fromUtc,
        DateTime toUtc,
        int shipmentCount,
        int notificationCount,
        int auditLogCount,
        int activityLogCount)
    {
        var lines = new[]
        {
            "KVKK Veri Paketi",
            $"Musteri: {workspaceName} ({tenantKey})",
            $"Aralik: {fromUtc:yyyy-MM-dd} - {toUtc:yyyy-MM-dd}",
            $"Gonderi kaydi: {shipmentCount}",
            $"Bildirim kaydi: {notificationCount}",
            $"Operasyon audit kaydi: {auditLogCount}",
            $"Panel aktivite kaydi: {activityLogCount}",
            $"Uretim zamani: {DateTimeOffset.UtcNow.ToString("dd.MM.yyyy HH:mm", CultureInfo.InvariantCulture)} UTC",
            "JSON dosyalari paketin icinde ayrica yer alir."
        };

        var escapedLines = lines.Select(EscapePdfText).ToArray();
        var content = new StringBuilder();
        content.AppendLine("BT");
        content.AppendLine("/F1 16 Tf");
        content.AppendLine("50 780 Td");
        content.AppendLine($"({escapedLines[0]}) Tj");
        content.AppendLine("/F1 11 Tf");
        for (var i = 1; i < escapedLines.Length; i++)
        {
            content.AppendLine("0 -22 Td");
            content.AppendLine($"({escapedLines[i]}) Tj");
        }
        content.AppendLine("ET");

        return MinimalPdfBuilder.Build(content.ToString());
    }

    private static string EscapePdfText(string value)
    {
        return value
            .Replace("\\", "\\\\", StringComparison.Ordinal)
            .Replace("(", "\\(", StringComparison.Ordinal)
            .Replace(")", "\\)", StringComparison.Ordinal);
    }

    public sealed record AuditExportPackage(byte[] Content, string FileName, AuditExportPackageViewModel Summary);

    private static class MinimalPdfBuilder
    {
        public static byte[] Build(string pageContent)
        {
            var objects = new List<string>();
            objects.Add("<< /Type /Catalog /Pages 2 0 R >>");
            objects.Add("<< /Type /Pages /Kids [3 0 R] /Count 1 >>");
            objects.Add("<< /Type /Page /Parent 2 0 R /MediaBox [0 0 595 842] /Resources << /Font << /F1 4 0 R >> >> /Contents 5 0 R >>");
            objects.Add("<< /Type /Font /Subtype /Type1 /BaseFont /Helvetica >>");

            var streamBytes = Encoding.ASCII.GetBytes(pageContent);
            objects.Add($"<< /Length {streamBytes.Length} >>\nstream\n{pageContent}endstream");

            var sb = new StringBuilder();
            sb.Append("%PDF-1.4\n");
            var offsets = new List<int> { 0 };

            for (var i = 0; i < objects.Count; i++)
            {
                offsets.Add(sb.Length);
                sb.Append($"{i + 1} 0 obj\n{objects[i]}\nendobj\n");
            }

            var xrefPosition = sb.Length;
            sb.Append($"xref\n0 {objects.Count + 1}\n");
            sb.Append("0000000000 65535 f \n");
            for (var i = 1; i < offsets.Count; i++)
            {
                sb.Append($"{offsets[i]:D10} 00000 n \n");
            }

            sb.Append("trailer\n");
            sb.Append($"<< /Size {objects.Count + 1} /Root 1 0 R >>\n");
            sb.Append("startxref\n");
            sb.Append(xrefPosition);
            sb.Append("\n%%EOF");
            return Encoding.ASCII.GetBytes(sb.ToString());
        }
    }
}
