using Kargoyeri.Application.Services;
using Kargoyeri.Domain.Entities;

namespace Kargoyeri.Studio.Core.Infrastructure;

/// <summary>
/// Single-workspace compatibility adapter. The persisted TenantKey remains in the
/// current storage schema so existing customer data is not deleted by this change.
/// All interactive Studio users resolve to the same workspace.
/// </summary>
public interface IStudioWorkspaceContext
{
    string GetCurrentCode();
    string? GetCurrentName();
    void Set(string customerCode, string? customerName);
    Task<CustomerTenant> GetOrCreateAsync(CustomerService customerService, CancellationToken cancellationToken);
    bool IsImpersonating();
    DateTimeOffset? GetImpersonationStartedAt();
    void StartImpersonation();
    void StopImpersonation();
}

internal sealed class StudioWorkspaceContext : IStudioWorkspaceContext
{
    public const string SingleWorkspaceCode = "studio-demo";
    private const string SingleWorkspaceName = "Kargoyeri";

    public string GetCurrentCode() => SingleWorkspaceCode;

    public string? GetCurrentName() => SingleWorkspaceName;

    // Kept as a no-op for existing callers; tenant switching is disabled.
    public void Set(string customerCode, string? customerName) { }

    public bool IsImpersonating() => false;

    public DateTimeOffset? GetImpersonationStartedAt() => null;

    public void StartImpersonation() { }

    public void StopImpersonation() { }

    public async Task<CustomerTenant> GetOrCreateAsync(CustomerService customerService, CancellationToken cancellationToken)
    {
        var profile = await customerService.GetProfileAsync(SingleWorkspaceCode, cancellationToken);
        if (profile is null)
        {
            profile = await customerService.UpsertAsync(SingleWorkspaceCode, new Kargoyeri.Contracts.Dtos.UpsertCustomerRequest
            {
                Name = SingleWorkspaceName,
                IsActive = true,
                AllowedProviders = new List<Kargoyeri.Contracts.Enums.CargoProviderTypeDto>(),
                NotificationTargets = new List<Kargoyeri.Contracts.Dtos.NotificationTargetDto>(),
                Metadata = new Dictionary<string, string>()
            }, cancellationToken);
        }

        return new CustomerTenant
        {
            TenantKey = profile.TenantKey,
            Name = profile.Name,
            IsActive = profile.IsActive
        };
    }
}
