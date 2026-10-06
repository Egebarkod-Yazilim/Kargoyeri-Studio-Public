using System.Collections.Generic;
using Kargoyeri.Application.Abstractions.Providers;
using Kargoyeri.Domain.Enums;
using Microsoft.Extensions.Logging;

namespace Kargoyeri.Infrastructure.Providers;

internal sealed class YurticiCargoProvider : PreviewBackedCargoProviderBase
{
	public YurticiCargoProvider(IEnumerable<IProviderBlueprint> blueprints, ILogger<YurticiCargoProvider> logger)
		: base(CargoProviderType.Yurtici, blueprints, logger)
	{
	}
}
