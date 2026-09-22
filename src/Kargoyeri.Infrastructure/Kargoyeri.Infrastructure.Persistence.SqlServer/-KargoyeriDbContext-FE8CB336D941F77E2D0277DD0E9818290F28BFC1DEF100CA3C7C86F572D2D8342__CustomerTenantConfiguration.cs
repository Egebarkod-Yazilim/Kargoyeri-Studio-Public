using System;
using System.Collections.Generic;
using System.Linq.Expressions;
using Kargoyeri.Domain.Entities;
using Kargoyeri.Domain.Enums;
using Kargoyeri.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace Kargoyeri.Infrastructure.Persistence.SqlServer;

internal sealed class _003CKargoyeriDbContext_003EFE8CB336D941F77E2D0277DD0E9818290F28BFC1DEF100CA3C7C86F572D2D8342__CustomerTenantConfiguration : IEntityTypeConfiguration<CustomerTenant>
{
	public void Configure(EntityTypeBuilder<CustomerTenant> b)
	{
		RelationalEntityTypeBuilderExtensions.ToTable<CustomerTenant>(b, "Customers");
		b.HasKey((Expression<Func<CustomerTenant, object>>)((CustomerTenant x) => x.TenantKey));
		b.Property<string>((Expression<Func<CustomerTenant, string>>)((CustomerTenant x) => x.TenantKey)).HasMaxLength(64).IsRequired(true);
		b.Property<string>((Expression<Func<CustomerTenant, string>>)((CustomerTenant x) => x.Name)).HasMaxLength(256).IsRequired(true);
		b.Property<string>((Expression<Func<CustomerTenant, string>>)((CustomerTenant x) => x.ApiKeyHash)).HasMaxLength(512);
		ValueConverter<List<CargoProviderType>, string> val = _003CKargoyeriDbContext_003EFE8CB336D941F77E2D0277DD0E9818290F28BFC1DEF100CA3C7C86F572D2D8342__JsonConvert.Converter<List<CargoProviderType>>();
		ValueComparer<List<CargoProviderType>> val2 = _003CKargoyeriDbContext_003EFE8CB336D941F77E2D0277DD0E9818290F28BFC1DEF100CA3C7C86F572D2D8342__JsonConvert.ListComparer<List<CargoProviderType>>();
		ValueConverter<List<NotificationTarget>, string> val3 = _003CKargoyeriDbContext_003EFE8CB336D941F77E2D0277DD0E9818290F28BFC1DEF100CA3C7C86F572D2D8342__JsonConvert.Converter<List<NotificationTarget>>();
		ValueComparer<List<NotificationTarget>> val4 = _003CKargoyeriDbContext_003EFE8CB336D941F77E2D0277DD0E9818290F28BFC1DEF100CA3C7C86F572D2D8342__JsonConvert.ListComparer<List<NotificationTarget>>();
		ValueConverter<Dictionary<string, string>, string> val5 = _003CKargoyeriDbContext_003EFE8CB336D941F77E2D0277DD0E9818290F28BFC1DEF100CA3C7C86F572D2D8342__JsonConvert.Converter<Dictionary<string, string>>();
		ValueComparer<Dictionary<string, string>> val6 = _003CKargoyeriDbContext_003EFE8CB336D941F77E2D0277DD0E9818290F28BFC1DEF100CA3C7C86F572D2D8342__JsonConvert.ListComparer<Dictionary<string, string>>();
		RelationalPropertyBuilderExtensions.HasColumnType<List<CargoProviderType>>(RelationalPropertyBuilderExtensions.HasColumnName<List<CargoProviderType>>(b.Property<List<CargoProviderType>>((Expression<Func<CustomerTenant, List<CargoProviderType>>>)((CustomerTenant x) => x.AllowedProviders)), "AllowedProvidersJson"), "nvarchar(max)").HasConversion<string>(val, (ValueComparer)(object)val2);
		RelationalPropertyBuilderExtensions.HasColumnType<List<NotificationTarget>>(RelationalPropertyBuilderExtensions.HasColumnName<List<NotificationTarget>>(b.Property<List<NotificationTarget>>((Expression<Func<CustomerTenant, List<NotificationTarget>>>)((CustomerTenant x) => x.NotificationTargets)), "NotificationTargetsJson"), "nvarchar(max)").HasConversion<string>(val3, (ValueComparer)(object)val4);
		RelationalPropertyBuilderExtensions.HasColumnType<Dictionary<string, string>>(RelationalPropertyBuilderExtensions.HasColumnName<Dictionary<string, string>>(b.Property<Dictionary<string, string>>((Expression<Func<CustomerTenant, Dictionary<string, string>>>)((CustomerTenant x) => x.Metadata)), "MetadataJson"), "nvarchar(max)").HasConversion<string>(val5, (ValueComparer)(object)val6);
		b.Property<DateTimeOffset>((Expression<Func<CustomerTenant, DateTimeOffset>>)((CustomerTenant x) => x.CreatedAtUtc)).IsRequired(true);
		b.Property<DateTimeOffset>((Expression<Func<CustomerTenant, DateTimeOffset>>)((CustomerTenant x) => x.UpdatedAtUtc)).IsRequired(true);
	}
}
