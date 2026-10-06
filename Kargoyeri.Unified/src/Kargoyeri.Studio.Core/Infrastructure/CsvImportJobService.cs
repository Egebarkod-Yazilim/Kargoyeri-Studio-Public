using System.Collections.Concurrent;
using Kargoyeri.Application.Services;
using Kargoyeri.Contracts.Dtos;
using Kargoyeri.Contracts.Enums;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Kargoyeri.Studio.Core.Infrastructure;

public enum CsvImportJobState { Queued, Running, Completed, Failed }

public sealed class CsvImportJob
{
    public Guid Id { get; init; } = Guid.NewGuid();
    public string TenantKey { get; init; } = string.Empty;
    public string FileName { get; init; } = string.Empty;
    public DateTimeOffset EnqueuedAtUtc { get; init; } = DateTimeOffset.UtcNow;
    public DateTimeOffset? StartedAtUtc { get; set; }
    public DateTimeOffset? CompletedAtUtc { get; set; }
    public CsvImportJobState State { get; set; } = CsvImportJobState.Queued;
    public int TotalRows { get; set; }
    public int SuccessCount { get; set; }
    public int FailedCount { get; set; }
    public List<string> Errors { get; } = new();
    public string? FatalError { get; set; }

    public int ProcessedCount => SuccessCount + FailedCount;
    public int ProgressPercent => TotalRows == 0 ? 0 : (int)Math.Round(100.0 * ProcessedCount / TotalRows);
}

/// <summary>
/// CSV dosyasini HTTP istegini bloklamadan arka planda isler.
/// Job durumu bellekten okunur; uygulama yeniden baslatilinca sifirlanir.
/// </summary>
public sealed class CsvImportJobService
{
    private static readonly string[] ExpectedHeaders =
    {
        "Provider",
        "OrderReference",
        "RecipientName",
        "RecipientPhone",
        "RecipientCity",
        "RecipientDistrict",
        "RecipientAddress",
        "Weight",
        "Desi",
        "CollectionAmount",
        "IdempotencyKey"
    };

    private readonly ConcurrentDictionary<Guid, CsvImportJob> _jobs = new();
    private readonly IServiceScopeFactory _scopeFactory;
    private readonly ILogger<CsvImportJobService> _logger;

    public CsvImportJobService(IServiceScopeFactory scopeFactory, ILogger<CsvImportJobService> logger)
    {
        _scopeFactory = scopeFactory;
        _logger = logger;
    }

    public CsvImportJob Enqueue(string tenantKey, string fileName, string csvContent)
    {
        var job = new CsvImportJob { TenantKey = tenantKey, FileName = fileName };
        _jobs[job.Id] = job;

        _ = Task.Run(() => RunAsync(job, csvContent));
        return job;
    }

    public CsvImportJob? Get(Guid id) => _jobs.TryGetValue(id, out var j) ? j : null;

    public IReadOnlyCollection<CsvImportJob> ListByTenant(string tenantKey) =>
        _jobs.Values
            .Where(j => string.Equals(j.TenantKey, tenantKey, StringComparison.OrdinalIgnoreCase))
            .OrderByDescending(j => j.EnqueuedAtUtc)
            .Take(20)
            .ToArray();

    private async Task RunAsync(CsvImportJob job, string csvContent)
    {
        job.State = CsvImportJobState.Running;
        job.StartedAtUtc = DateTimeOffset.UtcNow;

        try
        {
            using var scope = _scopeFactory.CreateScope();
            var orchestrator = scope.ServiceProvider.GetRequiredService<CargoOrchestrator>();
            var addressValidation = scope.ServiceProvider.GetRequiredService<ShipmentAddressValidationService>();

            var lines = csvContent.Split('\n', StringSplitOptions.None);
            ValidateHeader(lines.FirstOrDefault());
            job.TotalRows = Math.Max(0, lines.Length - 1); // header
            var seenIdempotencyKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
            var seenOrderReferences = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            for (int i = 1; i < lines.Length; i++)
            {
                var line = lines[i].TrimEnd('\r');
                if (string.IsNullOrWhiteSpace(line)) { job.TotalRows--; continue; }

                try
                {
                    var request = ParseRow(job.TenantKey, line, i + 1);

                    if (!string.IsNullOrWhiteSpace(request.IdempotencyKey) &&
                        !seenIdempotencyKeys.Add(request.IdempotencyKey))
                    {
                        throw new InvalidOperationException($"IdempotencyKey tekrar ediyor: {request.IdempotencyKey}");
                    }

                    if (!seenOrderReferences.Add(request.OrderReference))
                    {
                        throw new InvalidOperationException($"OrderReference tekrar ediyor: {request.OrderReference}");
                    }

                    var validation = await addressValidation.ValidateAsync(job.TenantKey, request.Recipient, CancellationToken.None);
                    if (validation.HasBlockingIssues)
                    {
                        throw new InvalidOperationException(validation.StatusText);
                    }

                    ShipmentAddressValidationService.ApplyNormalization(request.Recipient, validation);
                    request.Metadata["address.validationStatus"] = validation.StatusText;

                    await orchestrator.CreateShipmentAsync(request, CancellationToken.None);
                    job.SuccessCount++;
                }
                catch (Exception ex)
                {
                    job.FailedCount++;
                    if (job.Errors.Count < 50)
                        job.Errors.Add($"Satir {i + 1}: {ex.Message}");
                }
            }

            job.State = CsvImportJobState.Completed;
        }
        catch (Exception ex)
        {
            job.State = CsvImportJobState.Failed;
            job.FatalError = ex.Message;
            _logger.LogError(ex, "CSV import job {JobId} fatal hata", job.Id);
        }
        finally
        {
            job.CompletedAtUtc = DateTimeOffset.UtcNow;
        }
    }

    private static void ValidateHeader(string? line)
    {
        if (string.IsNullOrWhiteSpace(line))
        {
            throw new InvalidOperationException("CSV bos veya baslik satiri eksik.");
        }

        var columns = ParseColumns(line.TrimEnd('\r'));
        if (columns.Length < ExpectedHeaders.Length)
        {
            throw new InvalidOperationException("CSV baslik satiri eksik veya bozuk.");
        }

        for (var i = 0; i < ExpectedHeaders.Length; i++)
        {
            if (!string.Equals(columns[i], ExpectedHeaders[i], StringComparison.OrdinalIgnoreCase))
            {
                throw new InvalidOperationException(
                    $"Baslik eslesmedi. Beklenen sutun {i + 1}: '{ExpectedHeaders[i]}', gelen: '{columns[i]}'.");
            }
        }
    }

    private static CreateShipmentRequest ParseRow(string tenantKey, string line, int lineNum)
    {
        var cols = ParseColumns(line);
        if (cols.Length < 9)
            throw new InvalidOperationException($"Yetersiz sutun ({cols.Length}).");

        var providerStr   = cols[0].Trim();
        var orderRef      = cols[1].Trim();
        var recipName     = cols[2].Trim();
        var recipPhone    = cols[3].Trim();
        var recipCity     = cols[4].Trim();
        var recipDistrict = cols[5].Trim();
        var recipAddr     = cols[6].Trim();
        var weightStr     = cols[7].Trim();
        var desiStr       = cols[8].Trim();
        var collStr       = cols.Length > 9 ? cols[9].Trim() : "";
        var idempKey      = cols.Length > 10 ? cols[10].Trim() : "";
        // 12. kolon (opsiyonel): SourceChannel — "Trendyol"/"Hepsiburada"/"Manual"/...
        // Bos veya gecersizse Manual default.
        var sourceChStr   = cols.Length > 11 ? cols[11].Trim() : "";

        if (!Enum.TryParse<CargoProviderTypeDto>(providerStr, true, out var provider))
            throw new InvalidOperationException($"Gecersiz provider '{providerStr}'.");

        OrderSourceChannelDto sourceChannel = OrderSourceChannelDto.Manual;
        if (!string.IsNullOrWhiteSpace(sourceChStr)
            && Enum.TryParse<OrderSourceChannelDto>(sourceChStr, true, out var parsedSrc))
        {
            sourceChannel = parsedSrc;
        }

        if (string.IsNullOrWhiteSpace(orderRef) || string.IsNullOrWhiteSpace(recipName)
            || string.IsNullOrWhiteSpace(recipCity) || string.IsNullOrWhiteSpace(recipAddr))
            throw new InvalidOperationException("Zorunlu alanlar eksik (OrderRef, Alici, Sehir, Adres).");

        decimal.TryParse(weightStr, System.Globalization.NumberStyles.Any,
            System.Globalization.CultureInfo.InvariantCulture, out var weight);
        decimal.TryParse(desiStr, System.Globalization.NumberStyles.Any,
            System.Globalization.CultureInfo.InvariantCulture, out var desi);
        decimal? collAmount = decimal.TryParse(collStr, System.Globalization.NumberStyles.Any,
            System.Globalization.CultureInfo.InvariantCulture, out var collParsed) ? collParsed : null;

        return new CreateShipmentRequest
        {
            TenantKey        = tenantKey,
            Source           = IntegrationSourceTypeDto.Manual,
            SourceChannel    = sourceChannel,
            // null bırakılırsa ShipmentMapping kanonik prefix kodunu (mn/ty/et-shp ...) atar.
            SourceChannelCode = null,
            Provider         = provider,
            OrderReference   = orderRef,
            IdempotencyKey   = string.IsNullOrWhiteSpace(idempKey) ? null : idempKey,
            CollectionAmount = collAmount > 0 ? collAmount : null,
            CurrencyCode     = "TRY",
            Sender = new AddressDto
            {
                Name = "Kargoyeri Studio", Phone = "05550000000",
                City = "Istanbul", District = "Umraniye", AddressLine1 = "Inkilap Mah. No:10"
            },
            Recipient = new AddressDto
            {
                Name         = recipName,
                Phone        = string.IsNullOrWhiteSpace(recipPhone) ? null : recipPhone,
                City         = recipCity,
                District     = string.IsNullOrWhiteSpace(recipDistrict) ? null : recipDistrict,
                AddressLine1 = recipAddr
            },
            Packages = new List<PackageDto>
            {
                new()
                {
                    PackageSequence      = 1,
                    Weight               = weight > 0 ? weight : 1,
                    Desi                 = desi > 0 ? desi : 1,
                    CashOnDeliveryAmount = collAmount > 0 ? collAmount : null
                }
            },
            Metadata = new Dictionary<string, string> { ["studio.origin"] = "bulk-import" }
        };
    }

    private static string[] ParseColumns(string line)
    {
        var items = new List<string>();
        var current = new System.Text.StringBuilder();
        var inQuotes = false;

        for (var i = 0; i < line.Length; i++)
        {
            var ch = line[i];
            if (ch == '"')
            {
                if (inQuotes && i + 1 < line.Length && line[i + 1] == '"')
                {
                    current.Append('"');
                    i++;
                }
                else
                {
                    inQuotes = !inQuotes;
                }

                continue;
            }

            if (ch == ';' && !inQuotes)
            {
                items.Add(current.ToString().Trim());
                current.Clear();
                continue;
            }

            current.Append(ch);
        }

        items.Add(current.ToString().Trim());
        return items.ToArray();
    }
}
