using System.Collections.Generic;

namespace Kargoyeri.Application.Options;

public sealed class BootstrapCustomersOptions
{
	public List<BootstrapCustomerDefinition> Customers { get; set; } = new List<BootstrapCustomerDefinition>();

}
