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

internal sealed class _003CKargoyeriDbContext_003EFE8CB336D941F77E2D0277DD0E9818290F28BFC1DEF100CA3C7C86F572D2D8342__CargoShipmentConfiguration : IEntityTypeConfiguration<CargoShipment>
{
	public void Configure(EntityTypeBuilder<CargoShipment> b)
	{
		RelationalEntityTypeBuilderExtensions.ToTable<CargoShipment>(b, "Shipments");
		b.HasKey((Expression<Func<CargoShipment, object>>)((CargoShipment x) => x.Id));
		b.Property<string>((Expression<Func<CargoShipment, string>>)((CargoShipment x) => x.ShipmentReference)).HasMaxLength(64).IsRequired(true);
		b.HasIndex((Expression<Func<CargoShipment, object>>)((CargoShipment x) => x.ShipmentReference)).IsUnique(true);
		b.Property<string>((Expression<Func<CargoShipment, string>>)((CargoShipment x) => x.TenantKey)).HasMaxLength(64).IsRequired(true);
		b.HasIndex((Expression<Func<CargoShipment, object>>)((CargoShipment x) => x.TenantKey));
		b.Property<string>((Expression<Func<CargoShipment, string>>)((CargoShipment x) => x.OrderReference)).HasMaxLength(128).IsRequired(true);
		b.Property<string>((Expression<Func<CargoShipment, string>>)((CargoShipment x) => x.ClientShipmentReference)).HasMaxLength(128);
		b.Property<string>((Expression<Func<CargoShipment, string>>)((CargoShipment x) => x.IdempotencyKey)).HasMaxLength(256);
		b.HasIndex((Expression<Func<CargoShipment, object>>)((CargoShipment x) => new { x.TenantKey, x.IdempotencyKey }));
		b.Property<CargoProviderType>((Expression<Func<CargoShipment, CargoProviderType>>)((CargoShipment x) => x.Provider)).HasConversion<string>().HasMaxLength(32);
		b.Property<IntegrationSourceType>((Expression<Func<CargoShipment, IntegrationSourceType>>)((CargoShipment x) => x.Source)).HasConversion<string>().HasMaxLength(32);
		b.Property<ShipmentStatus>((Expression<Func<CargoShipment, ShipmentStatus>>)((CargoShipment x) => x.Status)).HasConversion<string>().HasMaxLength(32);
		b.Property<OrderSourceChannel?>((Expression<Func<CargoShipment, OrderSourceChannel?>>)((CargoShipment x) => x.SourceChannel)).HasConversion<string>().HasMaxLength(32);
		b.Property<string>((Expression<Func<CargoShipment, string>>)((CargoShipment x) => x.SourceChannelCode)).HasMaxLength(64);
		b.HasIndex((Expression<Func<CargoShipment, object>>)((CargoShipment x) => new { x.TenantKey, x.SourceChannel }));
		RelationalPropertyBuilderExtensions.HasColumnType<decimal?>(b.Property<decimal?>((Expression<Func<CargoShipment, decimal?>>)((CargoShipment x) => x.CollectionAmount)), "decimal(18,4)");
		RelationalPropertyBuilderExtensions.HasDefaultValue<string>(b.Property<string>((Expression<Func<CargoShipment, string>>)((CargoShipment x) => x.CurrencyCode)).HasMaxLength(8), (object)"TRY");
		b.Property<string>((Expression<Func<CargoShipment, string>>)((CargoShipment x) => x.TrackingNumber)).HasMaxLength(128);
		RelationalIndexBuilderExtensions.HasFilter<CargoShipment>(b.HasIndex((Expression<Func<CargoShipment, object>>)((CargoShipment x) => x.TrackingNumber)), "[TrackingNumber] IS NOT NULL");
		b.Property<string>((Expression<Func<CargoShipment, string>>)((CargoShipment x) => x.LabelUrl)).HasMaxLength(2048);
		RelationalPropertyBuilderExtensions.HasColumnType<string>(b.Property<string>((Expression<Func<CargoShipment, string>>)((CargoShipment x) => x.LabelContentBase64)), "nvarchar(max)");
		b.Property<string>((Expression<Func<CargoShipment, string>>)((CargoShipment x) => x.ProviderMessage)).HasMaxLength(1024);
		b.Property<string>((Expression<Func<CargoShipment, string>>)((CargoShipment x) => x.ErrorMessage)).HasMaxLength(2048);
		b.OwnsOne<AddressInfo>((Expression<Func<CargoShipment, AddressInfo>>)((CargoShipment x) => x.Sender), (Action<OwnedNavigationBuilder<CargoShipment, AddressInfo>>)delegate(OwnedNavigationBuilder<CargoShipment, AddressInfo> owned)
		{
			RelationalPropertyBuilderExtensions.HasColumnName<string>(owned.Property<string>((Expression<Func<AddressInfo, string>>)((AddressInfo a) => a.Name)), "SenderName").HasMaxLength(256).IsRequired(true);
			RelationalPropertyBuilderExtensions.HasColumnName<string>(owned.Property<string>((Expression<Func<AddressInfo, string>>)((AddressInfo a) => a.CompanyName)), "SenderCompanyName").HasMaxLength(256);
			RelationalPropertyBuilderExtensions.HasColumnName<string>(owned.Property<string>((Expression<Func<AddressInfo, string>>)((AddressInfo a) => a.Phone)), "SenderPhone").HasMaxLength(32);
			RelationalPropertyBuilderExtensions.HasColumnName<string>(owned.Property<string>((Expression<Func<AddressInfo, string>>)((AddressInfo a) => a.Email)), "SenderEmail").HasMaxLength(256);
			RelationalPropertyBuilderExtensions.HasDefaultValue<string>(RelationalPropertyBuilderExtensions.HasColumnName<string>(owned.Property<string>((Expression<Func<AddressInfo, string>>)((AddressInfo a) => a.CountryCode)), "SenderCountryCode").HasMaxLength(8), (object)"TR");
			RelationalPropertyBuilderExtensions.HasColumnName<string>(owned.Property<string>((Expression<Func<AddressInfo, string>>)((AddressInfo a) => a.City)), "SenderCity").HasMaxLength(128).IsRequired(true);
			RelationalPropertyBuilderExtensions.HasColumnName<string>(owned.Property<string>((Expression<Func<AddressInfo, string>>)((AddressInfo a) => a.District)), "SenderDistrict").HasMaxLength(128);
			RelationalPropertyBuilderExtensions.HasColumnName<string>(owned.Property<string>((Expression<Func<AddressInfo, string>>)((AddressInfo a) => a.PostalCode)), "SenderPostalCode").HasMaxLength(16);
			RelationalPropertyBuilderExtensions.HasColumnName<string>(owned.Property<string>((Expression<Func<AddressInfo, string>>)((AddressInfo a) => a.AddressLine1)), "SenderAddressLine1").HasMaxLength(512).IsRequired(true);
			RelationalPropertyBuilderExtensions.HasColumnName<string>(owned.Property<string>((Expression<Func<AddressInfo, string>>)((AddressInfo a) => a.AddressLine2)), "SenderAddressLine2").HasMaxLength(512);
		});
		b.OwnsOne<AddressInfo>((Expression<Func<CargoShipment, AddressInfo>>)((CargoShipment x) => x.Recipient), (Action<OwnedNavigationBuilder<CargoShipment, AddressInfo>>)delegate(OwnedNavigationBuilder<CargoShipment, AddressInfo> owned)
		{
			RelationalPropertyBuilderExtensions.HasColumnName<string>(owned.Property<string>((Expression<Func<AddressInfo, string>>)((AddressInfo a) => a.Name)), "RecipientName").HasMaxLength(256).IsRequired(true);
			RelationalPropertyBuilderExtensions.HasColumnName<string>(owned.Property<string>((Expression<Func<AddressInfo, string>>)((AddressInfo a) => a.CompanyName)), "RecipientCompanyName").HasMaxLength(256);
			RelationalPropertyBuilderExtensions.HasColumnName<string>(owned.Property<string>((Expression<Func<AddressInfo, string>>)((AddressInfo a) => a.Phone)), "RecipientPhone").HasMaxLength(32);
			RelationalPropertyBuilderExtensions.HasColumnName<string>(owned.Property<string>((Expression<Func<AddressInfo, string>>)((AddressInfo a) => a.Email)), "RecipientEmail").HasMaxLength(256);
			RelationalPropertyBuilderExtensions.HasDefaultValue<string>(RelationalPropertyBuilderExtensions.HasColumnName<string>(owned.Property<string>((Expression<Func<AddressInfo, string>>)((AddressInfo a) => a.CountryCode)), "RecipientCountryCode").HasMaxLength(8), (object)"TR");
			RelationalPropertyBuilderExtensions.HasColumnName<string>(owned.Property<string>((Expression<Func<AddressInfo, string>>)((AddressInfo a) => a.City)), "RecipientCity").HasMaxLength(128).IsRequired(true);
			RelationalPropertyBuilderExtensions.HasColumnName<string>(owned.Property<string>((Expression<Func<AddressInfo, string>>)((AddressInfo a) => a.District)), "RecipientDistrict").HasMaxLength(128);
			RelationalPropertyBuilderExtensions.HasColumnName<string>(owned.Property<string>((Expression<Func<AddressInfo, string>>)((AddressInfo a) => a.PostalCode)), "RecipientPostalCode").HasMaxLength(16);
			RelationalPropertyBuilderExtensions.HasColumnName<string>(owned.Property<string>((Expression<Func<AddressInfo, string>>)((AddressInfo a) => a.AddressLine1)), "RecipientAddressLine1").HasMaxLength(512).IsRequired(true);
			RelationalPropertyBuilderExtensions.HasColumnName<string>(owned.Property<string>((Expression<Func<AddressInfo, string>>)((AddressInfo a) => a.AddressLine2)), "RecipientAddressLine2").HasMaxLength(512);
		});
		ValueConverter<List<PackageInfo>, string> val = _003CKargoyeriDbContext_003EFE8CB336D941F77E2D0277DD0E9818290F28BFC1DEF100CA3C7C86F572D2D8342__JsonConvert.Converter<List<PackageInfo>>();
		ValueComparer<List<PackageInfo>> val2 = _003CKargoyeriDbContext_003EFE8CB336D941F77E2D0277DD0E9818290F28BFC1DEF100CA3C7C86F572D2D8342__JsonConvert.ListComparer<List<PackageInfo>>();
		ValueConverter<Dictionary<string, string>, string> val3 = _003CKargoyeriDbContext_003EFE8CB336D941F77E2D0277DD0E9818290F28BFC1DEF100CA3C7C86F572D2D8342__JsonConvert.Converter<Dictionary<string, string>>();
		ValueComparer<Dictionary<string, string>> val4 = _003CKargoyeriDbContext_003EFE8CB336D941F77E2D0277DD0E9818290F28BFC1DEF100CA3C7C86F572D2D8342__JsonConvert.ListComparer<Dictionary<string, string>>();
		RelationalPropertyBuilderExtensions.HasColumnType<List<PackageInfo>>(RelationalPropertyBuilderExtensions.HasColumnName<List<PackageInfo>>(b.Property<List<PackageInfo>>((Expression<Func<CargoShipment, List<PackageInfo>>>)((CargoShipment x) => x.Packages)), "PackagesJson"), "nvarchar(max)").HasConversion<string>(val, (ValueComparer)(object)val2);
		RelationalPropertyBuilderExtensions.HasColumnType<Dictionary<string, string>>(RelationalPropertyBuilderExtensions.HasColumnName<Dictionary<string, string>>(b.Property<Dictionary<string, string>>((Expression<Func<CargoShipment, Dictionary<string, string>>>)((CargoShipment x) => x.Metadata)), "MetadataJson"), "nvarchar(max)").HasConversion<string>(val3, (ValueComparer)(object)val4);
		b.Property<DateTimeOffset>((Expression<Func<CargoShipment, DateTimeOffset>>)((CargoShipment x) => x.CreatedAtUtc)).IsRequired(true);
		b.Property<DateTimeOffset>((Expression<Func<CargoShipment, DateTimeOffset>>)((CargoShipment x) => x.UpdatedAtUtc)).IsRequired(true);
		b.HasIndex((Expression<Func<CargoShipment, object>>)((CargoShipment x) => x.UpdatedAtUtc));
	}
}
