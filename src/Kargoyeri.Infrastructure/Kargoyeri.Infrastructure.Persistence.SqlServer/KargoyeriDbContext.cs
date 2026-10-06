using Kargoyeri.Domain.Entities;
using Microsoft.EntityFrameworkCore;

namespace Kargoyeri.Infrastructure.Persistence.SqlServer;

public sealed class KargoyeriDbContext : DbContext
{
	public DbSet<CargoShipment> Shipments { get; set; } = null;


	public DbSet<CustomerTenant> Customers { get; set; } = null;


	public DbSet<NotificationMessage> Notifications { get; set; } = null;


	public DbSet<ShipmentOperationLog> OperationLogs { get; set; } = null;


	public DbSet<ProviderCredential> ProviderCredentials { get; set; } = null;


	public KargoyeriDbContext(DbContextOptions<KargoyeriDbContext> options)
		: base((DbContextOptions)(object)options)
	{
	}

	protected override void OnModelCreating(ModelBuilder modelBuilder)
	{
		modelBuilder.Entity<CargoShipment>(entity =>
		{
			entity.ToTable("Shipments");
			entity.HasKey(x => x.Id);
			entity.HasIndex(x => x.ShipmentReference).IsUnique();
			entity.HasIndex(x => x.TenantKey);
			entity.HasIndex(x => x.TrackingNumber).HasFilter("[TrackingNumber] IS NOT NULL");
			entity.HasIndex(x => x.UpdatedAtUtc);
			entity.HasIndex(x => new { x.TenantKey, x.IdempotencyKey });
			entity.HasIndex(x => new { x.TenantKey, x.SourceChannel });
			entity.Property(x => x.ShipmentReference).HasMaxLength(64).IsRequired();
			entity.Property(x => x.TenantKey).HasMaxLength(64).IsRequired();
			entity.Property(x => x.OrderReference).HasMaxLength(128).IsRequired();
			entity.Property(x => x.ClientShipmentReference).HasMaxLength(128);
			entity.Property(x => x.IdempotencyKey).HasMaxLength(256);
			entity.Property(x => x.Provider).HasConversion<string>().HasMaxLength(32);
			entity.Property(x => x.Source).HasConversion<string>().HasMaxLength(32);
			entity.Property(x => x.SourceChannel).HasConversion<string>().HasMaxLength(32);
			entity.Property(x => x.Status).HasConversion<string>().HasMaxLength(32);
			entity.Property(x => x.SourceChannelCode).HasMaxLength(64);
			entity.Property(x => x.CollectionAmount).HasColumnType("decimal(18,4)");
			entity.Property(x => x.CurrencyCode).HasMaxLength(8).HasDefaultValue("TRY");
			entity.Property(x => x.TrackingNumber).HasMaxLength(128);
			entity.Property(x => x.LabelUrl).HasMaxLength(2048);
			entity.Property(x => x.ProviderMessage).HasMaxLength(1024);
			entity.Property(x => x.ErrorMessage).HasMaxLength(2048);
			entity.Property(x => x.Metadata).HasConversion(_003CKargoyeriDbContext_003EFE8CB336D941F77E2D0277DD0E9818290F28BFC1DEF100CA3C7C86F572D2D8342__JsonConvert.Converter<Dictionary<string, string>>()).HasColumnName("MetadataJson");
			entity.Property(x => x.Packages).HasConversion(_003CKargoyeriDbContext_003EFE8CB336D941F77E2D0277DD0E9818290F28BFC1DEF100CA3C7C86F572D2D8342__JsonConvert.Converter<List<Kargoyeri.Domain.ValueObjects.PackageInfo>>()).HasColumnName("PackagesJson");
			entity.OwnsOne(x => x.Sender, owned => ConfigureAddress(owned, "Sender"));
			entity.OwnsOne(x => x.Recipient, owned => ConfigureAddress(owned, "Recipient"));
		});

		modelBuilder.Entity<CustomerTenant>(entity =>
		{
			entity.ToTable("Customers");
			entity.HasKey(x => x.TenantKey);
			entity.Property(x => x.TenantKey).HasMaxLength(64);
			entity.Property(x => x.Name).HasMaxLength(256).IsRequired();
			entity.Property(x => x.ApiKeyHash).HasMaxLength(512).IsRequired();
			entity.Property(x => x.AllowedProviders).HasConversion(_003CKargoyeriDbContext_003EFE8CB336D941F77E2D0277DD0E9818290F28BFC1DEF100CA3C7C86F572D2D8342__JsonConvert.Converter<List<Kargoyeri.Domain.Enums.CargoProviderType>>()).HasColumnName("AllowedProvidersJson");
			entity.Property(x => x.NotificationTargets).HasConversion(_003CKargoyeriDbContext_003EFE8CB336D941F77E2D0277DD0E9818290F28BFC1DEF100CA3C7C86F572D2D8342__JsonConvert.Converter<List<Kargoyeri.Domain.ValueObjects.NotificationTarget>>()).HasColumnName("NotificationTargetsJson");
			entity.Property(x => x.Metadata).HasConversion(_003CKargoyeriDbContext_003EFE8CB336D941F77E2D0277DD0E9818290F28BFC1DEF100CA3C7C86F572D2D8342__JsonConvert.Converter<Dictionary<string, string>>()).HasColumnName("MetadataJson");
		});

		modelBuilder.Entity<NotificationMessage>(entity =>
		{
			entity.ToTable("Notifications");
			entity.HasKey(x => x.Id);
			entity.HasIndex(x => x.CreatedAtUtc);
			entity.HasIndex(x => x.ShipmentReference);
			entity.HasIndex(x => x.TenantKey);
			entity.Property(x => x.TenantKey).HasMaxLength(64).IsRequired();
			entity.Property(x => x.ShipmentReference).HasMaxLength(64).IsRequired();
			entity.Property(x => x.EventType).HasConversion<string>().HasMaxLength(32);
			entity.Property(x => x.Channel).HasConversion<string>().HasMaxLength(32);
			entity.Property(x => x.Status).HasConversion<string>().HasMaxLength(32);
			entity.Property(x => x.Address).HasMaxLength(512).IsRequired();
			entity.Property(x => x.Subject).HasMaxLength(512).IsRequired();
			entity.Property(x => x.ErrorMessage).HasMaxLength(2048);
		});

		modelBuilder.Entity<ShipmentOperationLog>(entity =>
		{
			entity.ToTable("OperationLogs");
			entity.HasKey(x => x.Id);
			entity.HasIndex(x => x.OccurredAtUtc);
			entity.HasIndex(x => x.ShipmentReference);
			entity.HasIndex(x => x.TenantKey);
			entity.Property(x => x.TenantKey).HasMaxLength(64).IsRequired();
			entity.Property(x => x.ShipmentReference).HasMaxLength(64).IsRequired();
			entity.Property(x => x.Operation).HasConversion<string>().HasMaxLength(64);
			entity.Property(x => x.Severity).HasConversion<string>().HasMaxLength(16);
			entity.Property(x => x.Message).HasMaxLength(2048).IsRequired();
		});

		modelBuilder.Entity<ProviderCredential>(entity =>
		{
			entity.ToTable("ProviderCredentials");
			entity.HasKey(x => new { x.TenantKey, x.Provider });
			entity.Property(x => x.TenantKey).HasMaxLength(64);
			entity.Property(x => x.Provider).HasConversion<string>().HasMaxLength(32);
			entity.Property(x => x.ClientCode).HasMaxLength(256);
			entity.Property(x => x.Username).HasMaxLength(256);
			entity.Property(x => x.Password).HasMaxLength(512);
			entity.Property(x => x.ApiKey).HasMaxLength(1024);
			entity.Property(x => x.EndpointBase).HasMaxLength(512);
			entity.Property(x => x.AdditionalSettings).HasConversion(_003CKargoyeriDbContext_003EFE8CB336D941F77E2D0277DD0E9818290F28BFC1DEF100CA3C7C86F572D2D8342__JsonConvert.Converter<Dictionary<string, string>>()).HasColumnName("AdditionalSettingsJson");
		});
	}

	private static void ConfigureAddress(Microsoft.EntityFrameworkCore.Metadata.Builders.OwnedNavigationBuilder<CargoShipment, Kargoyeri.Domain.ValueObjects.AddressInfo> owned, string prefix)
	{
		owned.Property(x => x.Name).HasColumnName(prefix + "Name").HasMaxLength(256).IsRequired();
		owned.Property(x => x.CompanyName).HasColumnName(prefix + "CompanyName").HasMaxLength(256);
		owned.Property(x => x.Phone).HasColumnName(prefix + "Phone").HasMaxLength(32);
		owned.Property(x => x.Email).HasColumnName(prefix + "Email").HasMaxLength(256);
		owned.Property(x => x.CountryCode).HasColumnName(prefix + "CountryCode").HasMaxLength(8).HasDefaultValue("TR");
		owned.Property(x => x.City).HasColumnName(prefix + "City").HasMaxLength(128).IsRequired();
		owned.Property(x => x.District).HasColumnName(prefix + "District").HasMaxLength(128);
		owned.Property(x => x.PostalCode).HasColumnName(prefix + "PostalCode").HasMaxLength(16);
		owned.Property(x => x.AddressLine1).HasColumnName(prefix + "AddressLine1").HasMaxLength(512).IsRequired();
		owned.Property(x => x.AddressLine2).HasColumnName(prefix + "AddressLine2").HasMaxLength(512);
	}
}
