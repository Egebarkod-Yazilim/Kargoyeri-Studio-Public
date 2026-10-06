using Kargoyeri.Application.Services;

namespace Kargoyeri.Studio.Core.Infrastructure;

public sealed class WorkspaceBrandingStore
{
    private readonly CustomerService _customerService;
    private readonly IStudioWorkspaceContext _workspaceContext;

    public WorkspaceBrandingStore(CustomerService customerService, IStudioWorkspaceContext workspaceContext)
    {
        _customerService = customerService;
        _workspaceContext = workspaceContext;
    }

    public async Task<WorkspaceBrandingSettings> GetCurrentAsync(CancellationToken cancellationToken = default)
    {
        var tenantKey = _workspaceContext.GetCurrentCode();
        if (string.IsNullOrWhiteSpace(tenantKey))
        {
            return new WorkspaceBrandingSettings();
        }

        var profile = await _customerService.GetProfileAsync(tenantKey, cancellationToken);
        return WorkspaceFeatureMetadata.ReadBranding(profile);
    }
}
