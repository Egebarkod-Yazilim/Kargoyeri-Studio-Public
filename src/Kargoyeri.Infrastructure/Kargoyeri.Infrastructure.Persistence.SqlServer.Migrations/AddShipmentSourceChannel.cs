using System;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Migrations;

namespace Kargoyeri.Infrastructure.Persistence.SqlServer.Migrations;

[DbContext(typeof(KargoyeriDbContext))]
[Migration("20260511053608_AddShipmentSourceChannel")]
public class AddShipmentSourceChannel : Migration
{
	protected override void Up(MigrationBuilder migrationBuilder)
	{
		int? num = 32;
		migrationBuilder.AddColumn<string>("SourceChannel", "Shipments", "nvarchar(32)", (bool?)null, num, false, (string)null, true, (object)null, (string)null, (string)null, (bool?)null, (string)null, (string)null, (int?)null, (int?)null, (bool?)null);
		num = 64;
		migrationBuilder.AddColumn<string>("SourceChannelCode", "Shipments", "nvarchar(64)", (bool?)null, num, false, (string)null, true, (object)null, (string)null, (string)null, (bool?)null, (string)null, (string)null, (int?)null, (int?)null, (bool?)null);
		migrationBuilder.CreateIndex("IX_Shipments_TenantKey_SourceChannel", "Shipments", new string[2] { "TenantKey", "SourceChannel" }, (string)null, false, (string)null, (bool[])null);
	}

	protected override void Down(MigrationBuilder migrationBuilder)
	{
		migrationBuilder.DropIndex("IX_Shipments_TenantKey_SourceChannel", "Shipments", (string)null);
		migrationBuilder.DropColumn("SourceChannel", "Shipments", (string)null);
		migrationBuilder.DropColumn("SourceChannelCode", "Shipments", (string)null);
	}

	protected override void BuildTargetModel(ModelBuilder modelBuilder)
	{
		modelBuilder.HasAnnotation("ProductVersion", (object)"8.0.8").HasAnnotation("Relational:MaxIdentifierLength", (object)128);
		SqlServerModelBuilderExtensions.UseIdentityColumns(modelBuilder, 1L, 1);
		modelBuilder.Entity("Kargoyeri.Domain.Entities.CargoShipment", (Action<EntityTypeBuilder>)delegate(EntityTypeBuilder b)
		{
			RelationalPropertyBuilderExtensions.HasColumnType<Guid>(b.Property<Guid>("Id").ValueGeneratedOnAdd(), "uniqueidentifier");
			RelationalPropertyBuilderExtensions.HasColumnType<string>(b.Property<string>("ClientShipmentReference").HasMaxLength(128), "nvarchar(128)");
			RelationalPropertyBuilderExtensions.HasColumnType<decimal?>(b.Property<decimal?>("CollectionAmount"), "decimal(18,4)");
			RelationalPropertyBuilderExtensions.HasColumnType<DateTimeOffset>(b.Property<DateTimeOffset>("CreatedAtUtc"), "datetimeoffset");
			RelationalPropertyBuilderExtensions.HasDefaultValue<string>(RelationalPropertyBuilderExtensions.HasColumnType<string>(b.Property<string>("CurrencyCode").IsRequired(true).ValueGeneratedOnAdd()
				.HasMaxLength(8), "nvarchar(8)"), (object)"TRY");
			RelationalPropertyBuilderExtensions.HasColumnType<string>(b.Property<string>("ErrorMessage").HasMaxLength(2048), "nvarchar(2048)");
			RelationalPropertyBuilderExtensions.HasColumnType<string>(b.Property<string>("IdempotencyKey").HasMaxLength(256), "nvarchar(256)");
			RelationalPropertyBuilderExtensions.HasColumnType<string>(b.Property<string>("LabelContentBase64"), "nvarchar(max)");
			RelationalPropertyBuilderExtensions.HasColumnType<string>(b.Property<string>("LabelUrl").HasMaxLength(2048), "nvarchar(2048)");
			RelationalPropertyBuilderExtensions.HasColumnType<DateTimeOffset?>(b.Property<DateTimeOffset?>("LastStatusCheckAtUtc"), "datetimeoffset");
			RelationalPropertyBuilderExtensions.HasColumnName<string>(RelationalPropertyBuilderExtensions.HasColumnType<string>(b.Property<string>("Metadata").IsRequired(true), "nvarchar(max)"), "MetadataJson");
			RelationalPropertyBuilderExtensions.HasColumnType<string>(b.Property<string>("OrderReference").IsRequired(true).HasMaxLength(128), "nvarchar(128)");
			RelationalPropertyBuilderExtensions.HasColumnName<string>(RelationalPropertyBuilderExtensions.HasColumnType<string>(b.Property<string>("Packages").IsRequired(true), "nvarchar(max)"), "PackagesJson");
			RelationalPropertyBuilderExtensions.HasColumnType<string>(b.Property<string>("Provider").IsRequired(true).HasMaxLength(32), "nvarchar(32)");
			RelationalPropertyBuilderExtensions.HasColumnType<string>(b.Property<string>("ProviderMessage").HasMaxLength(1024), "nvarchar(1024)");
			RelationalPropertyBuilderExtensions.HasColumnType<int>(b.Property<int>("RetryCount"), "int");
			RelationalPropertyBuilderExtensions.HasColumnType<string>(b.Property<string>("ShipmentReference").IsRequired(true).HasMaxLength(64), "nvarchar(64)");
			RelationalPropertyBuilderExtensions.HasColumnType<string>(b.Property<string>("Source").IsRequired(true).HasMaxLength(32), "nvarchar(32)");
			RelationalPropertyBuilderExtensions.HasColumnType<string>(b.Property<string>("SourceChannel").HasMaxLength(32), "nvarchar(32)");
			RelationalPropertyBuilderExtensions.HasColumnType<string>(b.Property<string>("SourceChannelCode").HasMaxLength(64), "nvarchar(64)");
			RelationalPropertyBuilderExtensions.HasColumnType<string>(b.Property<string>("Status").IsRequired(true).HasMaxLength(32), "nvarchar(32)");
			RelationalPropertyBuilderExtensions.HasColumnType<string>(b.Property<string>("TenantKey").IsRequired(true).HasMaxLength(64), "nvarchar(64)");
			RelationalPropertyBuilderExtensions.HasColumnType<string>(b.Property<string>("TrackingNumber").HasMaxLength(128), "nvarchar(128)");
			RelationalPropertyBuilderExtensions.HasColumnType<DateTimeOffset>(b.Property<DateTimeOffset>("UpdatedAtUtc"), "datetimeoffset");
			b.HasKey(new string[1] { "Id" });
			b.HasIndex(new string[1] { "ShipmentReference" }).IsUnique(true);
			b.HasIndex(new string[1] { "TenantKey" });
			RelationalIndexBuilderExtensions.HasFilter(b.HasIndex(new string[1] { "TrackingNumber" }), "[TrackingNumber] IS NOT NULL");
			b.HasIndex(new string[1] { "UpdatedAtUtc" });
			b.HasIndex(new string[2] { "TenantKey", "IdempotencyKey" });
			b.HasIndex(new string[2] { "TenantKey", "SourceChannel" });
			RelationalEntityTypeBuilderExtensions.ToTable(b, "Shipments", (string)null);
		});
		modelBuilder.Entity("Kargoyeri.Domain.Entities.CustomerTenant", (Action<EntityTypeBuilder>)delegate(EntityTypeBuilder b)
		{
			RelationalPropertyBuilderExtensions.HasColumnType<string>(b.Property<string>("TenantKey").HasMaxLength(64), "nvarchar(64)");
			RelationalPropertyBuilderExtensions.HasColumnName<string>(RelationalPropertyBuilderExtensions.HasColumnType<string>(b.Property<string>("AllowedProviders").IsRequired(true), "nvarchar(max)"), "AllowedProvidersJson");
			RelationalPropertyBuilderExtensions.HasColumnType<string>(b.Property<string>("ApiKeyHash").IsRequired(true).HasMaxLength(512), "nvarchar(512)");
			RelationalPropertyBuilderExtensions.HasColumnType<DateTimeOffset>(b.Property<DateTimeOffset>("CreatedAtUtc"), "datetimeoffset");
			RelationalPropertyBuilderExtensions.HasColumnType<bool>(b.Property<bool>("IsActive"), "bit");
			RelationalPropertyBuilderExtensions.HasColumnName<string>(RelationalPropertyBuilderExtensions.HasColumnType<string>(b.Property<string>("Metadata").IsRequired(true), "nvarchar(max)"), "MetadataJson");
			RelationalPropertyBuilderExtensions.HasColumnType<string>(b.Property<string>("Name").IsRequired(true).HasMaxLength(256), "nvarchar(256)");
			RelationalPropertyBuilderExtensions.HasColumnName<string>(RelationalPropertyBuilderExtensions.HasColumnType<string>(b.Property<string>("NotificationTargets").IsRequired(true), "nvarchar(max)"), "NotificationTargetsJson");
			RelationalPropertyBuilderExtensions.HasColumnType<DateTimeOffset>(b.Property<DateTimeOffset>("UpdatedAtUtc"), "datetimeoffset");
			b.HasKey(new string[1] { "TenantKey" });
			RelationalEntityTypeBuilderExtensions.ToTable(b, "Customers", (string)null);
		});
		modelBuilder.Entity("Kargoyeri.Domain.Entities.NotificationMessage", (Action<EntityTypeBuilder>)delegate(EntityTypeBuilder b)
		{
			RelationalPropertyBuilderExtensions.HasColumnType<Guid>(b.Property<Guid>("Id").ValueGeneratedOnAdd(), "uniqueidentifier");
			RelationalPropertyBuilderExtensions.HasColumnType<string>(b.Property<string>("Address").IsRequired(true).HasMaxLength(512), "nvarchar(512)");
			RelationalPropertyBuilderExtensions.HasColumnType<string>(b.Property<string>("Body").IsRequired(true), "nvarchar(max)");
			RelationalPropertyBuilderExtensions.HasColumnType<string>(b.Property<string>("Channel").IsRequired(true).HasMaxLength(32), "nvarchar(32)");
			RelationalPropertyBuilderExtensions.HasColumnType<DateTimeOffset>(b.Property<DateTimeOffset>("CreatedAtUtc"), "datetimeoffset");
			RelationalPropertyBuilderExtensions.HasColumnType<DateTimeOffset?>(b.Property<DateTimeOffset?>("DeliveredAtUtc"), "datetimeoffset");
			RelationalPropertyBuilderExtensions.HasColumnType<string>(b.Property<string>("ErrorMessage").HasMaxLength(2048), "nvarchar(2048)");
			RelationalPropertyBuilderExtensions.HasColumnType<string>(b.Property<string>("EventType").IsRequired(true).HasMaxLength(32), "nvarchar(32)");
			RelationalPropertyBuilderExtensions.HasColumnType<string>(b.Property<string>("ShipmentReference").IsRequired(true).HasMaxLength(64), "nvarchar(64)");
			RelationalPropertyBuilderExtensions.HasColumnType<string>(b.Property<string>("Status").IsRequired(true).HasMaxLength(32), "nvarchar(32)");
			RelationalPropertyBuilderExtensions.HasColumnType<string>(b.Property<string>("Subject").IsRequired(true).HasMaxLength(512), "nvarchar(512)");
			RelationalPropertyBuilderExtensions.HasColumnType<string>(b.Property<string>("TenantKey").IsRequired(true).HasMaxLength(64), "nvarchar(64)");
			b.HasKey(new string[1] { "Id" });
			b.HasIndex(new string[1] { "CreatedAtUtc" });
			b.HasIndex(new string[1] { "ShipmentReference" });
			b.HasIndex(new string[1] { "TenantKey" });
			RelationalEntityTypeBuilderExtensions.ToTable(b, "Notifications", (string)null);
		});
		modelBuilder.Entity("Kargoyeri.Domain.Entities.ProviderCredential", (Action<EntityTypeBuilder>)delegate(EntityTypeBuilder b)
		{
			RelationalPropertyBuilderExtensions.HasColumnType<string>(b.Property<string>("TenantKey").HasMaxLength(64), "nvarchar(64)");
			RelationalPropertyBuilderExtensions.HasColumnType<string>(b.Property<string>("Provider").HasMaxLength(32), "nvarchar(32)");
			RelationalPropertyBuilderExtensions.HasColumnName<string>(RelationalPropertyBuilderExtensions.HasColumnType<string>(b.Property<string>("AdditionalSettings").IsRequired(true), "nvarchar(max)"), "AdditionalSettingsJson");
			RelationalPropertyBuilderExtensions.HasColumnType<string>(b.Property<string>("ApiKey").HasMaxLength(1024), "nvarchar(1024)");
			RelationalPropertyBuilderExtensions.HasColumnType<string>(b.Property<string>("ClientCode").HasMaxLength(256), "nvarchar(256)");
			RelationalPropertyBuilderExtensions.HasColumnType<string>(b.Property<string>("EndpointBase").HasMaxLength(512), "nvarchar(512)");
			RelationalPropertyBuilderExtensions.HasColumnType<bool>(b.Property<bool>("IsEnabled"), "bit");
			RelationalPropertyBuilderExtensions.HasColumnType<string>(b.Property<string>("Password").HasMaxLength(512), "nvarchar(512)");
			RelationalPropertyBuilderExtensions.HasColumnType<DateTimeOffset>(b.Property<DateTimeOffset>("UpdatedAtUtc"), "datetimeoffset");
			RelationalPropertyBuilderExtensions.HasColumnType<string>(b.Property<string>("Username").HasMaxLength(256), "nvarchar(256)");
			b.HasKey(new string[2] { "TenantKey", "Provider" });
			RelationalEntityTypeBuilderExtensions.ToTable(b, "ProviderCredentials", (string)null);
		});
		modelBuilder.Entity("Kargoyeri.Domain.Entities.ShipmentOperationLog", (Action<EntityTypeBuilder>)delegate(EntityTypeBuilder b)
		{
			RelationalPropertyBuilderExtensions.HasColumnType<Guid>(b.Property<Guid>("Id").ValueGeneratedOnAdd(), "uniqueidentifier");
			RelationalPropertyBuilderExtensions.HasColumnType<string>(b.Property<string>("Message").IsRequired(true).HasMaxLength(2048), "nvarchar(2048)");
			RelationalPropertyBuilderExtensions.HasColumnType<DateTimeOffset>(b.Property<DateTimeOffset>("OccurredAtUtc"), "datetimeoffset");
			RelationalPropertyBuilderExtensions.HasColumnType<string>(b.Property<string>("Operation").IsRequired(true).HasMaxLength(64), "nvarchar(64)");
			RelationalPropertyBuilderExtensions.HasColumnType<string>(b.Property<string>("ProviderPayload"), "nvarchar(max)");
			RelationalPropertyBuilderExtensions.HasColumnType<string>(b.Property<string>("Severity").IsRequired(true).HasMaxLength(16), "nvarchar(16)");
			RelationalPropertyBuilderExtensions.HasColumnType<string>(b.Property<string>("ShipmentReference").IsRequired(true).HasMaxLength(64), "nvarchar(64)");
			RelationalPropertyBuilderExtensions.HasColumnType<string>(b.Property<string>("TenantKey").IsRequired(true).HasMaxLength(64), "nvarchar(64)");
			b.HasKey(new string[1] { "Id" });
			b.HasIndex(new string[1] { "OccurredAtUtc" });
			b.HasIndex(new string[1] { "ShipmentReference" });
			b.HasIndex(new string[1] { "TenantKey" });
			RelationalEntityTypeBuilderExtensions.ToTable(b, "OperationLogs", (string)null);
		});
		modelBuilder.Entity("Kargoyeri.Domain.Entities.CargoShipment", (Action<EntityTypeBuilder>)delegate(EntityTypeBuilder b)
		{
			b.OwnsOne("Kargoyeri.Domain.ValueObjects.AddressInfo", "Recipient", (Action<OwnedNavigationBuilder>)delegate(OwnedNavigationBuilder b1)
			{
				RelationalPropertyBuilderExtensions.HasColumnType<Guid>(b1.Property<Guid>("CargoShipmentId"), "uniqueidentifier");
				RelationalPropertyBuilderExtensions.HasColumnName<string>(RelationalPropertyBuilderExtensions.HasColumnType<string>(b1.Property<string>("AddressLine1").IsRequired(true).HasMaxLength(512), "nvarchar(512)"), "RecipientAddressLine1");
				RelationalPropertyBuilderExtensions.HasColumnName<string>(RelationalPropertyBuilderExtensions.HasColumnType<string>(b1.Property<string>("AddressLine2").HasMaxLength(512), "nvarchar(512)"), "RecipientAddressLine2");
				RelationalPropertyBuilderExtensions.HasColumnName<string>(RelationalPropertyBuilderExtensions.HasColumnType<string>(b1.Property<string>("City").IsRequired(true).HasMaxLength(128), "nvarchar(128)"), "RecipientCity");
				RelationalPropertyBuilderExtensions.HasColumnName<string>(RelationalPropertyBuilderExtensions.HasColumnType<string>(b1.Property<string>("CompanyName").HasMaxLength(256), "nvarchar(256)"), "RecipientCompanyName");
				RelationalPropertyBuilderExtensions.HasColumnName<string>(RelationalPropertyBuilderExtensions.HasDefaultValue<string>(RelationalPropertyBuilderExtensions.HasColumnType<string>(b1.Property<string>("CountryCode").IsRequired(true).ValueGeneratedOnAdd()
					.HasMaxLength(8), "nvarchar(8)"), (object)"TR"), "RecipientCountryCode");
				RelationalPropertyBuilderExtensions.HasColumnName<string>(RelationalPropertyBuilderExtensions.HasColumnType<string>(b1.Property<string>("District").HasMaxLength(128), "nvarchar(128)"), "RecipientDistrict");
				RelationalPropertyBuilderExtensions.HasColumnName<string>(RelationalPropertyBuilderExtensions.HasColumnType<string>(b1.Property<string>("Email").HasMaxLength(256), "nvarchar(256)"), "RecipientEmail");
				RelationalPropertyBuilderExtensions.HasColumnName<string>(RelationalPropertyBuilderExtensions.HasColumnType<string>(b1.Property<string>("Name").IsRequired(true).HasMaxLength(256), "nvarchar(256)"), "RecipientName");
				RelationalPropertyBuilderExtensions.HasColumnName<string>(RelationalPropertyBuilderExtensions.HasColumnType<string>(b1.Property<string>("Phone").HasMaxLength(32), "nvarchar(32)"), "RecipientPhone");
				RelationalPropertyBuilderExtensions.HasColumnName<string>(RelationalPropertyBuilderExtensions.HasColumnType<string>(b1.Property<string>("PostalCode").HasMaxLength(16), "nvarchar(16)"), "RecipientPostalCode");
				b1.HasKey(new string[1] { "CargoShipmentId" });
				RelationalEntityTypeBuilderExtensions.ToTable(b1, "Shipments");
				b1.WithOwner((string)null).HasForeignKey(new string[1] { "CargoShipmentId" });
			});
			b.OwnsOne("Kargoyeri.Domain.ValueObjects.AddressInfo", "Sender", (Action<OwnedNavigationBuilder>)delegate(OwnedNavigationBuilder b1)
			{
				RelationalPropertyBuilderExtensions.HasColumnType<Guid>(b1.Property<Guid>("CargoShipmentId"), "uniqueidentifier");
				RelationalPropertyBuilderExtensions.HasColumnName<string>(RelationalPropertyBuilderExtensions.HasColumnType<string>(b1.Property<string>("AddressLine1").IsRequired(true).HasMaxLength(512), "nvarchar(512)"), "SenderAddressLine1");
				RelationalPropertyBuilderExtensions.HasColumnName<string>(RelationalPropertyBuilderExtensions.HasColumnType<string>(b1.Property<string>("AddressLine2").HasMaxLength(512), "nvarchar(512)"), "SenderAddressLine2");
				RelationalPropertyBuilderExtensions.HasColumnName<string>(RelationalPropertyBuilderExtensions.HasColumnType<string>(b1.Property<string>("City").IsRequired(true).HasMaxLength(128), "nvarchar(128)"), "SenderCity");
				RelationalPropertyBuilderExtensions.HasColumnName<string>(RelationalPropertyBuilderExtensions.HasColumnType<string>(b1.Property<string>("CompanyName").HasMaxLength(256), "nvarchar(256)"), "SenderCompanyName");
				RelationalPropertyBuilderExtensions.HasColumnName<string>(RelationalPropertyBuilderExtensions.HasDefaultValue<string>(RelationalPropertyBuilderExtensions.HasColumnType<string>(b1.Property<string>("CountryCode").IsRequired(true).ValueGeneratedOnAdd()
					.HasMaxLength(8), "nvarchar(8)"), (object)"TR"), "SenderCountryCode");
				RelationalPropertyBuilderExtensions.HasColumnName<string>(RelationalPropertyBuilderExtensions.HasColumnType<string>(b1.Property<string>("District").HasMaxLength(128), "nvarchar(128)"), "SenderDistrict");
				RelationalPropertyBuilderExtensions.HasColumnName<string>(RelationalPropertyBuilderExtensions.HasColumnType<string>(b1.Property<string>("Email").HasMaxLength(256), "nvarchar(256)"), "SenderEmail");
				RelationalPropertyBuilderExtensions.HasColumnName<string>(RelationalPropertyBuilderExtensions.HasColumnType<string>(b1.Property<string>("Name").IsRequired(true).HasMaxLength(256), "nvarchar(256)"), "SenderName");
				RelationalPropertyBuilderExtensions.HasColumnName<string>(RelationalPropertyBuilderExtensions.HasColumnType<string>(b1.Property<string>("Phone").HasMaxLength(32), "nvarchar(32)"), "SenderPhone");
				RelationalPropertyBuilderExtensions.HasColumnName<string>(RelationalPropertyBuilderExtensions.HasColumnType<string>(b1.Property<string>("PostalCode").HasMaxLength(16), "nvarchar(16)"), "SenderPostalCode");
				b1.HasKey(new string[1] { "CargoShipmentId" });
				RelationalEntityTypeBuilderExtensions.ToTable(b1, "Shipments");
				b1.WithOwner((string)null).HasForeignKey(new string[1] { "CargoShipmentId" });
			});
			b.Navigation("Recipient").IsRequired(true);
			b.Navigation("Sender").IsRequired(true);
		});
	}
}
