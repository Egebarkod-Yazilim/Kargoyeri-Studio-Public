using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Cryptography;
using System.Text;
using System.Threading;
using System.Threading.Tasks;
using Kargoyeri.Application.Abstractions.Persistence;
using Kargoyeri.Contracts.Dtos;
using Kargoyeri.Contracts.Enums;
using Kargoyeri.Domain.Entities;
using Kargoyeri.Domain.Enums;
using Kargoyeri.Domain.ValueObjects;

namespace Kargoyeri.Application.Services;

public sealed class TenantOnboardingService
{
	private readonly ICustomerRepository _customerRepository;

	private readonly IUnitOfWork _unitOfWork;

	public TenantOnboardingService(ICustomerRepository customerRepository, IUnitOfWork unitOfWork)
	{
		_customerRepository = customerRepository;
		_unitOfWork = unitOfWork;
	}

	public async Task<TenantOnboardingResponse> CreateAsync(TenantOnboardingRequest request, CancellationToken cancellationToken)
	{
		if (string.IsNullOrWhiteSpace(request.Name))
		{
			throw new InvalidOperationException("Customer name is required.");
		}
		string tenantKey = await ResolveUniqueTenantKeyAsync(request, cancellationToken);
		string apiKey = GenerateApiKey(tenantKey);
		CustomerTenant customer = new CustomerTenant
		{
			TenantKey = tenantKey,
			Name = request.Name.Trim(),
			IsActive = request.IsActive,
			ApiKeyHash = ApiKeyHasher.Hash(apiKey),
			AllowedProviders = request.AllowedProviders.Select((CargoProviderTypeDto x) => (CargoProviderType)x).Distinct().ToList(),
			NotificationTargets = request.NotificationTargets.Select((NotificationTargetDto x) => new NotificationTarget
			{
				Channel = (NotificationChannel)x.Channel,
				Address = x.Address,
				IsEnabled = x.IsEnabled
			}).ToList(),
			Metadata = request.Metadata,
			CreatedAtUtc = DateTimeOffset.UtcNow,
			UpdatedAtUtc = DateTimeOffset.UtcNow
		};
		await _customerRepository.UpsertAsync(customer, cancellationToken);
		await _unitOfWork.CommitAsync(cancellationToken);
		return new TenantOnboardingResponse
		{
			TenantKey = tenantKey,
			ApiKey = apiKey,
			Customer = Map(customer),
			UsefulLinks = new Dictionary<string, string>
			{
				["Swagger"] = "/swagger",
				["IntegrationGuide"] = "/docs/integration",
				["ProviderCatalog"] = "/docs/providers",
				["TenantProfile"] = "/api/me"
			}
		};
	}

	public async Task<IReadOnlyCollection<CustomerProfileDto>> ListAsync(CancellationToken cancellationToken)
	{
		return (IReadOnlyCollection<CustomerProfileDto>)(object)(await _customerRepository.ListAsync(cancellationToken)).OrderBy((CustomerTenant x) => x.TenantKey).Select(Map).ToArray();
	}

	private async Task<string> ResolveUniqueTenantKeyAsync(TenantOnboardingRequest request, CancellationToken cancellationToken)
	{
		string preferred = (string.IsNullOrWhiteSpace(request.TenantKey) ? Slugify(request.Name) : Slugify(request.TenantKey));
		if (string.IsNullOrWhiteSpace(preferred))
		{
			preferred = "tenant";
		}
		string candidate = preferred;
		int suffix = 1;
		while (await _customerRepository.GetByTenantKeyAsync(candidate, cancellationToken) != null)
		{
			suffix++;
			candidate = $"{preferred}-{suffix}";
		}
		return candidate;
	}

	private static string GenerateApiKey(string tenantKey)
	{
		byte[] bytes = RandomNumberGenerator.GetBytes(24);
		return "ky_" + tenantKey + "_" + Convert.ToHexString(bytes).ToLowerInvariant();
	}

	private static string Slugify(string value)
	{
		StringBuilder stringBuilder = new StringBuilder();
		bool flag = false;
		string text = value.Trim().ToLowerInvariant();
		foreach (char c in text)
		{
			if (char.IsLetterOrDigit(c))
			{
				stringBuilder.Append(c);
				flag = false;
			}
			else if (!flag)
			{
				stringBuilder.Append('-');
				flag = true;
			}
		}
		return stringBuilder.ToString().Trim('-');
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
}
