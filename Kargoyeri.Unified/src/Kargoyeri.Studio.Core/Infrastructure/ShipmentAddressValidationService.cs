using System.Net.Http.Json;
using System.Text.Json;
using Kargoyeri.Application.Services;
using Kargoyeri.Contracts.Dtos;
using Kargoyeri.Studio.Core.Models;

namespace Kargoyeri.Studio.Core.Infrastructure;

public sealed class ShipmentAddressValidationService
{
    private static readonly string[] SuspiciousTerms =
    {
        "test", "deneme", "asdf", "qwer", "bilinmiyor", "unknown", "lorem"
    };

    private readonly CustomerService _customerService;
    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<ShipmentAddressValidationService> _logger;

    public ShipmentAddressValidationService(
        CustomerService customerService,
        IHttpClientFactory httpClientFactory,
        ILogger<ShipmentAddressValidationService> logger)
    {
        _customerService = customerService;
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    public async Task<AddressValidationSummaryViewModel> ValidateAsync(
        string tenantKey,
        AddressDto address,
        CancellationToken cancellationToken)
    {
        var profile = await _customerService.GetProfileAsync(tenantKey, cancellationToken);
        var settings = WorkspaceFeatureMetadata.ReadAddressValidation(profile);
        if (!settings.Enabled)
        {
            return new AddressValidationSummaryViewModel
            {
                Enabled = false,
                IsValid = true,
                StatusText = "Adres dogrulama devre disi."
            };
        }

        var result = new AddressValidationSummaryViewModel
        {
            Enabled = true,
            IsValid = true,
            StatusText = settings.HasRemoteConfiguration
                ? "PTT / adres dogrulama kontrolu uygulandi."
                : "Kuralli adres dogrulama uygulandi."
        };

        ApplyLocalRules(address, result);

        if (settings.HasRemoteConfiguration)
        {
            await TryRemoteValidationAsync(settings, address, result, cancellationToken);
        }

        if (settings.StrictMode && result.HasBlockingIssues)
        {
            result.IsValid = false;
            result.StatusText = "Adres dogrulama basarisiz.";
        }
        else if (!settings.StrictMode && result.HasBlockingIssues)
        {
            result.StatusText = "Adres riskli bulundu; yine de devam ettirilebilir.";
        }

        return result;
    }

    public static void ApplyNormalization(AddressDto address, AddressValidationSummaryViewModel validation)
    {
        if (!string.IsNullOrWhiteSpace(validation.NormalizedPhone))
        {
            address.Phone = validation.NormalizedPhone;
        }

        if (!string.IsNullOrWhiteSpace(validation.NormalizedCity))
        {
            address.City = validation.NormalizedCity;
        }

        if (!string.IsNullOrWhiteSpace(validation.NormalizedDistrict))
        {
            address.District = validation.NormalizedDistrict;
        }

        if (!string.IsNullOrWhiteSpace(validation.NormalizedAddressLine1))
        {
            address.AddressLine1 = validation.NormalizedAddressLine1;
        }
    }

    private static void ApplyLocalRules(AddressDto address, AddressValidationSummaryViewModel result)
    {
        var issues = new List<AddressValidationIssueViewModel>();
        var phoneDigits = new string((address.Phone ?? string.Empty).Where(char.IsDigit).ToArray());
        var normalizedPhone = phoneDigits.Length switch
        {
            10 => "0" + phoneDigits,
            11 => phoneDigits,
            _ => null
        };

        result.NormalizedPhone = normalizedPhone;
        result.NormalizedCity = NormalizeWord(address.City);
        result.NormalizedDistrict = NormalizeWord(address.District);
        result.NormalizedAddressLine1 = NormalizeAddress(address.AddressLine1);

        if (string.IsNullOrWhiteSpace(address.Name) || address.Name.Trim().Length < 3)
        {
            issues.Add(Block("Alici adi cok kisa veya bos."));
        }

        if (normalizedPhone is null)
        {
            issues.Add(Block("Telefon numarasi 10-11 haneli olmalidir."));
        }

        if (string.IsNullOrWhiteSpace(address.City) || address.City.Trim().Length < 2)
        {
            issues.Add(Block("Il bilgisi eksik."));
        }

        if (string.IsNullOrWhiteSpace(address.District) || address.District!.Trim().Length < 2)
        {
            issues.Add(Block("Ilce bilgisi eksik."));
        }

        if (string.IsNullOrWhiteSpace(address.AddressLine1) || address.AddressLine1.Trim().Length < 10)
        {
            issues.Add(Block("Adres satiri cok kisa; mahalle, cadde ve bina bilgisi eklenmeli."));
        }
        else
        {
            var normalized = address.AddressLine1.Trim().ToLowerInvariant();
            if (SuspiciousTerms.Any(normalized.Contains))
            {
                issues.Add(Block("Adres icinde test/placeholder ifade bulundu."));
            }

            if (!normalized.Any(char.IsDigit))
            {
                issues.Add(Warn("Adres satirinda bina veya kapi numarasi bulunmuyor gibi gorunuyor."));
            }
        }

        result.Issues = issues;
    }

    private async Task TryRemoteValidationAsync(
        AddressValidationWorkspaceSettings settings,
        AddressDto address,
        AddressValidationSummaryViewModel result,
        CancellationToken cancellationToken)
    {
        try
        {
            using var client = _httpClientFactory.CreateClient("studio-address-validation");
            using var request = new HttpRequestMessage(HttpMethod.Post, BuildValidationUrl(settings.ApiBaseUrl!))
            {
                Content = JsonContent.Create(new
                {
                    fullName = address.Name,
                    phone = address.Phone,
                    city = address.City,
                    district = address.District,
                    addressLine1 = address.AddressLine1,
                    postalCode = address.PostalCode,
                    countryCode = address.CountryCode
                })
            };

            if (!string.IsNullOrWhiteSpace(settings.ApiKey))
            {
                request.Headers.TryAddWithoutValidation("X-Api-Key", settings.ApiKey);
            }

            if (!string.IsNullOrWhiteSpace(settings.ApiSecret))
            {
                request.Headers.TryAddWithoutValidation("X-Api-Secret", settings.ApiSecret);
            }

            using var response = await client.SendAsync(request, cancellationToken);
            result.UsedRemoteProvider = true;

            if (!response.IsSuccessStatusCode)
            {
                AppendIssue(result, Warn($"PTT/adres servisi {response.StatusCode} dondu; yerel kurallarla devam edildi."));
                return;
            }

            using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync(cancellationToken));
            var root = document.RootElement;

            if (TryReadBoolean(root, "isValid", "valid", "success") == false)
            {
                AppendIssue(result, Block("PTT/adres servisi bu adresi gecersiz olarak isaretledi."));
            }

            result.NormalizedCity ??= ReadString(root, "city", "normalizedCity");
            result.NormalizedDistrict ??= ReadString(root, "district", "normalizedDistrict");
            result.NormalizedAddressLine1 ??= ReadString(root, "addressLine1", "normalizedAddress", "normalizedAddressLine1");
            result.NormalizedPhone ??= ReadString(root, "phone", "normalizedPhone");

            foreach (var warning in ReadStringArray(root, "warnings", "messages"))
            {
                AppendIssue(result, Warn(warning));
            }
        }
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Adres dogrulama servisi cagrisinda hata olustu.");
            AppendIssue(result, Warn("PTT/adres dogrulama servisine ulasilamadi; yerel kurallarla devam edildi."));
        }
    }

    private static void AppendIssue(AddressValidationSummaryViewModel result, AddressValidationIssueViewModel issue)
    {
        result.Issues = result.Issues.Concat(new[] { issue }).ToArray();
    }

    private static string BuildValidationUrl(string baseUrl)
    {
        return baseUrl.EndsWith("/validate", StringComparison.OrdinalIgnoreCase)
            ? baseUrl
            : baseUrl.TrimEnd('/') + "/validate";
    }

    private static AddressValidationIssueViewModel Warn(string message) => new()
    {
        Severity = "warning",
        Message = message,
        IsBlocking = false
    };

    private static AddressValidationIssueViewModel Block(string message) => new()
    {
        Severity = "error",
        Message = message,
        IsBlocking = true
    };

    private static string? NormalizeWord(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        var parts = value.Trim()
            .Split(' ', StringSplitOptions.RemoveEmptyEntries)
            .Select(x => char.ToUpperInvariant(x[0]) + x[1..].ToLowerInvariant());

        return string.Join(" ", parts);
    }

    private static string? NormalizeAddress(string? value)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            return null;
        }

        return string.Join(" ",
            value.Trim()
                .Split(new[] { ' ', '\t', '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries));
    }

    private static bool? TryReadBoolean(JsonElement root, params string[] names)
    {
        foreach (var name in names)
        {
            if (root.TryGetProperty(name, out var property))
            {
                if (property.ValueKind == JsonValueKind.True) return true;
                if (property.ValueKind == JsonValueKind.False) return false;
            }
        }

        return null;
    }

    private static string? ReadString(JsonElement root, params string[] names)
    {
        foreach (var name in names)
        {
            if (root.TryGetProperty(name, out var property) && property.ValueKind == JsonValueKind.String)
            {
                return property.GetString();
            }
        }

        return null;
    }

    private static IReadOnlyCollection<string> ReadStringArray(JsonElement root, params string[] names)
    {
        foreach (var name in names)
        {
            if (!root.TryGetProperty(name, out var property) || property.ValueKind != JsonValueKind.Array)
            {
                continue;
            }

            return property.EnumerateArray()
                .Where(x => x.ValueKind == JsonValueKind.String && !string.IsNullOrWhiteSpace(x.GetString()))
                .Select(x => x.GetString()!)
                .ToArray();
        }

        return Array.Empty<string>();
    }
}
