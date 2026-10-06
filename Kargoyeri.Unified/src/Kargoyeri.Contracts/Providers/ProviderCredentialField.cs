namespace Kargoyeri.Contracts.Providers;

/// <summary>
/// Her sağlayıcının bağlantı formu için alan tanımı.
/// UI bu şemayı okuyup dinamik form render eder — sabit hardcode form yok.
/// </summary>
public record ProviderCredentialField(
    string Key,
    string Label,
    CredentialFieldType Type = CredentialFieldType.Text,
    string? Placeholder = null,
    string? HelpText = null,
    bool Required = true,
    string? DefaultValue = null,
    IReadOnlyList<SelectOption>? Options = null
);

public enum CredentialFieldType
{
    Text,
    Password,
    Url,
    Email,
    Number,
    Select,
    Toggle,
}

public record SelectOption(string Value, string Label);

/// <summary>
/// Bir sağlayıcının bağlantı form şeması.
/// CredentialSchema bu nesneden okunur; her provider kendi şemasını döner.
/// </summary>
public record ProviderFormSchema(
    string ProviderId,
    string Title,
    string? Description,
    IReadOnlyList<ProviderCredentialField> Fields,
    string? DocumentationUrl = null
);
