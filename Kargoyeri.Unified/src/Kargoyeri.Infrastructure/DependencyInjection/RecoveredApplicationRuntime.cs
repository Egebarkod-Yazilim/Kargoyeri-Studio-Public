using System.Collections.Concurrent;
using Kargoyeri.Application.Abstractions.Persistence;
using Kargoyeri.Application.Abstractions.Providers;
using Kargoyeri.Application.Services;
using Kargoyeri.Domain.Entities;
using Kargoyeri.Domain.Enums;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;

namespace Kargoyeri.Infrastructure.DependencyInjection;

internal static class RecoveredApplicationRuntime
{
    public static IServiceCollection AddRecoveredApplicationRuntime(this IServiceCollection services)
    {
        services.TryAddSingleton<RecoveredApplicationRuntimeMarker>();
        services.TryAddSingleton<ICustomerRepository, InMemoryCustomerRepository>();
        services.TryAddSingleton<IShipmentRepository, InMemoryShipmentRepository>();
        services.TryAddSingleton<IProviderSettingsRepository, InMemoryProviderSettingsRepository>();
        services.TryAddSingleton<IOperationLogRepository, InMemoryOperationLogRepository>();
        services.TryAddSingleton<INotificationRepository, InMemoryNotificationRepository>();
        services.TryAddSingleton<IUnitOfWork, NoopUnitOfWork>();
        services.TryAddSingleton<ICargoProviderResolver, NullCargoProviderResolver>();
        services.TryAddSingleton<INotificationDispatcher, NullNotificationDispatcher>();

        services.TryAddScoped<CustomerService>();
        services.TryAddScoped<NotificationService>();
        services.TryAddScoped<CargoOrchestrator>();
        services.TryAddScoped<ProviderCatalogService>();
        services.TryAddScoped<ProviderIntegrationStatusService>();
        services.TryAddScoped<TenantOnboardingService>();
        services.TryAddSingleton<ApiDocumentationService>();

        return services;
    }

    private sealed class NoopUnitOfWork : IUnitOfWork
    {
        public Task CommitAsync(CancellationToken cancellationToken) => Task.CompletedTask;
    }

    private sealed class InMemoryCustomerRepository : ICustomerRepository
    {
        private readonly ConcurrentDictionary<string, CustomerTenant> _customers = new(StringComparer.OrdinalIgnoreCase);

        public Task UpsertAsync(CustomerTenant customer, CancellationToken cancellationToken)
        {
            _customers[customer.TenantKey ?? string.Empty] = Clone(customer);
            return Task.CompletedTask;
        }

        public Task<CustomerTenant> GetByTenantKeyAsync(string tenantKey, CancellationToken cancellationToken)
        {
            return Task.FromResult(_customers.TryGetValue(tenantKey, out var customer)
                ? Clone(customer)
                : CreateCustomer(tenantKey));
        }

        public Task<CustomerTenant> GetByApiKeyHashAsync(string apiKeyHash, CancellationToken cancellationToken)
        {
            var customer = _customers.Values.FirstOrDefault(x => string.Equals(x.ApiKeyHash, apiKeyHash, StringComparison.Ordinal));
            return Task.FromResult(customer is null ? CreateCustomer("default") : Clone(customer));
        }

        public Task<IReadOnlyCollection<CustomerTenant>> ListAsync(CancellationToken cancellationToken)
        {
            IReadOnlyCollection<CustomerTenant> customers = _customers.Values.Select(Clone).ToArray();
            return Task.FromResult(customers);
        }

        private static CustomerTenant CreateCustomer(string? tenantKey)
        {
            return new CustomerTenant
            {
                TenantKey = string.IsNullOrWhiteSpace(tenantKey) ? "default" : tenantKey,
                Name = "Recovered Workspace",
                IsActive = true,
                ApiKeyHash = string.Empty,
                AllowedProviders = new List<CargoProviderType> { CargoProviderType.Sandbox },
                NotificationTargets = new(),
                Metadata = new(StringComparer.OrdinalIgnoreCase),
                CreatedAtUtc = DateTimeOffset.UtcNow,
                UpdatedAtUtc = DateTimeOffset.UtcNow
            };
        }

        private static CustomerTenant Clone(CustomerTenant customer)
        {
            return new CustomerTenant
            {
                TenantKey = customer.TenantKey,
                Name = customer.Name,
                IsActive = customer.IsActive,
                ApiKeyHash = customer.ApiKeyHash,
                AllowedProviders = customer.AllowedProviders?.ToList() ?? new(),
                NotificationTargets = customer.NotificationTargets?.ToList() ?? new(),
                Metadata = customer.Metadata is null
                    ? new(StringComparer.OrdinalIgnoreCase)
                    : new Dictionary<string, string>(customer.Metadata, StringComparer.OrdinalIgnoreCase),
                CreatedAtUtc = customer.CreatedAtUtc,
                UpdatedAtUtc = customer.UpdatedAtUtc
            };
        }
    }

    private sealed class InMemoryShipmentRepository : IShipmentRepository
    {
        private readonly ConcurrentDictionary<string, CargoShipment> _shipments = new(StringComparer.OrdinalIgnoreCase);

        public Task UpsertAsync(CargoShipment shipment, CancellationToken cancellationToken)
        {
            _shipments[shipment.ShipmentReference ?? Guid.NewGuid().ToString("N")] = Clone(shipment);
            return Task.CompletedTask;
        }

        public Task<CargoShipment> GetByReferenceAsync(string shipmentReference, CancellationToken cancellationToken)
        {
            return Task.FromResult(_shipments.TryGetValue(shipmentReference, out var shipment)
                ? Clone(shipment)
                : CreateShipment(shipmentReference, "default"));
        }

        public Task<CargoShipment> GetByTrackingNumberAsync(string trackingNumber, CancellationToken cancellationToken)
        {
            var shipment = _shipments.Values.FirstOrDefault(x => string.Equals(x.TrackingNumber, trackingNumber, StringComparison.OrdinalIgnoreCase));
            return Task.FromResult(shipment is null ? CreateShipment(trackingNumber, "default") : Clone(shipment));
        }

        public Task<CargoShipment> GetByIdempotencyKeyAsync(string tenantKey, string idempotencyKey, CancellationToken cancellationToken)
        {
            var shipment = _shipments.Values.FirstOrDefault(x =>
                string.Equals(x.TenantKey, tenantKey, StringComparison.OrdinalIgnoreCase) &&
                string.Equals(x.IdempotencyKey, idempotencyKey, StringComparison.OrdinalIgnoreCase));

            return Task.FromResult(shipment is null ? CreateShipment(idempotencyKey, tenantKey) : Clone(shipment));
        }

        public Task<IReadOnlyCollection<CargoShipment>> ListAsync(string tenantKey, CancellationToken cancellationToken)
        {
            IReadOnlyCollection<CargoShipment> shipments = _shipments.Values
                .Where(x => string.Equals(x.TenantKey, tenantKey, StringComparison.OrdinalIgnoreCase))
                .Select(Clone)
                .ToArray();
            return Task.FromResult(shipments);
        }

        public Task<(IReadOnlyCollection<CargoShipment>, int)> ListPagedAsync(
            string tenantKey,
            string search,
            ShipmentStatus? status,
            CargoProviderType? provider,
            int page,
            int pageSize,
            CancellationToken cancellationToken)
        {
            var query = _shipments.Values
                .Where(x => string.Equals(x.TenantKey, tenantKey, StringComparison.OrdinalIgnoreCase));

            if (!string.IsNullOrWhiteSpace(search))
            {
                query = query.Where(x =>
                    (x.ShipmentReference?.Contains(search, StringComparison.OrdinalIgnoreCase) ?? false) ||
                    (x.OrderReference?.Contains(search, StringComparison.OrdinalIgnoreCase) ?? false) ||
                    (x.TrackingNumber?.Contains(search, StringComparison.OrdinalIgnoreCase) ?? false));
            }

            if (status.HasValue)
            {
                query = query.Where(x => x.Status == status.Value);
            }

            if (provider.HasValue)
            {
                query = query.Where(x => x.Provider == provider.Value);
            }

            var total = query.Count();
            var items = query
                .Skip(Math.Max(page - 1, 0) * Math.Max(pageSize, 1))
                .Take(Math.Max(pageSize, 1))
                .Select(Clone)
                .ToArray();

            return Task.FromResult(((IReadOnlyCollection<CargoShipment>)items, total));
        }

        public Task<IReadOnlyCollection<CargoShipment>> GetRefreshCandidatesAsync(int take, CancellationToken cancellationToken)
        {
            IReadOnlyCollection<CargoShipment> shipments = _shipments.Values
                .OrderByDescending(x => x.UpdatedAtUtc)
                .Take(Math.Max(take, 0))
                .Select(Clone)
                .ToArray();
            return Task.FromResult(shipments);
        }

        public Task<IReadOnlyCollection<CargoShipment>> ListByDateRangeAsync(string tenantKey, DateTime fromUtc, DateTime toUtc, CancellationToken cancellationToken)
        {
            IReadOnlyCollection<CargoShipment> shipments = _shipments.Values
                .Where(x =>
                    string.Equals(x.TenantKey, tenantKey, StringComparison.OrdinalIgnoreCase) &&
                    x.CreatedAtUtc.UtcDateTime >= fromUtc &&
                    x.CreatedAtUtc.UtcDateTime <= toUtc)
                .Select(Clone)
                .ToArray();

            return Task.FromResult(shipments);
        }

        private static CargoShipment CreateShipment(string? referenceSeed, string? tenantKey)
        {
            return new CargoShipment
            {
                Id = Guid.NewGuid(),
                ShipmentReference = string.IsNullOrWhiteSpace(referenceSeed) ? $"REC-{Guid.NewGuid():N}" : referenceSeed,
                TenantKey = string.IsNullOrWhiteSpace(tenantKey) ? "default" : tenantKey,
                OrderReference = string.Empty,
                ClientShipmentReference = string.Empty,
                IdempotencyKey = string.Empty,
                Provider = CargoProviderType.Sandbox,
                Source = IntegrationSourceType.Manual,
                SourceChannel = null,
                SourceChannelCode = string.Empty,
                Status = ShipmentStatus.Pending,
                CollectionAmount = null,
                CurrencyCode = "TRY",
                Sender = new(),
                Recipient = new(),
                Packages = new(),
                Metadata = new(StringComparer.OrdinalIgnoreCase),
                TrackingNumber = string.Empty,
                LabelUrl = string.Empty,
                LabelContentBase64 = string.Empty,
                ProviderMessage = string.Empty,
                ErrorMessage = string.Empty,
                RetryCount = 0,
                CreatedAtUtc = DateTimeOffset.UtcNow,
                UpdatedAtUtc = DateTimeOffset.UtcNow,
                LastStatusCheckAtUtc = null
            };
        }

        private static CargoShipment Clone(CargoShipment shipment)
        {
            return new CargoShipment
            {
                Id = shipment.Id,
                ShipmentReference = shipment.ShipmentReference,
                TenantKey = shipment.TenantKey,
                OrderReference = shipment.OrderReference,
                ClientShipmentReference = shipment.ClientShipmentReference,
                IdempotencyKey = shipment.IdempotencyKey,
                Provider = shipment.Provider,
                Source = shipment.Source,
                SourceChannel = shipment.SourceChannel,
                SourceChannelCode = shipment.SourceChannelCode,
                Status = shipment.Status,
                CollectionAmount = shipment.CollectionAmount,
                CurrencyCode = shipment.CurrencyCode,
                Sender = shipment.Sender,
                Recipient = shipment.Recipient,
                Packages = shipment.Packages?.ToList() ?? new(),
                Metadata = shipment.Metadata is null
                    ? new(StringComparer.OrdinalIgnoreCase)
                    : new Dictionary<string, string>(shipment.Metadata, StringComparer.OrdinalIgnoreCase),
                TrackingNumber = shipment.TrackingNumber,
                LabelUrl = shipment.LabelUrl,
                LabelContentBase64 = shipment.LabelContentBase64,
                ProviderMessage = shipment.ProviderMessage,
                ErrorMessage = shipment.ErrorMessage,
                RetryCount = shipment.RetryCount,
                CreatedAtUtc = shipment.CreatedAtUtc,
                UpdatedAtUtc = shipment.UpdatedAtUtc,
                LastStatusCheckAtUtc = shipment.LastStatusCheckAtUtc
            };
        }
    }

    private sealed class InMemoryProviderSettingsRepository : IProviderSettingsRepository
    {
        private readonly ConcurrentDictionary<string, ProviderCredential> _settings = new(StringComparer.OrdinalIgnoreCase);

        public Task UpsertAsync(ProviderCredential settings, CancellationToken cancellationToken)
        {
            _settings[Key(settings.TenantKey, settings.Provider)] = Clone(settings);
            return Task.CompletedTask;
        }

        public Task<ProviderCredential> GetAsync(string tenantKey, CargoProviderType provider, CancellationToken cancellationToken)
        {
            return Task.FromResult(_settings.TryGetValue(Key(tenantKey, provider), out var settings)
                ? Clone(settings)
                : new ProviderCredential
                {
                    TenantKey = tenantKey,
                    Provider = provider,
                    IsEnabled = false,
                    ClientCode = string.Empty,
                    Username = string.Empty,
                    Password = string.Empty,
                    ApiKey = string.Empty,
                    EndpointBase = string.Empty,
                    AdditionalSettings = new(StringComparer.OrdinalIgnoreCase),
                    UpdatedAtUtc = DateTimeOffset.UtcNow
                });
        }

        private static string Key(string tenantKey, CargoProviderType provider) => $"{tenantKey}:{provider}";

        private static ProviderCredential Clone(ProviderCredential credential)
        {
            return new ProviderCredential
            {
                TenantKey = credential.TenantKey,
                Provider = credential.Provider,
                IsEnabled = credential.IsEnabled,
                ClientCode = credential.ClientCode,
                Username = credential.Username,
                Password = credential.Password,
                ApiKey = credential.ApiKey,
                EndpointBase = credential.EndpointBase,
                AdditionalSettings = credential.AdditionalSettings is null
                    ? new(StringComparer.OrdinalIgnoreCase)
                    : new Dictionary<string, string>(credential.AdditionalSettings, StringComparer.OrdinalIgnoreCase),
                UpdatedAtUtc = credential.UpdatedAtUtc
            };
        }
    }

    private sealed class InMemoryOperationLogRepository : IOperationLogRepository
    {
        private readonly ConcurrentDictionary<string, List<ShipmentOperationLog>> _logs = new(StringComparer.OrdinalIgnoreCase);

        public Task AppendAsync(ShipmentOperationLog log, CancellationToken cancellationToken)
        {
            var key = $"{log.TenantKey}:{log.ShipmentReference}";
            var bucket = _logs.GetOrAdd(key, _ => new List<ShipmentOperationLog>());
            lock (bucket)
            {
                bucket.Add(Clone(log));
            }

            return Task.CompletedTask;
        }

        public Task<IReadOnlyCollection<ShipmentOperationLog>> ListByShipmentReferenceAsync(
            string tenantKey,
            string shipmentReference,
            CancellationToken cancellationToken)
        {
            var key = $"{tenantKey}:{shipmentReference}";
            if (!_logs.TryGetValue(key, out var bucket))
            {
                return Task.FromResult((IReadOnlyCollection<ShipmentOperationLog>)Array.Empty<ShipmentOperationLog>());
            }

            lock (bucket)
            {
                return Task.FromResult((IReadOnlyCollection<ShipmentOperationLog>)bucket.Select(Clone).ToArray());
            }
        }

        private static ShipmentOperationLog Clone(ShipmentOperationLog log)
        {
            return new ShipmentOperationLog
            {
                Id = log.Id,
                TenantKey = log.TenantKey,
                ShipmentReference = log.ShipmentReference,
                Operation = log.Operation,
                Severity = log.Severity,
                Message = log.Message,
                ProviderPayload = log.ProviderPayload,
                OccurredAtUtc = log.OccurredAtUtc
            };
        }
    }

    private sealed class InMemoryNotificationRepository : INotificationRepository
    {
        private readonly ConcurrentDictionary<Guid, NotificationMessage> _notifications = new();

        public Task AppendAsync(NotificationMessage notification, CancellationToken cancellationToken)
        {
            var item = Clone(notification);
            if (item.Id == Guid.Empty)
            {
                item.Id = Guid.NewGuid();
            }

            _notifications[item.Id] = item;
            return Task.CompletedTask;
        }

        public Task UpdateAsync(NotificationMessage notification, CancellationToken cancellationToken)
        {
            _notifications[notification.Id] = Clone(notification);
            return Task.CompletedTask;
        }

        public Task<NotificationMessage> GetByIdAsync(Guid id, CancellationToken cancellationToken)
        {
            return Task.FromResult(_notifications.TryGetValue(id, out var notification)
                ? Clone(notification)
                : new NotificationMessage
                {
                    Id = id,
                    TenantKey = "default",
                    ShipmentReference = string.Empty,
                    EventType = NotificationEventType.ShipmentStatusChanged,
                    Channel = NotificationChannel.Internal,
                    Status = NotificationDeliveryStatus.Queued,
                    Address = string.Empty,
                    Subject = string.Empty,
                    Body = string.Empty,
                    ErrorMessage = string.Empty,
                    CreatedAtUtc = DateTimeOffset.UtcNow
                });
        }

        public Task<IReadOnlyCollection<NotificationMessage>> ListByTenantAsync(string tenantKey, CancellationToken cancellationToken)
        {
            IReadOnlyCollection<NotificationMessage> notifications = _notifications.Values
                .Where(x => string.Equals(x.TenantKey, tenantKey, StringComparison.OrdinalIgnoreCase))
                .Select(Clone)
                .ToArray();

            return Task.FromResult(notifications);
        }

        public Task<IReadOnlyCollection<NotificationMessage>> ListByShipmentAsync(string tenantKey, string shipmentReference, CancellationToken cancellationToken)
        {
            IReadOnlyCollection<NotificationMessage> notifications = _notifications.Values
                .Where(x =>
                    string.Equals(x.TenantKey, tenantKey, StringComparison.OrdinalIgnoreCase) &&
                    string.Equals(x.ShipmentReference, shipmentReference, StringComparison.OrdinalIgnoreCase))
                .Select(Clone)
                .ToArray();

            return Task.FromResult(notifications);
        }

        private static NotificationMessage Clone(NotificationMessage notification)
        {
            return new NotificationMessage
            {
                Id = notification.Id,
                TenantKey = notification.TenantKey,
                ShipmentReference = notification.ShipmentReference,
                EventType = notification.EventType,
                Channel = notification.Channel,
                Status = notification.Status,
                Address = notification.Address,
                Subject = notification.Subject,
                Body = notification.Body,
                ErrorMessage = notification.ErrorMessage,
                CreatedAtUtc = notification.CreatedAtUtc,
                DeliveredAtUtc = notification.DeliveredAtUtc
            };
        }
    }

    private sealed class NullNotificationDispatcher : INotificationDispatcher
    {
        public Task DispatchAsync(CustomerTenant customer, NotificationMessage notification, CancellationToken cancellationToken)
        {
            return Task.CompletedTask;
        }
    }

    private sealed class NullCargoProviderResolver : ICargoProviderResolver
    {
        private readonly ICargoProvider _provider = new NullCargoProvider();

        public ICargoProvider Resolve(CargoProviderType provider) => _provider;
    }

    private sealed class NullCargoProvider : ICargoProvider
    {
        public CargoProviderType SupportedProvider => CargoProviderType.Sandbox;

        public Task<ProviderShipmentResult> CreateShipmentAsync(CargoShipment shipment, ProviderCredential credential, CancellationToken cancellationToken)
        {
            return Task.FromResult(CreateResult("Recovered placeholder provider accepted the request."));
        }

        public Task<ProviderShipmentResult> CancelShipmentAsync(CargoShipment shipment, ProviderCredential credential, string reason, CancellationToken cancellationToken)
        {
            return Task.FromResult(CreateResult("Recovered placeholder provider cancelled the request."));
        }

        public Task<ProviderShipmentResult> RefreshStatusAsync(CargoShipment shipment, ProviderCredential credential, CancellationToken cancellationToken)
        {
            return Task.FromResult(CreateResult("Recovered placeholder provider returned a synthetic status refresh."));
        }

        private static ProviderShipmentResult CreateResult(string message)
        {
            return new ProviderShipmentResult
            {
                Success = true,
                Status = ShipmentStatus.Pending,
                TrackingNumber = string.Empty,
                LabelUrl = string.Empty,
                LabelContentBase64 = string.Empty,
                Message = message,
                RawResponse = string.Empty
            };
        }
    }
}

public sealed class RecoveredApplicationRuntimeMarker;
