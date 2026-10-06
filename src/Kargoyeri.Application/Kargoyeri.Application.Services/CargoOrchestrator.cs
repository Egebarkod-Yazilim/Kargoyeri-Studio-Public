using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Kargoyeri.Application.Abstractions.Persistence;
using Kargoyeri.Application.Abstractions.Providers;
using Kargoyeri.Contracts.Dtos;
using Kargoyeri.Contracts.Enums;
using Kargoyeri.Domain.Entities;
using Kargoyeri.Domain.Enums;

namespace Kargoyeri.Application.Services;

public sealed class CargoOrchestrator
{
	private readonly IShipmentRepository _shipmentRepository;

	private readonly IOperationLogRepository _operationLogRepository;

	private readonly IProviderSettingsRepository _providerSettingsRepository;

	private readonly ICargoProviderResolver _providerResolver;

	private readonly CustomerService _customerService;

	private readonly NotificationService _notificationService;

	private readonly IUnitOfWork _unitOfWork;

	public CargoOrchestrator(IShipmentRepository shipmentRepository, IOperationLogRepository operationLogRepository, IProviderSettingsRepository providerSettingsRepository, ICargoProviderResolver providerResolver, CustomerService customerService, NotificationService notificationService, IUnitOfWork unitOfWork)
	{
		_shipmentRepository = shipmentRepository;
		_operationLogRepository = operationLogRepository;
		_providerSettingsRepository = providerSettingsRepository;
		_providerResolver = providerResolver;
		_customerService = customerService;
		_notificationService = notificationService;
		_unitOfWork = unitOfWork;
	}

	public async Task<CreateShipmentResponse> CreateShipmentAsync(CreateShipmentRequest request, CancellationToken cancellationToken)
	{
		ValidateCreateRequest(request);
		CustomerTenant customer = await _customerService.RequireTenantAsync(request.TenantKey, cancellationToken);
		CargoProviderType provider = ShipmentMapping.MapProvider(request.Provider);
		if (!customer.CanUseProvider(provider))
		{
			throw new InvalidOperationException($"Tenant '{customer.TenantKey}' is not allowed to use provider '{provider}'.");
		}
		if (!string.IsNullOrWhiteSpace(request.IdempotencyKey))
		{
			CargoShipment existing = await _shipmentRepository.GetByIdempotencyKeyAsync(request.TenantKey, request.IdempotencyKey, cancellationToken);
			if (existing != null)
			{
				await AppendLogAsync(existing.TenantKey, existing.ShipmentReference, CargoOperationType.CreateShipment, LogSeverity.Information, "Idempotent replay detected; existing shipment returned.", null, cancellationToken);
				return MapCreateResponse(existing, replay: true, "Idempotent replay.");
			}
		}
		CargoShipment shipment = ShipmentMapping.BuildTransientShipment(request);
		await _shipmentRepository.UpsertAsync(shipment, cancellationToken);
		ProviderCredential providerSettings = await _providerSettingsRepository.GetAsync(customer.TenantKey, shipment.Provider, cancellationToken);
		if (providerSettings == null || !providerSettings.IsEnabled)
		{
			shipment.ApplyProviderFailure($"{shipment.Provider} provider is not configured or disabled for tenant '{customer.TenantKey}'.");
			await _shipmentRepository.UpsertAsync(shipment, cancellationToken);
			await AppendLogAsync(shipment.TenantKey, shipment.ShipmentReference, CargoOperationType.CreateShipment, LogSeverity.Error, shipment.ErrorMessage ?? "Provider configuration missing.", null, cancellationToken);
			await _unitOfWork.CommitAsync(cancellationToken);
			await _notificationService.PublishAsync(customer, shipment, NotificationEventType.ProviderError, shipment.ErrorMessage ?? "Provider configuration missing.", cancellationToken);
			return MapCreateResponse(shipment, replay: false, shipment.ErrorMessage);
		}
		ICargoProvider resolvedProvider = _providerResolver.Resolve(shipment.Provider);
		ProviderShipmentResult result = await resolvedProvider.CreateShipmentAsync(shipment, providerSettings, cancellationToken);
		await ApplyProviderResultAsync(customer, shipment, CargoOperationType.CreateShipment, result, cancellationToken);
		return MapCreateResponse(shipment, replay: false, result.Message);
	}

	public async Task<CancelShipmentResponse> CancelShipmentAsync(string shipmentReference, string tenantKey, string? reason, CancellationToken cancellationToken)
	{
		CustomerTenant customer = await _customerService.RequireTenantAsync(tenantKey, cancellationToken);
		CargoShipment shipment = await RequireShipmentAsync(shipmentReference, tenantKey, cancellationToken);
		ICargoProvider provider = _providerResolver.Resolve(shipment.Provider);
		ProviderShipmentResult result = await provider.CancelShipmentAsync(shipment, await _providerSettingsRepository.GetAsync(customer.TenantKey, shipment.Provider, cancellationToken), reason, cancellationToken);
		await ApplyProviderResultAsync(customer, shipment, CargoOperationType.CancelShipment, result, cancellationToken);
		return new CancelShipmentResponse
		{
			ShipmentReference = shipment.ShipmentReference,
			Status = ShipmentMapping.MapStatus(shipment.Status),
			Message = (result.Message ?? "Shipment updated.")
		};
	}

	public async Task<ShipmentDetailResponse?> GetShipmentAsync(string shipmentReference, string tenantKey, CancellationToken cancellationToken)
	{
		CargoShipment shipment = await _shipmentRepository.GetByReferenceAsync(shipmentReference, cancellationToken);
		if (shipment == null || !string.Equals(shipment.TenantKey, tenantKey, StringComparison.OrdinalIgnoreCase))
		{
			return null;
		}
		return MapDetailResponse(shipment);
	}

	public async Task<ShipmentDetailResponse?> GetShipmentByTrackingNumberAsync(string trackingNumber, CancellationToken cancellationToken)
	{
		if (string.IsNullOrWhiteSpace(trackingNumber))
		{
			return null;
		}
		CargoShipment shipment = await _shipmentRepository.GetByTrackingNumberAsync(trackingNumber.Trim(), cancellationToken);
		return (shipment == null) ? null : MapDetailResponse(shipment);
	}

	public async Task<ShipmentDetailResponse> RefreshShipmentAsync(string shipmentReference, string tenantKey, CancellationToken cancellationToken)
	{
		CustomerTenant customer = await _customerService.RequireTenantAsync(tenantKey, cancellationToken);
		CargoShipment shipment = await RequireShipmentAsync(shipmentReference, tenantKey, cancellationToken);
		shipment.RegisterRefreshAttempt();
		ProviderCredential settings = await _providerSettingsRepository.GetAsync(customer.TenantKey, shipment.Provider, cancellationToken);
		if (settings == null || !settings.IsEnabled)
		{
			shipment.ApplyProviderFailure($"{shipment.Provider} provider is not configured or disabled for tenant '{customer.TenantKey}'.");
			await _shipmentRepository.UpsertAsync(shipment, cancellationToken);
			await AppendLogAsync(shipment.TenantKey, shipment.ShipmentReference, CargoOperationType.RefreshShipmentStatus, LogSeverity.Warning, shipment.ErrorMessage ?? "Provider configuration missing.", null, cancellationToken);
			await _unitOfWork.CommitAsync(cancellationToken);
			await _notificationService.PublishAsync(customer, shipment, NotificationEventType.ProviderError, shipment.ErrorMessage ?? "Provider configuration missing.", cancellationToken);
			return MapDetailResponse(shipment);
		}
		ICargoProvider provider = _providerResolver.Resolve(shipment.Provider);
		await ApplyProviderResultAsync(previousStatus: shipment.Status, customer: customer, shipment: shipment, operation: CargoOperationType.RefreshShipmentStatus, result: await provider.RefreshStatusAsync(shipment, settings, cancellationToken), cancellationToken: cancellationToken);
		return MapDetailResponse(shipment);
	}

	public async Task<IReadOnlyCollection<ShipmentListItemResponse>> ListShipmentsAsync(string tenantKey, CancellationToken cancellationToken)
	{
		return (IReadOnlyCollection<ShipmentListItemResponse>)(object)(await _shipmentRepository.ListAsync(tenantKey, cancellationToken)).OrderByDescending((CargoShipment x) => x.CreatedAtUtc).Select(MapListResponse).ToArray();
	}

	public async Task<(IReadOnlyCollection<ShipmentListItemResponse> Items, int TotalCount)> ListShipmentsPagedAsync(string tenantKey, string? search, string? statusFilter, string? providerFilter, int page, int pageSize, CancellationToken cancellationToken)
	{
		ShipmentStatus? status = null;
		if (!string.IsNullOrWhiteSpace(statusFilter) && Enum.TryParse<ShipmentStatus>(statusFilter, ignoreCase: true, out var parsedStatus))
		{
			status = parsedStatus;
		}
		CargoProviderType? provider = null;
		if (!string.IsNullOrWhiteSpace(providerFilter) && Enum.TryParse<CargoProviderType>(providerFilter, ignoreCase: true, out var parsedProvider))
		{
			provider = parsedProvider;
		}
		(IReadOnlyCollection<CargoShipment> Items, int TotalCount) tuple = await _shipmentRepository.ListPagedAsync(tenantKey, search, status, provider, page, pageSize, cancellationToken);
		IReadOnlyCollection<CargoShipment> shipments;
		(shipments, _) = tuple;
		return new ValueTuple<IReadOnlyCollection<ShipmentListItemResponse>, int>(item2: tuple.TotalCount, item1: (IReadOnlyCollection<ShipmentListItemResponse>)(object)shipments.Select(MapListResponse).ToArray());
	}

	public async Task<IReadOnlyCollection<ShipmentOperationLogDto>> ListLogsAsync(string shipmentReference, string tenantKey, CancellationToken cancellationToken)
	{
		await RequireShipmentAsync(shipmentReference, tenantKey, cancellationToken);
		return (IReadOnlyCollection<ShipmentOperationLogDto>)(object)(from x in await _operationLogRepository.ListByShipmentReferenceAsync(tenantKey, shipmentReference, cancellationToken)
			orderby x.OccurredAtUtc descending
			select new ShipmentOperationLogDto
			{
				ShipmentReference = x.ShipmentReference,
				Operation = x.Operation.ToString(),
				Severity = x.Severity.ToString(),
				Message = x.Message,
				ProviderPayload = x.ProviderPayload,
				OccurredAtUtc = x.OccurredAtUtc
			}).ToArray();
	}

	public async Task<ProviderSettingsDto> UpsertProviderSettingsAsync(string tenantKey, UpsertProviderSettingsRequest request, CancellationToken cancellationToken)
	{
		await _customerService.RequireTenantAsync(tenantKey, cancellationToken);
		ProviderCredential settings = new ProviderCredential
		{
			TenantKey = tenantKey,
			Provider = ShipmentMapping.MapProvider(request.Provider),
			IsEnabled = request.IsEnabled,
			ClientCode = request.ClientCode,
			Username = request.Username,
			Password = request.Password,
			ApiKey = request.ApiKey,
			EndpointBase = request.EndpointBase,
			AdditionalSettings = request.AdditionalSettings,
			UpdatedAtUtc = DateTimeOffset.UtcNow
		};
		await _providerSettingsRepository.UpsertAsync(settings, cancellationToken);
		return MapProviderSettings(settings);
	}

	public async Task<ProviderSettingsDto?> GetProviderSettingsAsync(string tenantKey, CargoProviderTypeDto provider, CancellationToken cancellationToken)
	{
		await _customerService.RequireTenantAsync(tenantKey, cancellationToken);
		ProviderCredential settings = await _providerSettingsRepository.GetAsync(tenantKey, ShipmentMapping.MapProvider(provider), cancellationToken);
		return (settings == null) ? null : MapProviderSettings(settings);
	}

	public async Task<CreateShipmentResponse> SubmitShipmentAsync(string shipmentReference, string tenantKey, CargoProviderTypeDto newProvider, CancellationToken cancellationToken)
	{
		CustomerTenant customer = await _customerService.RequireTenantAsync(tenantKey, cancellationToken);
		CargoShipment shipment = await RequireShipmentAsync(shipmentReference, tenantKey, cancellationToken);
		if (shipment.Status != 0 && shipment.Status != ShipmentStatus.Failed)
		{
			throw new InvalidOperationException($"Gonderi '{shipmentReference}' durumu '{shipment.Status}' — yalnizca Pending veya Failed gonderiler yeniden gonderilebilir.");
		}
		CargoProviderType provider = ShipmentMapping.MapProvider(newProvider);
		if (!customer.CanUseProvider(provider))
		{
			throw new InvalidOperationException($"Musteri '{customer.TenantKey}' bu firmaya izinli degil: '{provider}'.");
		}
		shipment.Provider = provider;
		shipment.ErrorMessage = null;
		shipment.Status = ShipmentStatus.Pending;
		shipment.RetryCount = 0;
		shipment.UpdatedAtUtc = DateTimeOffset.UtcNow;
		ProviderCredential providerSettings = await _providerSettingsRepository.GetAsync(customer.TenantKey, provider, cancellationToken);
		if (providerSettings == null || !providerSettings.IsEnabled)
		{
			shipment.ApplyProviderFailure($"{provider} firmasi yapilandirilmamis veya devre disi.");
			await _shipmentRepository.UpsertAsync(shipment, cancellationToken);
			await AppendLogAsync(shipment.TenantKey, shipment.ShipmentReference, CargoOperationType.CreateShipment, LogSeverity.Error, shipment.ErrorMessage ?? "Provider configuration missing.", null, cancellationToken);
			await _unitOfWork.CommitAsync(cancellationToken);
			return MapCreateResponse(shipment, replay: false, shipment.ErrorMessage);
		}
		ICargoProvider resolvedProvider = _providerResolver.Resolve(provider);
		ProviderShipmentResult result = await resolvedProvider.CreateShipmentAsync(shipment, providerSettings, cancellationToken);
		await ApplyProviderResultAsync(customer, shipment, CargoOperationType.CreateShipment, result, cancellationToken);
		return MapCreateResponse(shipment, replay: false, result.Message);
	}

	public async Task<int> RefreshPendingShipmentsAsync(int take, CancellationToken cancellationToken, int maxRetryCount = 10, int providerTimeoutSeconds = 30, int batchItemDelayMs = 200)
	{
		IReadOnlyCollection<CargoShipment> candidates = await _shipmentRepository.GetRefreshCandidatesAsync(take, cancellationToken);
		int processed = 0;
		foreach (CargoShipment shipment in candidates)
		{
			if (cancellationToken.IsCancellationRequested)
			{
				break;
			}
			if (shipment.RetryCount >= maxRetryCount)
			{
				string deadMsg = $"Dead-letter: {shipment.Provider} gonderisi {shipment.RetryCount} denemeden sonra vazgecildi.";
				shipment.ApplyProviderFailure(deadMsg);
				await _shipmentRepository.UpsertAsync(shipment, cancellationToken);
				await AppendLogAsync(shipment.TenantKey, shipment.ShipmentReference, CargoOperationType.RefreshShipmentStatus, LogSeverity.Error, deadMsg, null, cancellationToken);
				await _unitOfWork.CommitAsync(cancellationToken);
				processed++;
				continue;
			}
			CustomerTenant customer = await _customerService.RequireTenantAsync(shipment.TenantKey, cancellationToken);
			shipment.RegisterRefreshAttempt();
			ProviderCredential settings = await _providerSettingsRepository.GetAsync(customer.TenantKey, shipment.Provider, cancellationToken);
			if (settings == null || !settings.IsEnabled)
			{
				shipment.ApplyProviderFailure($"{shipment.Provider} provider is not configured or disabled for tenant '{customer.TenantKey}'.");
				await _shipmentRepository.UpsertAsync(shipment, cancellationToken);
				await AppendLogAsync(shipment.TenantKey, shipment.ShipmentReference, CargoOperationType.RefreshShipmentStatus, LogSeverity.Warning, shipment.ErrorMessage ?? "Provider configuration missing.", null, cancellationToken);
				await _unitOfWork.CommitAsync(cancellationToken);
				await _notificationService.PublishAsync(customer, shipment, NotificationEventType.ProviderError, shipment.ErrorMessage ?? "Provider configuration missing.", cancellationToken);
				processed++;
				continue;
			}
			using CancellationTokenSource timeoutCts = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
			timeoutCts.CancelAfter(TimeSpan.FromSeconds(providerTimeoutSeconds));
			try
			{
				ICargoProvider provider = _providerResolver.Resolve(shipment.Provider);
				await ApplyProviderResultAsync(previousStatus: shipment.Status, customer: customer, shipment: shipment, operation: CargoOperationType.RefreshShipmentStatus, result: await provider.RefreshStatusAsync(shipment, settings, timeoutCts.Token), cancellationToken: cancellationToken);
			}
			catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
			{
				string timeoutMsg = $"{shipment.Provider} provider timeout ({providerTimeoutSeconds}s). Deneme {shipment.RetryCount}/{maxRetryCount}.";
				await _shipmentRepository.UpsertAsync(shipment, cancellationToken);
				await AppendLogAsync(shipment.TenantKey, shipment.ShipmentReference, CargoOperationType.RefreshShipmentStatus, LogSeverity.Warning, timeoutMsg, null, cancellationToken);
				await _unitOfWork.CommitAsync(cancellationToken);
			}
			processed++;
			if (batchItemDelayMs > 0 && !cancellationToken.IsCancellationRequested)
			{
				await Task.Delay(batchItemDelayMs, cancellationToken).ConfigureAwait(continueOnCapturedContext: false);
			}
		}
		return processed;
	}

	private async Task ApplyProviderResultAsync(CustomerTenant customer, CargoShipment shipment, CargoOperationType operation, ProviderShipmentResult result, CancellationToken cancellationToken, ShipmentStatus? previousStatus = null)
	{
		if (result.Success)
		{
			if (operation == CargoOperationType.CancelShipment)
			{
				shipment.MarkCancelled(result.Message);
				await _notificationService.PublishAsync(customer, shipment, NotificationEventType.ShipmentCancelled, result.Message ?? "Shipment cancelled.", cancellationToken);
			}
			else
			{
				shipment.ApplyProviderSuccess(result.Status, result.TrackingNumber, result.LabelUrl, result.LabelContentBase64, result.Message);
				NotificationEventType eventType = (previousStatus.HasValue ? ((previousStatus != shipment.Status) ? NotificationEventType.ShipmentStatusChanged : NotificationEventType.ShipmentCreated) : NotificationEventType.ShipmentCreated);
				await _notificationService.PublishAsync(customer, shipment, eventType, result.Message ?? "Shipment processed.", cancellationToken);
			}
		}
		else
		{
			shipment.ApplyProviderFailure(result.Message);
			await _notificationService.PublishAsync(customer, shipment, NotificationEventType.ShipmentFailed, result.Message ?? "Shipment failed.", cancellationToken);
		}
		await _shipmentRepository.UpsertAsync(shipment, cancellationToken);
		await AppendLogAsync(shipment.TenantKey, shipment.ShipmentReference, operation, (!result.Success) ? LogSeverity.Error : LogSeverity.Information, result.Message ?? "Provider operation completed.", result.RawResponse, cancellationToken);
		await _unitOfWork.CommitAsync(cancellationToken);
	}

	private async Task AppendLogAsync(string tenantKey, string shipmentReference, CargoOperationType operation, LogSeverity severity, string message, string? providerPayload, CancellationToken cancellationToken)
	{
		await _operationLogRepository.AppendAsync(new ShipmentOperationLog
		{
			TenantKey = tenantKey,
			ShipmentReference = shipmentReference,
			Operation = operation,
			Severity = severity,
			Message = message,
			ProviderPayload = providerPayload
		}, cancellationToken);
	}

	private async Task<CargoShipment> RequireShipmentAsync(string shipmentReference, string tenantKey, CancellationToken cancellationToken)
	{
		CargoShipment shipment = (await _shipmentRepository.GetByReferenceAsync(shipmentReference, cancellationToken)) ?? throw new InvalidOperationException("Shipment '" + shipmentReference + "' was not found.");
		if (!string.Equals(shipment.TenantKey, tenantKey, StringComparison.OrdinalIgnoreCase))
		{
			throw new InvalidOperationException("Shipment does not belong to the current tenant.");
		}
		return shipment;
	}

	private static void ValidateCreateRequest(CreateShipmentRequest request)
	{
		if (string.IsNullOrWhiteSpace(request.TenantKey))
		{
			throw new InvalidOperationException("CustomerCode is required.");
		}
		if (string.IsNullOrWhiteSpace(request.OrderReference))
		{
			throw new InvalidOperationException("OrderReference is required.");
		}
		if (request.Packages.Count == 0)
		{
			throw new InvalidOperationException("At least one package must be provided.");
		}
	}

	private static ProviderSettingsDto MapProviderSettings(ProviderCredential settings)
	{
		return new ProviderSettingsDto
		{
			Provider = ShipmentMapping.MapProvider(settings.Provider),
			IsEnabled = settings.IsEnabled,
			ClientCode = settings.ClientCode,
			Username = settings.Username,
			PasswordMasked = Mask(settings.Password),
			ApiKeyMasked = Mask(settings.ApiKey),
			EndpointBase = settings.EndpointBase,
			AdditionalSettings = settings.AdditionalSettings,
			UpdatedAtUtc = settings.UpdatedAtUtc
		};
	}

	private static ShipmentListItemResponse MapListResponse(CargoShipment shipment)
	{
		return new ShipmentListItemResponse
		{
			ShipmentReference = shipment.ShipmentReference,
			CustomerCode = shipment.TenantKey,
			OrderReference = shipment.OrderReference,
			Provider = ShipmentMapping.MapProvider(shipment.Provider),
			Status = ShipmentMapping.MapStatus(shipment.Status),
			TrackingNumber = shipment.TrackingNumber,
			CreatedAtUtc = shipment.CreatedAtUtc,
			UpdatedAtUtc = shipment.UpdatedAtUtc,
			SourceChannel = (shipment.SourceChannel.HasValue ? new OrderSourceChannelDto?((OrderSourceChannelDto)shipment.SourceChannel.Value) : null),
			SourceChannelCode = shipment.SourceChannelCode
		};
	}

	private static ShipmentDetailResponse MapDetailResponse(CargoShipment shipment)
	{
		ShipmentDetailResponse shipmentDetailResponse = new ShipmentDetailResponse();
		shipmentDetailResponse.ShipmentReference = shipment.ShipmentReference;
		shipmentDetailResponse.CustomerCode = shipment.TenantKey;
		shipmentDetailResponse.OrderReference = shipment.OrderReference;
		shipmentDetailResponse.ClientShipmentReference = shipment.ClientShipmentReference;
		shipmentDetailResponse.IdempotencyKey = shipment.IdempotencyKey;
		shipmentDetailResponse.Provider = ShipmentMapping.MapProvider(shipment.Provider);
		shipmentDetailResponse.Source = ShipmentMapping.MapSource(shipment.Source);
		shipmentDetailResponse.Status = ShipmentMapping.MapStatus(shipment.Status);
		shipmentDetailResponse.TrackingNumber = shipment.TrackingNumber;
		shipmentDetailResponse.LabelUrl = shipment.LabelUrl;
		shipmentDetailResponse.ProviderMessage = shipment.ProviderMessage;
		shipmentDetailResponse.ErrorMessage = shipment.ErrorMessage;
		shipmentDetailResponse.CollectionAmount = shipment.CollectionAmount;
		shipmentDetailResponse.CurrencyCode = shipment.CurrencyCode;
		shipmentDetailResponse.Sender = ShipmentMapping.MapAddress(shipment.Sender);
		shipmentDetailResponse.Recipient = ShipmentMapping.MapAddress(shipment.Recipient);
		shipmentDetailResponse.Packages = shipment.Packages.Select(ShipmentMapping.MapPackage).ToList();
		shipmentDetailResponse.Metadata = shipment.Metadata;
		shipmentDetailResponse.CreatedAtUtc = shipment.CreatedAtUtc;
		shipmentDetailResponse.UpdatedAtUtc = shipment.UpdatedAtUtc;
		shipmentDetailResponse.LastStatusCheckAtUtc = shipment.LastStatusCheckAtUtc;
		shipmentDetailResponse.RetryCount = shipment.RetryCount;
		return shipmentDetailResponse;
	}

	private static CreateShipmentResponse MapCreateResponse(CargoShipment shipment, bool replay, string? message)
	{
		return new CreateShipmentResponse
		{
			ShipmentReference = shipment.ShipmentReference,
			Provider = ShipmentMapping.MapProvider(shipment.Provider),
			Status = ShipmentMapping.MapStatus(shipment.Status),
			TrackingNumber = shipment.TrackingNumber,
			LabelUrl = shipment.LabelUrl,
			IsIdempotentReplay = replay,
			Message = (message ?? shipment.ProviderMessage ?? shipment.ErrorMessage)
		};
	}

	public async Task<ShipmentReportData> GetShipmentReportAsync(string tenantKey, DateTime fromUtc, DateTime toUtc, CancellationToken cancellationToken)
	{
		IReadOnlyCollection<CargoShipment> shipments = await _shipmentRepository.ListByDateRangeAsync(tenantKey, fromUtc, toUtc, cancellationToken);
		Dictionary<string, int> byStatus = (from x in shipments
			group x by ShipmentMapping.MapStatus(x.Status)).ToDictionary((IGrouping<ShipmentStatusDto, CargoShipment> g) => g.Key.ToString(), (IGrouping<ShipmentStatusDto, CargoShipment> g) => g.Count());
		ProviderReportRow[] byProvider = (from x in shipments
			group x by ShipmentMapping.MapProvider(x.Provider) into g
			select new ProviderReportRow
			{
				Provider = g.Key.ToString(),
				Total = g.Count(),
				Delivered = g.Count((CargoShipment x) => x.Status == ShipmentStatus.Delivered),
				Failed = g.Count((CargoShipment x) => x.Status == ShipmentStatus.Failed || x.Status == ShipmentStatus.Cancelled),
				InTransit = g.Count((CargoShipment x) => x.Status == ShipmentStatus.InTransit || x.Status == ShipmentStatus.ProviderAccepted),
				Pending = g.Count((CargoShipment x) => x.Status == ShipmentStatus.Pending)
			} into x
			orderby x.Total descending
			select x).ToArray();
		return new ShipmentReportData
		{
			FromUtc = fromUtc,
			ToUtc = toUtc,
			TotalCount = shipments.Count,
			ByStatus = byStatus,
			ByProvider = (IReadOnlyCollection<ProviderReportRow>)(object)byProvider
		};
	}

	private static string? Mask(string? value)
	{
		if (string.IsNullOrWhiteSpace(value))
		{
			return value;
		}
		string result;
		if (value.Length > 4)
		{
			string text = new string('*', value.Length - 4);
			int length = value.Length;
			int num = length - 4;
			result = text + value.Substring(num, length - num);
		}
		else
		{
			result = new string('*', value.Length);
		}
		return result;
	}
}
