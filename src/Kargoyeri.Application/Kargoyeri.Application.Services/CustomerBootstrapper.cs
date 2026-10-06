using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using Kargoyeri.Application.Abstractions.Persistence;
using Kargoyeri.Application.Options;
using Kargoyeri.Contracts.Dtos;
using Kargoyeri.Domain.Entities;
using Kargoyeri.Domain.Enums;
using Kargoyeri.Domain.ValueObjects;

namespace Kargoyeri.Application.Services;

public sealed class CustomerBootstrapper
{
	private readonly ICustomerRepository _customerRepository;

	private readonly BootstrapCustomersOptions _options;

	private readonly IUnitOfWork _unitOfWork;

	public CustomerBootstrapper(ICustomerRepository customerRepository, BootstrapCustomersOptions options, IUnitOfWork unitOfWork)
	{
		_customerRepository = customerRepository;
		_options = options;
		_unitOfWork = unitOfWork;
	}

	public async Task EnsureSeededAsync(CancellationToken cancellationToken)
	{
		foreach (BootstrapCustomerDefinition customer in _options.Customers)
		{
			if (await _customerRepository.GetByTenantKeyAsync(customer.TenantKey, cancellationToken) == null)
			{
				CargoProviderType result;
				CustomerTenant entity = new CustomerTenant
				{
					TenantKey = customer.TenantKey,
					Name = customer.Name,
					IsActive = customer.IsActive,
					ApiKeyHash = ApiKeyHasher.Hash(customer.ApiKey),
					AllowedProviders = customer.AllowedProviders.Select((string x) => Enum.TryParse<CargoProviderType>(x, ignoreCase: true, out result) ? result : CargoProviderType.Sandbox).Distinct().ToList(),
					NotificationTargets = customer.NotificationTargets.Select((NotificationTargetDto x) => new NotificationTarget
					{
						Channel = (NotificationChannel)x.Channel,
						Address = x.Address,
						IsEnabled = x.IsEnabled
					}).ToList(),
					Metadata = customer.Metadata,
					CreatedAtUtc = DateTimeOffset.UtcNow,
					UpdatedAtUtc = DateTimeOffset.UtcNow
				};
				await _customerRepository.UpsertAsync(entity, cancellationToken);
			}
		}
		await _unitOfWork.CommitAsync(cancellationToken);
	}
}
