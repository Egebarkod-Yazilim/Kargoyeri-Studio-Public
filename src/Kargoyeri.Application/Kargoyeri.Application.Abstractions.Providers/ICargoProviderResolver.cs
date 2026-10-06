using Kargoyeri.Domain.Enums;

namespace Kargoyeri.Application.Abstractions.Providers;

public interface ICargoProviderResolver
{
	ICargoProvider Resolve(CargoProviderType provider);
}
