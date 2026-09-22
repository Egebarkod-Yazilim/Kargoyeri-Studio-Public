using System;
using System.Linq.Expressions;
using Kargoyeri.Domain.Entities;
using Kargoyeri.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Kargoyeri.Infrastructure.Persistence.SqlServer;

internal sealed class _003CKargoyeriDbContext_003EFE8CB336D941F77E2D0277DD0E9818290F28BFC1DEF100CA3C7C86F572D2D8342__ShipmentOperationLogConfiguration : IEntityTypeConfiguration<ShipmentOperationLog>
{
	public void Configure(EntityTypeBuilder<ShipmentOperationLog> b)
	{
		RelationalEntityTypeBuilderExtensions.ToTable<ShipmentOperationLog>(b, "OperationLogs");
		b.HasKey((Expression<Func<ShipmentOperationLog, object>>)((ShipmentOperationLog x) => x.Id));
		b.Property<string>((Expression<Func<ShipmentOperationLog, string>>)((ShipmentOperationLog x) => x.TenantKey)).HasMaxLength(64).IsRequired(true);
		b.HasIndex((Expression<Func<ShipmentOperationLog, object>>)((ShipmentOperationLog x) => x.TenantKey));
		b.Property<string>((Expression<Func<ShipmentOperationLog, string>>)((ShipmentOperationLog x) => x.ShipmentReference)).HasMaxLength(64).IsRequired(true);
		b.HasIndex((Expression<Func<ShipmentOperationLog, object>>)((ShipmentOperationLog x) => x.ShipmentReference));
		b.Property<CargoOperationType>((Expression<Func<ShipmentOperationLog, CargoOperationType>>)((ShipmentOperationLog x) => x.Operation)).HasConversion<string>().HasMaxLength(64);
		b.Property<LogSeverity>((Expression<Func<ShipmentOperationLog, LogSeverity>>)((ShipmentOperationLog x) => x.Severity)).HasConversion<string>().HasMaxLength(16);
		b.Property<string>((Expression<Func<ShipmentOperationLog, string>>)((ShipmentOperationLog x) => x.Message)).HasMaxLength(2048).IsRequired(true);
		RelationalPropertyBuilderExtensions.HasColumnType<string>(b.Property<string>((Expression<Func<ShipmentOperationLog, string>>)((ShipmentOperationLog x) => x.ProviderPayload)), "nvarchar(max)");
		b.Property<DateTimeOffset>((Expression<Func<ShipmentOperationLog, DateTimeOffset>>)((ShipmentOperationLog x) => x.OccurredAtUtc)).IsRequired(true);
		b.HasIndex((Expression<Func<ShipmentOperationLog, object>>)((ShipmentOperationLog x) => x.OccurredAtUtc));
	}
}
