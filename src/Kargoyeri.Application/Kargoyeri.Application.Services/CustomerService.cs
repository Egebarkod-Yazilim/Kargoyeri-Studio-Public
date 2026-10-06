using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Kargoyeri.Application.Abstractions.Persistence;
using Kargoyeri.Contracts.Dtos;
using Kargoyeri.Contracts.Enums;
using Kargoyeri.Domain.Entities;
using Kargoyeri.Domain.Enums;
using Kargoyeri.Domain.ValueObjects;

namespace Kargoyeri.Application.Services;

public sealed class CustomerService
{
	private readonly ICustomerRepository _customerRepository;

	private readonly IUnitOfWork _unitOfWork;

	public CustomerService(ICustomerRepository customerRepository, IUnitOfWork unitOfWork)
	{
		_customerRepository = customerRepository;
		_unitOfWork = unitOfWork;
	}

	public async Task<CustomerTenant> RequireTenantAsync(string tenantKey, CancellationToken cancellationToken)
	{
		CustomerTenant customer = (await _customerRepository.GetByTenantKeyAsync(tenantKey, cancellationToken)) ?? throw new InvalidOperationException("Tenant '" + tenantKey + "' was not found.");
		if (!customer.IsActive)
		{
			throw new InvalidOperationException("Tenant '" + tenantKey + "' is inactive.");
		}
		return customer;
	}

	public async Task<CustomerTenant> EnsureCustomerAsync(string customerCode, string? customerName, string? notificationWebhook, string? notificationEmail, string? notificationPhone, CancellationToken cancellationToken)
	{
		if (string.IsNullOrWhiteSpace(customerCode))
		{
			throw new InvalidOperationException("X-Customer-Code header is required.");
		}
		string normalizedCode = customerCode.Trim();
		CustomerTenant customer = (await _customerRepository.GetByTenantKeyAsync(normalizedCode, cancellationToken)) ?? new CustomerTenant
		{
			TenantKey = normalizedCode,
			CreatedAtUtc = DateTimeOffset.UtcNow
		};
		customer.Name = ((!string.IsNullOrWhiteSpace(customerName)) ? customerName.Trim() : (string.IsNullOrWhiteSpace(customer.Name) ? normalizedCode : customer.Name));
		customer.IsActive = true;
		customer.NotificationTargets = MergeNotificationTargets(customer.NotificationTargets, normalizedCode, notificationWebhook, notificationEmail, notificationPhone);
		customer.UpdatedAtUtc = DateTimeOffset.UtcNow;
		await _customerRepository.UpsertAsync(customer, cancellationToken);
		await _unitOfWork.CommitAsync(cancellationToken);
		return customer;
	}

	public async Task<CustomerProfileDto?> GetProfileAsync(string tenantKey, CancellationToken cancellationToken)
	{
		CustomerTenant customer = await _customerRepository.GetByTenantKeyAsync(tenantKey, cancellationToken);
		return (customer == null) ? null : Map(customer);
	}

	public async Task<CustomerProfileDto> UpsertAsync(string tenantKey, UpsertCustomerRequest request, CancellationToken cancellationToken)
	{
		CustomerTenant customer = (await _customerRepository.GetByTenantKeyAsync(tenantKey, cancellationToken)) ?? new CustomerTenant
		{
			TenantKey = tenantKey,
			CreatedAtUtc = DateTimeOffset.UtcNow
		};
		customer.Name = request.Name;
		customer.IsActive = request.IsActive;
		customer.AllowedProviders = request.AllowedProviders.Select((CargoProviderTypeDto x) => (CargoProviderType)x).Distinct().ToList();
		customer.NotificationTargets = request.NotificationTargets.Select((NotificationTargetDto x) => new NotificationTarget
		{
			Channel = (NotificationChannel)x.Channel,
			Address = x.Address,
			IsEnabled = x.IsEnabled
		}).ToList();
		customer.Metadata = request.Metadata;
		customer.UpdatedAtUtc = DateTimeOffset.UtcNow;
		if (!string.IsNullOrWhiteSpace(request.ApiKey))
		{
			customer.ApiKeyHash = ApiKeyHasher.Hash(request.ApiKey);
		}
		await _customerRepository.UpsertAsync(customer, cancellationToken);
		await _unitOfWork.CommitAsync(cancellationToken);
		return Map(customer);
	}

	public async Task<IReadOnlyCollection<CustomerProfileDto>> ListAllAsync(CancellationToken cancellationToken)
	{
		return (IReadOnlyCollection<CustomerProfileDto>)(object)(from x in (await _customerRepository.ListAsync(cancellationToken)).Select(Map)
			orderby x.Name
			select x).ToArray();
	}

	private static CustomerProfileDto Map(CustomerTenant customer)
	{
		return new CustomerProfileDto
		{
			TenantKey = customer.TenantKey,
			Name = customer.Name,
			IsActive = customer.IsActive,
			AllowedProviders = customer.AllowedProviders.Select((CargoProviderType x) => (CargoProviderTypeDto)x).ToList(),
			NotificationTargets = customer.NotificationTargets.Select((NotificationTarget x) => new NotificationTargetDto
			{
				Channel = (NotificationChannelDto)x.Channel,
				Address = x.Address,
				IsEnabled = x.IsEnabled
			}).ToList(),
			Metadata = customer.Metadata,
			UpdatedAtUtc = customer.UpdatedAtUtc
		};
	}

	private static List<NotificationTarget> MergeNotificationTargets(List<NotificationTarget> existingTargets, string customerCode, string? notificationWebhook, string? notificationEmail, string? notificationPhone)
	{
		List<NotificationTarget> list = existingTargets.Select((NotificationTarget x) => new NotificationTarget
		{
			Channel = x.Channel,
			Address = x.Address,
			IsEnabled = x.IsEnabled
		}).ToList();
		UpsertTarget(list, NotificationChannel.Internal, customerCode);
		if (!string.IsNullOrWhiteSpace(notificationWebhook))
		{
			UpsertTarget(list, NotificationChannel.Webhook, notificationWebhook.Trim());
		}
		if (!string.IsNullOrWhiteSpace(notificationEmail))
		{
			UpsertTarget(list, NotificationChannel.Email, notificationEmail.Trim());
		}
		if (!string.IsNullOrWhiteSpace(notificationPhone))
		{
			UpsertTarget(list, NotificationChannel.Sms, notificationPhone.Trim());
		}
		return list;
	}

	private static void UpsertTarget(List<NotificationTarget> targets, NotificationChannel channel, string address)
	{
		NotificationTarget notificationTarget = targets.FirstOrDefault((NotificationTarget x) => x.Channel == channel);
		if (notificationTarget == null)
		{
			targets.Add(new NotificationTarget
			{
				Channel = channel,
				Address = address,
				IsEnabled = true
			});
		}
		else
		{
			notificationTarget.Address = address;
			notificationTarget.IsEnabled = true;
		}
	}
}
