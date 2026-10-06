// Generated from assembly metadata during repository recovery.
#nullable enable
namespace Kargoyeri.Application.Services;

public sealed partial class CustomerService
{
    public CustomerService(global::Kargoyeri.Application.Abstractions.Persistence.ICustomerRepository customerRepository, global::Kargoyeri.Application.Abstractions.Persistence.IUnitOfWork unitOfWork) { }
    public global::System.Threading.Tasks.Task<global::Kargoyeri.Domain.Entities.CustomerTenant> RequireTenantAsync(string tenantKey, global::System.Threading.CancellationToken cancellationToken)
    {
        throw new global::System.NotImplementedException();
    }
    public global::System.Threading.Tasks.Task<global::Kargoyeri.Domain.Entities.CustomerTenant> EnsureCustomerAsync(string customerCode, string customerName, string notificationWebhook, string notificationEmail, string notificationPhone, global::System.Threading.CancellationToken cancellationToken)
    {
        throw new global::System.NotImplementedException();
    }
    public global::System.Threading.Tasks.Task<global::Kargoyeri.Contracts.Dtos.CustomerProfileDto> GetProfileAsync(string tenantKey, global::System.Threading.CancellationToken cancellationToken)
    {
        throw new global::System.NotImplementedException();
    }
    public global::System.Threading.Tasks.Task<global::Kargoyeri.Contracts.Dtos.CustomerProfileDto> UpsertAsync(string tenantKey, global::Kargoyeri.Contracts.Dtos.UpsertCustomerRequest request, global::System.Threading.CancellationToken cancellationToken)
    {
        throw new global::System.NotImplementedException();
    }
    public global::System.Threading.Tasks.Task<global::System.Collections.Generic.IReadOnlyCollection<global::Kargoyeri.Contracts.Dtos.CustomerProfileDto>> ListAllAsync(global::System.Threading.CancellationToken cancellationToken)
    {
        throw new global::System.NotImplementedException();
    }
}
