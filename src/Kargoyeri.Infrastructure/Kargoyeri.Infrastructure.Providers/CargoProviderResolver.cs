using System;
using System.Collections.Generic;
using System.Linq;
using Kargoyeri.Application.Abstractions.Providers;
using Kargoyeri.Domain.Enums;

namespace Kargoyeri.Infrastructure.Providers;

internal sealed class CargoProviderResolver : ICargoProviderResolver
{
	private readonly IReadOnlyDictionary<CargoProviderType, ICargoProvider> _providers;

	public CargoProviderResolver(IEnumerable<ICargoProvider> providers)
	{
		_providers = providers.ToDictionary((ICargoProvider x) => x.SupportedProvider, (ICargoProvider x) => x);
	}

	public ICargoProvider Resolve(CargoProviderType provider)
	{
		if (_providers.TryGetValue(provider, out ICargoProvider value))
		{
			return value;
		}
		throw new InvalidOperationException($"Provider '{provider}' is not registered.");
	}
}
