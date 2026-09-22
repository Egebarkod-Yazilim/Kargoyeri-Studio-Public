// Generated from assembly metadata during repository recovery.
#nullable enable
namespace Kargoyeri.Application.Services;

public sealed partial class CargoOrchestrator
{
    public CargoOrchestrator(global::Kargoyeri.Application.Abstractions.Persistence.IShipmentRepository shipmentRepository, global::Kargoyeri.Application.Abstractions.Persistence.IOperationLogRepository operationLogRepository, global::Kargoyeri.Application.Abstractions.Persistence.IProviderSettingsRepository providerSettingsRepository, global::Kargoyeri.Application.Abstractions.Providers.ICargoProviderResolver providerResolver, global::Kargoyeri.Application.Services.CustomerService customerService, global::Kargoyeri.Application.Services.NotificationService notificationService, global::Kargoyeri.Application.Abstractions.Persistence.IUnitOfWork unitOfWork) { }
    public global::System.Threading.Tasks.Task<global::Kargoyeri.Contracts.Dtos.CreateShipmentResponse> CreateShipmentAsync(global::Kargoyeri.Contracts.Dtos.CreateShipmentRequest request, global::System.Threading.CancellationToken cancellationToken)
    {
        throw new global::System.NotImplementedException();
    }
    public global::System.Threading.Tasks.Task<global::Kargoyeri.Contracts.Dtos.CancelShipmentResponse> CancelShipmentAsync(string shipmentReference, string tenantKey, string reason, global::System.Threading.CancellationToken cancellationToken)
    {
        throw new global::System.NotImplementedException();
    }
    public global::System.Threading.Tasks.Task<global::Kargoyeri.Contracts.Dtos.ShipmentDetailResponse> GetShipmentAsync(string shipmentReference, string tenantKey, global::System.Threading.CancellationToken cancellationToken)
    {
        throw new global::System.NotImplementedException();
    }
    public global::System.Threading.Tasks.Task<global::Kargoyeri.Contracts.Dtos.ShipmentDetailResponse> GetShipmentByTrackingNumberAsync(string trackingNumber, global::System.Threading.CancellationToken cancellationToken)
    {
        throw new global::System.NotImplementedException();
    }
    public global::System.Threading.Tasks.Task<global::Kargoyeri.Contracts.Dtos.ShipmentDetailResponse> RefreshShipmentAsync(string shipmentReference, string tenantKey, global::System.Threading.CancellationToken cancellationToken)
    {
        throw new global::System.NotImplementedException();
    }
    public global::System.Threading.Tasks.Task<global::System.Collections.Generic.IReadOnlyCollection<global::Kargoyeri.Contracts.Dtos.ShipmentListItemResponse>> ListShipmentsAsync(string tenantKey, global::System.Threading.CancellationToken cancellationToken)
    {
        throw new global::System.NotImplementedException();
    }
    public global::System.Threading.Tasks.Task<global::System.ValueTuple<global::System.Collections.Generic.IReadOnlyCollection<global::Kargoyeri.Contracts.Dtos.ShipmentListItemResponse>, int>> ListShipmentsPagedAsync(string tenantKey, string search, string statusFilter, string providerFilter, int page, int pageSize, global::System.Threading.CancellationToken cancellationToken)
    {
        throw new global::System.NotImplementedException();
    }
    public global::System.Threading.Tasks.Task<global::System.Collections.Generic.IReadOnlyCollection<global::Kargoyeri.Contracts.Dtos.ShipmentOperationLogDto>> ListLogsAsync(string shipmentReference, string tenantKey, global::System.Threading.CancellationToken cancellationToken)
    {
        throw new global::System.NotImplementedException();
    }
    public global::System.Threading.Tasks.Task<global::Kargoyeri.Contracts.Dtos.ProviderSettingsDto> UpsertProviderSettingsAsync(string tenantKey, global::Kargoyeri.Contracts.Dtos.UpsertProviderSettingsRequest request, global::System.Threading.CancellationToken cancellationToken)
    {
        throw new global::System.NotImplementedException();
    }
    public global::System.Threading.Tasks.Task<global::Kargoyeri.Contracts.Dtos.ProviderSettingsDto> GetProviderSettingsAsync(string tenantKey, global::Kargoyeri.Contracts.Enums.CargoProviderTypeDto provider, global::System.Threading.CancellationToken cancellationToken)
    {
        throw new global::System.NotImplementedException();
    }
    public global::System.Threading.Tasks.Task<global::Kargoyeri.Contracts.Dtos.CreateShipmentResponse> SubmitShipmentAsync(string shipmentReference, string tenantKey, global::Kargoyeri.Contracts.Enums.CargoProviderTypeDto newProvider, global::System.Threading.CancellationToken cancellationToken)
    {
        throw new global::System.NotImplementedException();
    }
    public global::System.Threading.Tasks.Task<int> RefreshPendingShipmentsAsync(int take, global::System.Threading.CancellationToken cancellationToken, int maxRetryCount, int providerTimeoutSeconds, int batchItemDelayMs)
    {
        throw new global::System.NotImplementedException();
    }
    public global::System.Threading.Tasks.Task<global::Kargoyeri.Contracts.Dtos.ShipmentReportData> GetShipmentReportAsync(string tenantKey, global::System.DateTime fromUtc, global::System.DateTime toUtc, global::System.Threading.CancellationToken cancellationToken)
    {
        throw new global::System.NotImplementedException();
    }
}
