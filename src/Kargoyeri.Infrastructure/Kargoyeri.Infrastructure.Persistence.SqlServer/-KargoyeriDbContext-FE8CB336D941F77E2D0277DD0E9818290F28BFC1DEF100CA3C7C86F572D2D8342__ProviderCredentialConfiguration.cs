using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using Kargoyeri.Domain.Entities;
using Kargoyeri.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace Kargoyeri.Infrastructure.Persistence.SqlServer;

internal sealed class _003CKargoyeriDbContext_003EFE8CB336D941F77E2D0277DD0E9818290F28BFC1DEF100CA3C7C86F572D2D8342__ProviderCredentialConfiguration : IEntityTypeConfiguration<ProviderCredential>
{
	public void Configure(EntityTypeBuilder<ProviderCredential> b)
	{
		RelationalEntityTypeBuilderExtensions.ToTable<ProviderCredential>(b, "ProviderCredentials");
		b.HasKey((Expression<Func<ProviderCredential, object>>)((ProviderCredential x) => new { x.TenantKey, x.Provider }));
		b.Property<string>((Expression<Func<ProviderCredential, string>>)((ProviderCredential x) => x.TenantKey)).HasMaxLength(64).IsRequired(true);
		b.Property<CargoProviderType>((Expression<Func<ProviderCredential, CargoProviderType>>)((ProviderCredential x) => x.Provider)).HasConversion<string>().HasMaxLength(32)
			.IsRequired(true);
		b.Property<string>((Expression<Func<ProviderCredential, string>>)((ProviderCredential x) => x.ClientCode)).HasMaxLength(256);
		b.Property<string>((Expression<Func<ProviderCredential, string>>)((ProviderCredential x) => x.Username)).HasMaxLength(256);
		b.Property<string>((Expression<Func<ProviderCredential, string>>)((ProviderCredential x) => x.Password)).HasMaxLength(512);
		b.Property<string>((Expression<Func<ProviderCredential, string>>)((ProviderCredential x) => x.ApiKey)).HasMaxLength(1024);
		b.Property<string>((Expression<Func<ProviderCredential, string>>)((ProviderCredential x) => x.EndpointBase)).HasMaxLength(512);
		ValueConverter<Dictionary<string, string>, string> val = _003CKargoyeriDbContext_003EFE8CB336D941F77E2D0277DD0E9818290F28BFC1DEF100CA3C7C86F572D2D8342__JsonConvert.Converter<Dictionary<string, string>>();
		ValueComparer<Dictionary<string, string>> val2 = _003CKargoyeriDbContext_003EFE8CB336D941F77E2D0277DD0E9818290F28BFC1DEF100CA3C7C86F572D2D8342__JsonConvert.ListComparer<Dictionary<string, string>>();
		RelationalPropertyBuilderExtensions.HasColumnType<Dictionary<string, string>>(RelationalPropertyBuilderExtensions.HasColumnName<Dictionary<string, string>>(b.Property<Dictionary<string, string>>((Expression<Func<ProviderCredential, Dictionary<string, string>>>)((ProviderCredential x) => x.AdditionalSettings)), "AdditionalSettingsJson"), "nvarchar(max)").HasConversion<string>(val, (ValueComparer)(object)val2);
		b.Property<DateTimeOffset>((Expression<Func<ProviderCredential, DateTimeOffset>>)((ProviderCredential x) => x.UpdatedAtUtc)).IsRequired(true);
	}
}
