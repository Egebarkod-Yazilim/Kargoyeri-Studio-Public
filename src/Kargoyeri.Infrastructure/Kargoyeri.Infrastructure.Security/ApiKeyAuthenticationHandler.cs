using System.Collections.Generic;
using System.Security.Claims;
using System.Text.Encodings.Web;
using System.Threading.Tasks;
using Kargoyeri.Application.Abstractions.Persistence;
using Kargoyeri.Application.Services;
using Kargoyeri.Domain.Entities;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;

namespace Kargoyeri.Infrastructure.Security;

internal sealed class ApiKeyAuthenticationHandler : AuthenticationHandler<AuthenticationSchemeOptions>
{
	public const string SchemeName = "ApiKey";

	private readonly ICustomerRepository _customerRepository;

	public ApiKeyAuthenticationHandler(IOptionsMonitor<AuthenticationSchemeOptions> options, ILoggerFactory logger, UrlEncoder encoder, ISystemClock clock, ICustomerRepository customerRepository)
		: base(options, logger, encoder, clock)
	{
		_customerRepository = customerRepository;
	}

	protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
	{
		if (!base.Request.Headers.TryGetValue("X-Kargoyeri-ApiKey", out var values))
		{
			return AuthenticateResult.Fail("API key header was not provided.");
		}
		string providedKey = values.ToString();
		if (string.IsNullOrWhiteSpace(providedKey))
		{
			return AuthenticateResult.Fail("API key is empty.");
		}
		string hashedKey = ApiKeyHasher.Hash(providedKey);
		CustomerTenant customer = await _customerRepository.GetByApiKeyHashAsync(hashedKey, base.Context.RequestAborted);
		if (customer == null || !customer.IsActive)
		{
			return AuthenticateResult.Fail("API key is invalid or customer is inactive.");
		}
		List<Claim> claims = new List<Claim>
		{
			new Claim("http://schemas.xmlsoap.org/ws/2005/05/identity/claims/name", customer.Name),
			new Claim("tenant", customer.TenantKey)
		};
		ClaimsIdentity identity = new ClaimsIdentity(claims, "ApiKey");
		return AuthenticateResult.Success(new AuthenticationTicket(new ClaimsPrincipal(identity), "ApiKey"));
	}
}
