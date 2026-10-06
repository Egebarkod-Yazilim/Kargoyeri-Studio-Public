using System;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using Microsoft.EntityFrameworkCore.Migrations;
using Microsoft.EntityFrameworkCore.Migrations.Operations;
using Microsoft.EntityFrameworkCore.Migrations.Operations.Builders;

namespace Kargoyeri.Infrastructure.Persistence.SqlServer.Migrations;

[DbContext(typeof(KargoyeriDbContext))]
[Migration("20260424123910_InitialCreate")]
public class InitialCreate : Migration
{
	protected override void Up(MigrationBuilder migrationBuilder)
	{
		migrationBuilder.CreateTable("Customers", delegate(ColumnsBuilder table)
		{
			int? num5 = 64;
			OperationBuilder<AddColumnOperation> tenantKey5 = table.Column<string>("nvarchar(64)", (bool?)null, num5, false, (string)null, false, (object)null, (string)null, (string)null, (bool?)null, (string)null, (string)null, (int?)null, (int?)null, (bool?)null);
			num5 = 256;
			OperationBuilder<AddColumnOperation> name = table.Column<string>("nvarchar(256)", (bool?)null, num5, false, (string)null, false, (object)null, (string)null, (string)null, (bool?)null, (string)null, (string)null, (int?)null, (int?)null, (bool?)null);
			OperationBuilder<AddColumnOperation> isActive = table.Column<bool>("bit", (bool?)null, (int?)null, false, (string)null, false, (object)null, (string)null, (string)null, (bool?)null, (string)null, (string)null, (int?)null, (int?)null, (bool?)null);
			num5 = 512;
			return new
			{
				TenantKey = tenantKey5,
				Name = name,
				IsActive = isActive,
				ApiKeyHash = table.Column<string>("nvarchar(512)", (bool?)null, num5, false, (string)null, false, (object)null, (string)null, (string)null, (bool?)null, (string)null, (string)null, (int?)null, (int?)null, (bool?)null),
				AllowedProvidersJson = table.Column<string>("nvarchar(max)", (bool?)null, (int?)null, false, (string)null, false, (object)null, (string)null, (string)null, (bool?)null, (string)null, (string)null, (int?)null, (int?)null, (bool?)null),
				NotificationTargetsJson = table.Column<string>("nvarchar(max)", (bool?)null, (int?)null, false, (string)null, false, (object)null, (string)null, (string)null, (bool?)null, (string)null, (string)null, (int?)null, (int?)null, (bool?)null),
				MetadataJson = table.Column<string>("nvarchar(max)", (bool?)null, (int?)null, false, (string)null, false, (object)null, (string)null, (string)null, (bool?)null, (string)null, (string)null, (int?)null, (int?)null, (bool?)null),
				CreatedAtUtc = table.Column<DateTimeOffset>("datetimeoffset", (bool?)null, (int?)null, false, (string)null, false, (object)null, (string)null, (string)null, (bool?)null, (string)null, (string)null, (int?)null, (int?)null, (bool?)null),
				UpdatedAtUtc = table.Column<DateTimeOffset>("datetimeoffset", (bool?)null, (int?)null, false, (string)null, false, (object)null, (string)null, (string)null, (bool?)null, (string)null, (string)null, (int?)null, (int?)null, (bool?)null)
			};
		}, (string)null, table =>
		{
			table.PrimaryKey("PK_Customers", x => (object)x.TenantKey);
		}, (string)null);
		migrationBuilder.CreateTable("Notifications", delegate(ColumnsBuilder table)
		{
			OperationBuilder<AddColumnOperation> id3 = table.Column<Guid>("uniqueidentifier", (bool?)null, (int?)null, false, (string)null, false, (object)null, (string)null, (string)null, (bool?)null, (string)null, (string)null, (int?)null, (int?)null, (bool?)null);
			int? num4 = 64;
			OperationBuilder<AddColumnOperation> tenantKey4 = table.Column<string>("nvarchar(64)", (bool?)null, num4, false, (string)null, false, (object)null, (string)null, (string)null, (bool?)null, (string)null, (string)null, (int?)null, (int?)null, (bool?)null);
			num4 = 64;
			OperationBuilder<AddColumnOperation> shipmentReference3 = table.Column<string>("nvarchar(64)", (bool?)null, num4, false, (string)null, false, (object)null, (string)null, (string)null, (bool?)null, (string)null, (string)null, (int?)null, (int?)null, (bool?)null);
			num4 = 32;
			OperationBuilder<AddColumnOperation> eventType = table.Column<string>("nvarchar(32)", (bool?)null, num4, false, (string)null, false, (object)null, (string)null, (string)null, (bool?)null, (string)null, (string)null, (int?)null, (int?)null, (bool?)null);
			num4 = 32;
			OperationBuilder<AddColumnOperation> channel = table.Column<string>("nvarchar(32)", (bool?)null, num4, false, (string)null, false, (object)null, (string)null, (string)null, (bool?)null, (string)null, (string)null, (int?)null, (int?)null, (bool?)null);
			num4 = 32;
			OperationBuilder<AddColumnOperation> status2 = table.Column<string>("nvarchar(32)", (bool?)null, num4, false, (string)null, false, (object)null, (string)null, (string)null, (bool?)null, (string)null, (string)null, (int?)null, (int?)null, (bool?)null);
			num4 = 512;
			OperationBuilder<AddColumnOperation> address = table.Column<string>("nvarchar(512)", (bool?)null, num4, false, (string)null, false, (object)null, (string)null, (string)null, (bool?)null, (string)null, (string)null, (int?)null, (int?)null, (bool?)null);
			num4 = 512;
			OperationBuilder<AddColumnOperation> subject = table.Column<string>("nvarchar(512)", (bool?)null, num4, false, (string)null, false, (object)null, (string)null, (string)null, (bool?)null, (string)null, (string)null, (int?)null, (int?)null, (bool?)null);
			OperationBuilder<AddColumnOperation> body = table.Column<string>("nvarchar(max)", (bool?)null, (int?)null, false, (string)null, false, (object)null, (string)null, (string)null, (bool?)null, (string)null, (string)null, (int?)null, (int?)null, (bool?)null);
			num4 = 2048;
			return new
			{
				Id = id3,
				TenantKey = tenantKey4,
				ShipmentReference = shipmentReference3,
				EventType = eventType,
				Channel = channel,
				Status = status2,
				Address = address,
				Subject = subject,
				Body = body,
				ErrorMessage = table.Column<string>("nvarchar(2048)", (bool?)null, num4, false, (string)null, true, (object)null, (string)null, (string)null, (bool?)null, (string)null, (string)null, (int?)null, (int?)null, (bool?)null),
				CreatedAtUtc = table.Column<DateTimeOffset>("datetimeoffset", (bool?)null, (int?)null, false, (string)null, false, (object)null, (string)null, (string)null, (bool?)null, (string)null, (string)null, (int?)null, (int?)null, (bool?)null),
				DeliveredAtUtc = table.Column<DateTimeOffset>("datetimeoffset", (bool?)null, (int?)null, false, (string)null, true, (object)null, (string)null, (string)null, (bool?)null, (string)null, (string)null, (int?)null, (int?)null, (bool?)null)
			};
		}, (string)null, table =>
		{
			table.PrimaryKey("PK_Notifications", x => (object)x.Id);
		}, (string)null);
		migrationBuilder.CreateTable("OperationLogs", delegate(ColumnsBuilder table)
		{
			OperationBuilder<AddColumnOperation> id2 = table.Column<Guid>("uniqueidentifier", (bool?)null, (int?)null, false, (string)null, false, (object)null, (string)null, (string)null, (bool?)null, (string)null, (string)null, (int?)null, (int?)null, (bool?)null);
			int? num3 = 64;
			OperationBuilder<AddColumnOperation> tenantKey3 = table.Column<string>("nvarchar(64)", (bool?)null, num3, false, (string)null, false, (object)null, (string)null, (string)null, (bool?)null, (string)null, (string)null, (int?)null, (int?)null, (bool?)null);
			num3 = 64;
			OperationBuilder<AddColumnOperation> shipmentReference2 = table.Column<string>("nvarchar(64)", (bool?)null, num3, false, (string)null, false, (object)null, (string)null, (string)null, (bool?)null, (string)null, (string)null, (int?)null, (int?)null, (bool?)null);
			num3 = 64;
			OperationBuilder<AddColumnOperation> operation = table.Column<string>("nvarchar(64)", (bool?)null, num3, false, (string)null, false, (object)null, (string)null, (string)null, (bool?)null, (string)null, (string)null, (int?)null, (int?)null, (bool?)null);
			num3 = 16;
			OperationBuilder<AddColumnOperation> severity = table.Column<string>("nvarchar(16)", (bool?)null, num3, false, (string)null, false, (object)null, (string)null, (string)null, (bool?)null, (string)null, (string)null, (int?)null, (int?)null, (bool?)null);
			num3 = 2048;
			return new
			{
				Id = id2,
				TenantKey = tenantKey3,
				ShipmentReference = shipmentReference2,
				Operation = operation,
				Severity = severity,
				Message = table.Column<string>("nvarchar(2048)", (bool?)null, num3, false, (string)null, false, (object)null, (string)null, (string)null, (bool?)null, (string)null, (string)null, (int?)null, (int?)null, (bool?)null),
				ProviderPayload = table.Column<string>("nvarchar(max)", (bool?)null, (int?)null, false, (string)null, true, (object)null, (string)null, (string)null, (bool?)null, (string)null, (string)null, (int?)null, (int?)null, (bool?)null),
				OccurredAtUtc = table.Column<DateTimeOffset>("datetimeoffset", (bool?)null, (int?)null, false, (string)null, false, (object)null, (string)null, (string)null, (bool?)null, (string)null, (string)null, (int?)null, (int?)null, (bool?)null)
			};
		}, (string)null, table =>
		{
			table.PrimaryKey("PK_OperationLogs", x => (object)x.Id);
		}, (string)null);
		migrationBuilder.CreateTable("ProviderCredentials", delegate(ColumnsBuilder table)
		{
			int? num2 = 64;
			OperationBuilder<AddColumnOperation> tenantKey2 = table.Column<string>("nvarchar(64)", (bool?)null, num2, false, (string)null, false, (object)null, (string)null, (string)null, (bool?)null, (string)null, (string)null, (int?)null, (int?)null, (bool?)null);
			num2 = 32;
			OperationBuilder<AddColumnOperation> provider2 = table.Column<string>("nvarchar(32)", (bool?)null, num2, false, (string)null, false, (object)null, (string)null, (string)null, (bool?)null, (string)null, (string)null, (int?)null, (int?)null, (bool?)null);
			OperationBuilder<AddColumnOperation> isEnabled = table.Column<bool>("bit", (bool?)null, (int?)null, false, (string)null, false, (object)null, (string)null, (string)null, (bool?)null, (string)null, (string)null, (int?)null, (int?)null, (bool?)null);
			num2 = 256;
			OperationBuilder<AddColumnOperation> clientCode = table.Column<string>("nvarchar(256)", (bool?)null, num2, false, (string)null, true, (object)null, (string)null, (string)null, (bool?)null, (string)null, (string)null, (int?)null, (int?)null, (bool?)null);
			num2 = 256;
			OperationBuilder<AddColumnOperation> username = table.Column<string>("nvarchar(256)", (bool?)null, num2, false, (string)null, true, (object)null, (string)null, (string)null, (bool?)null, (string)null, (string)null, (int?)null, (int?)null, (bool?)null);
			num2 = 512;
			OperationBuilder<AddColumnOperation> password = table.Column<string>("nvarchar(512)", (bool?)null, num2, false, (string)null, true, (object)null, (string)null, (string)null, (bool?)null, (string)null, (string)null, (int?)null, (int?)null, (bool?)null);
			num2 = 1024;
			OperationBuilder<AddColumnOperation> apiKey = table.Column<string>("nvarchar(1024)", (bool?)null, num2, false, (string)null, true, (object)null, (string)null, (string)null, (bool?)null, (string)null, (string)null, (int?)null, (int?)null, (bool?)null);
			num2 = 512;
			return new
			{
				TenantKey = tenantKey2,
				Provider = provider2,
				IsEnabled = isEnabled,
				ClientCode = clientCode,
				Username = username,
				Password = password,
				ApiKey = apiKey,
				EndpointBase = table.Column<string>("nvarchar(512)", (bool?)null, num2, false, (string)null, true, (object)null, (string)null, (string)null, (bool?)null, (string)null, (string)null, (int?)null, (int?)null, (bool?)null),
				AdditionalSettingsJson = table.Column<string>("nvarchar(max)", (bool?)null, (int?)null, false, (string)null, false, (object)null, (string)null, (string)null, (bool?)null, (string)null, (string)null, (int?)null, (int?)null, (bool?)null),
				UpdatedAtUtc = table.Column<DateTimeOffset>("datetimeoffset", (bool?)null, (int?)null, false, (string)null, false, (object)null, (string)null, (string)null, (bool?)null, (string)null, (string)null, (int?)null, (int?)null, (bool?)null)
			};
		}, (string)null, table =>
		{
			table.PrimaryKey("PK_ProviderCredentials", x => (object)new { x.TenantKey, x.Provider });
		}, (string)null);
		migrationBuilder.CreateTable("Shipments", delegate(ColumnsBuilder table)
		{
			OperationBuilder<AddColumnOperation> id = table.Column<Guid>("uniqueidentifier", (bool?)null, (int?)null, false, (string)null, false, (object)null, (string)null, (string)null, (bool?)null, (string)null, (string)null, (int?)null, (int?)null, (bool?)null);
			int? num = 64;
			OperationBuilder<AddColumnOperation> shipmentReference = table.Column<string>("nvarchar(64)", (bool?)null, num, false, (string)null, false, (object)null, (string)null, (string)null, (bool?)null, (string)null, (string)null, (int?)null, (int?)null, (bool?)null);
			num = 64;
			OperationBuilder<AddColumnOperation> tenantKey = table.Column<string>("nvarchar(64)", (bool?)null, num, false, (string)null, false, (object)null, (string)null, (string)null, (bool?)null, (string)null, (string)null, (int?)null, (int?)null, (bool?)null);
			num = 128;
			OperationBuilder<AddColumnOperation> orderReference = table.Column<string>("nvarchar(128)", (bool?)null, num, false, (string)null, false, (object)null, (string)null, (string)null, (bool?)null, (string)null, (string)null, (int?)null, (int?)null, (bool?)null);
			num = 128;
			OperationBuilder<AddColumnOperation> clientShipmentReference = table.Column<string>("nvarchar(128)", (bool?)null, num, false, (string)null, true, (object)null, (string)null, (string)null, (bool?)null, (string)null, (string)null, (int?)null, (int?)null, (bool?)null);
			num = 256;
			OperationBuilder<AddColumnOperation> idempotencyKey = table.Column<string>("nvarchar(256)", (bool?)null, num, false, (string)null, true, (object)null, (string)null, (string)null, (bool?)null, (string)null, (string)null, (int?)null, (int?)null, (bool?)null);
			num = 32;
			OperationBuilder<AddColumnOperation> provider = table.Column<string>("nvarchar(32)", (bool?)null, num, false, (string)null, false, (object)null, (string)null, (string)null, (bool?)null, (string)null, (string)null, (int?)null, (int?)null, (bool?)null);
			num = 32;
			OperationBuilder<AddColumnOperation> source = table.Column<string>("nvarchar(32)", (bool?)null, num, false, (string)null, false, (object)null, (string)null, (string)null, (bool?)null, (string)null, (string)null, (int?)null, (int?)null, (bool?)null);
			num = 32;
			OperationBuilder<AddColumnOperation> status = table.Column<string>("nvarchar(32)", (bool?)null, num, false, (string)null, false, (object)null, (string)null, (string)null, (bool?)null, (string)null, (string)null, (int?)null, (int?)null, (bool?)null);
			OperationBuilder<AddColumnOperation> collectionAmount = table.Column<decimal>("decimal(18,4)", (bool?)null, (int?)null, false, (string)null, true, (object)null, (string)null, (string)null, (bool?)null, (string)null, (string)null, (int?)null, (int?)null, (bool?)null);
			num = 8;
			OperationBuilder<AddColumnOperation> currencyCode = table.Column<string>("nvarchar(8)", (bool?)null, num, false, (string)null, false, (object)"TRY", (string)null, (string)null, (bool?)null, (string)null, (string)null, (int?)null, (int?)null, (bool?)null);
			num = 256;
			OperationBuilder<AddColumnOperation> senderName = table.Column<string>("nvarchar(256)", (bool?)null, num, false, (string)null, false, (object)null, (string)null, (string)null, (bool?)null, (string)null, (string)null, (int?)null, (int?)null, (bool?)null);
			num = 256;
			OperationBuilder<AddColumnOperation> senderCompanyName = table.Column<string>("nvarchar(256)", (bool?)null, num, false, (string)null, true, (object)null, (string)null, (string)null, (bool?)null, (string)null, (string)null, (int?)null, (int?)null, (bool?)null);
			num = 32;
			OperationBuilder<AddColumnOperation> senderPhone = table.Column<string>("nvarchar(32)", (bool?)null, num, false, (string)null, true, (object)null, (string)null, (string)null, (bool?)null, (string)null, (string)null, (int?)null, (int?)null, (bool?)null);
			num = 256;
			OperationBuilder<AddColumnOperation> senderEmail = table.Column<string>("nvarchar(256)", (bool?)null, num, false, (string)null, true, (object)null, (string)null, (string)null, (bool?)null, (string)null, (string)null, (int?)null, (int?)null, (bool?)null);
			num = 8;
			OperationBuilder<AddColumnOperation> senderCountryCode = table.Column<string>("nvarchar(8)", (bool?)null, num, false, (string)null, false, (object)"TR", (string)null, (string)null, (bool?)null, (string)null, (string)null, (int?)null, (int?)null, (bool?)null);
			num = 128;
			OperationBuilder<AddColumnOperation> senderCity = table.Column<string>("nvarchar(128)", (bool?)null, num, false, (string)null, false, (object)null, (string)null, (string)null, (bool?)null, (string)null, (string)null, (int?)null, (int?)null, (bool?)null);
			num = 128;
			OperationBuilder<AddColumnOperation> senderDistrict = table.Column<string>("nvarchar(128)", (bool?)null, num, false, (string)null, true, (object)null, (string)null, (string)null, (bool?)null, (string)null, (string)null, (int?)null, (int?)null, (bool?)null);
			num = 16;
			OperationBuilder<AddColumnOperation> senderPostalCode = table.Column<string>("nvarchar(16)", (bool?)null, num, false, (string)null, true, (object)null, (string)null, (string)null, (bool?)null, (string)null, (string)null, (int?)null, (int?)null, (bool?)null);
			num = 512;
			OperationBuilder<AddColumnOperation> senderAddressLine = table.Column<string>("nvarchar(512)", (bool?)null, num, false, (string)null, false, (object)null, (string)null, (string)null, (bool?)null, (string)null, (string)null, (int?)null, (int?)null, (bool?)null);
			num = 512;
			OperationBuilder<AddColumnOperation> senderAddressLine2 = table.Column<string>("nvarchar(512)", (bool?)null, num, false, (string)null, true, (object)null, (string)null, (string)null, (bool?)null, (string)null, (string)null, (int?)null, (int?)null, (bool?)null);
			num = 256;
			OperationBuilder<AddColumnOperation> recipientName = table.Column<string>("nvarchar(256)", (bool?)null, num, false, (string)null, false, (object)null, (string)null, (string)null, (bool?)null, (string)null, (string)null, (int?)null, (int?)null, (bool?)null);
			num = 256;
			OperationBuilder<AddColumnOperation> recipientCompanyName = table.Column<string>("nvarchar(256)", (bool?)null, num, false, (string)null, true, (object)null, (string)null, (string)null, (bool?)null, (string)null, (string)null, (int?)null, (int?)null, (bool?)null);
			num = 32;
			OperationBuilder<AddColumnOperation> recipientPhone = table.Column<string>("nvarchar(32)", (bool?)null, num, false, (string)null, true, (object)null, (string)null, (string)null, (bool?)null, (string)null, (string)null, (int?)null, (int?)null, (bool?)null);
			num = 256;
			OperationBuilder<AddColumnOperation> recipientEmail = table.Column<string>("nvarchar(256)", (bool?)null, num, false, (string)null, true, (object)null, (string)null, (string)null, (bool?)null, (string)null, (string)null, (int?)null, (int?)null, (bool?)null);
			num = 8;
			OperationBuilder<AddColumnOperation> recipientCountryCode = table.Column<string>("nvarchar(8)", (bool?)null, num, false, (string)null, false, (object)"TR", (string)null, (string)null, (bool?)null, (string)null, (string)null, (int?)null, (int?)null, (bool?)null);
			num = 128;
			OperationBuilder<AddColumnOperation> recipientCity = table.Column<string>("nvarchar(128)", (bool?)null, num, false, (string)null, false, (object)null, (string)null, (string)null, (bool?)null, (string)null, (string)null, (int?)null, (int?)null, (bool?)null);
			num = 128;
			OperationBuilder<AddColumnOperation> recipientDistrict = table.Column<string>("nvarchar(128)", (bool?)null, num, false, (string)null, true, (object)null, (string)null, (string)null, (bool?)null, (string)null, (string)null, (int?)null, (int?)null, (bool?)null);
			num = 16;
			OperationBuilder<AddColumnOperation> recipientPostalCode = table.Column<string>("nvarchar(16)", (bool?)null, num, false, (string)null, true, (object)null, (string)null, (string)null, (bool?)null, (string)null, (string)null, (int?)null, (int?)null, (bool?)null);
			num = 512;
			OperationBuilder<AddColumnOperation> recipientAddressLine = table.Column<string>("nvarchar(512)", (bool?)null, num, false, (string)null, false, (object)null, (string)null, (string)null, (bool?)null, (string)null, (string)null, (int?)null, (int?)null, (bool?)null);
			num = 512;
			OperationBuilder<AddColumnOperation> recipientAddressLine2 = table.Column<string>("nvarchar(512)", (bool?)null, num, false, (string)null, true, (object)null, (string)null, (string)null, (bool?)null, (string)null, (string)null, (int?)null, (int?)null, (bool?)null);
			OperationBuilder<AddColumnOperation> packagesJson = table.Column<string>("nvarchar(max)", (bool?)null, (int?)null, false, (string)null, false, (object)null, (string)null, (string)null, (bool?)null, (string)null, (string)null, (int?)null, (int?)null, (bool?)null);
			OperationBuilder<AddColumnOperation> metadataJson = table.Column<string>("nvarchar(max)", (bool?)null, (int?)null, false, (string)null, false, (object)null, (string)null, (string)null, (bool?)null, (string)null, (string)null, (int?)null, (int?)null, (bool?)null);
			num = 128;
			OperationBuilder<AddColumnOperation> trackingNumber = table.Column<string>("nvarchar(128)", (bool?)null, num, false, (string)null, true, (object)null, (string)null, (string)null, (bool?)null, (string)null, (string)null, (int?)null, (int?)null, (bool?)null);
			num = 2048;
			OperationBuilder<AddColumnOperation> labelUrl = table.Column<string>("nvarchar(2048)", (bool?)null, num, false, (string)null, true, (object)null, (string)null, (string)null, (bool?)null, (string)null, (string)null, (int?)null, (int?)null, (bool?)null);
			OperationBuilder<AddColumnOperation> labelContentBase = table.Column<string>("nvarchar(max)", (bool?)null, (int?)null, false, (string)null, true, (object)null, (string)null, (string)null, (bool?)null, (string)null, (string)null, (int?)null, (int?)null, (bool?)null);
			num = 1024;
			OperationBuilder<AddColumnOperation> providerMessage = table.Column<string>("nvarchar(1024)", (bool?)null, num, false, (string)null, true, (object)null, (string)null, (string)null, (bool?)null, (string)null, (string)null, (int?)null, (int?)null, (bool?)null);
			num = 2048;
			return new
			{
				Id = id,
				ShipmentReference = shipmentReference,
				TenantKey = tenantKey,
				OrderReference = orderReference,
				ClientShipmentReference = clientShipmentReference,
				IdempotencyKey = idempotencyKey,
				Provider = provider,
				Source = source,
				Status = status,
				CollectionAmount = collectionAmount,
				CurrencyCode = currencyCode,
				SenderName = senderName,
				SenderCompanyName = senderCompanyName,
				SenderPhone = senderPhone,
				SenderEmail = senderEmail,
				SenderCountryCode = senderCountryCode,
				SenderCity = senderCity,
				SenderDistrict = senderDistrict,
				SenderPostalCode = senderPostalCode,
				SenderAddressLine1 = senderAddressLine,
				SenderAddressLine2 = senderAddressLine2,
				RecipientName = recipientName,
				RecipientCompanyName = recipientCompanyName,
				RecipientPhone = recipientPhone,
				RecipientEmail = recipientEmail,
				RecipientCountryCode = recipientCountryCode,
				RecipientCity = recipientCity,
				RecipientDistrict = recipientDistrict,
				RecipientPostalCode = recipientPostalCode,
				RecipientAddressLine1 = recipientAddressLine,
				RecipientAddressLine2 = recipientAddressLine2,
				PackagesJson = packagesJson,
				MetadataJson = metadataJson,
				TrackingNumber = trackingNumber,
				LabelUrl = labelUrl,
				LabelContentBase64 = labelContentBase,
				ProviderMessage = providerMessage,
				ErrorMessage = table.Column<string>("nvarchar(2048)", (bool?)null, num, false, (string)null, true, (object)null, (string)null, (string)null, (bool?)null, (string)null, (string)null, (int?)null, (int?)null, (bool?)null),
				RetryCount = table.Column<int>("int", (bool?)null, (int?)null, false, (string)null, false, (object)null, (string)null, (string)null, (bool?)null, (string)null, (string)null, (int?)null, (int?)null, (bool?)null),
				CreatedAtUtc = table.Column<DateTimeOffset>("datetimeoffset", (bool?)null, (int?)null, false, (string)null, false, (object)null, (string)null, (string)null, (bool?)null, (string)null, (string)null, (int?)null, (int?)null, (bool?)null),
				UpdatedAtUtc = table.Column<DateTimeOffset>("datetimeoffset", (bool?)null, (int?)null, false, (string)null, false, (object)null, (string)null, (string)null, (bool?)null, (string)null, (string)null, (int?)null, (int?)null, (bool?)null),
				LastStatusCheckAtUtc = table.Column<DateTimeOffset>("datetimeoffset", (bool?)null, (int?)null, false, (string)null, true, (object)null, (string)null, (string)null, (bool?)null, (string)null, (string)null, (int?)null, (int?)null, (bool?)null)
			};
		}, (string)null, table =>
		{
			table.PrimaryKey("PK_Shipments", x => (object)x.Id);
		}, (string)null);
		migrationBuilder.CreateIndex("IX_Notifications_CreatedAtUtc", "Notifications", "CreatedAtUtc", (string)null, false, (string)null, (bool[])null);
		migrationBuilder.CreateIndex("IX_Notifications_ShipmentReference", "Notifications", "ShipmentReference", (string)null, false, (string)null, (bool[])null);
		migrationBuilder.CreateIndex("IX_Notifications_TenantKey", "Notifications", "TenantKey", (string)null, false, (string)null, (bool[])null);
		migrationBuilder.CreateIndex("IX_OperationLogs_OccurredAtUtc", "OperationLogs", "OccurredAtUtc", (string)null, false, (string)null, (bool[])null);
		migrationBuilder.CreateIndex("IX_OperationLogs_ShipmentReference", "OperationLogs", "ShipmentReference", (string)null, false, (string)null, (bool[])null);
		migrationBuilder.CreateIndex("IX_OperationLogs_TenantKey", "OperationLogs", "TenantKey", (string)null, false, (string)null, (bool[])null);
		migrationBuilder.CreateIndex("IX_Shipments_ShipmentReference", "Shipments", "ShipmentReference", (string)null, true, (string)null, (bool[])null);
		migrationBuilder.CreateIndex("IX_Shipments_TenantKey", "Shipments", "TenantKey", (string)null, false, (string)null, (bool[])null);
		migrationBuilder.CreateIndex("IX_Shipments_TenantKey_IdempotencyKey", "Shipments", new string[2] { "TenantKey", "IdempotencyKey" }, (string)null, false, (string)null, (bool[])null);
		migrationBuilder.CreateIndex("IX_Shipments_TrackingNumber", "Shipments", "TrackingNumber", (string)null, false, "[TrackingNumber] IS NOT NULL", (bool[])null);
		migrationBuilder.CreateIndex("IX_Shipments_UpdatedAtUtc", "Shipments", "UpdatedAtUtc", (string)null, false, (string)null, (bool[])null);
	}

	protected override void Down(MigrationBuilder migrationBuilder)
	{
		migrationBuilder.DropTable("Customers", (string)null);
		migrationBuilder.DropTable("Notifications", (string)null);
		migrationBuilder.DropTable("OperationLogs", (string)null);
		migrationBuilder.DropTable("ProviderCredentials", (string)null);
		migrationBuilder.DropTable("Shipments", (string)null);
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
