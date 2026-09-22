using System;
using System.Linq.Expressions;
using Kargoyeri.Domain.Entities;
using Kargoyeri.Domain.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;

namespace Kargoyeri.Infrastructure.Persistence.SqlServer;

internal sealed class _003CKargoyeriDbContext_003EFE8CB336D941F77E2D0277DD0E9818290F28BFC1DEF100CA3C7C86F572D2D8342__NotificationMessageConfiguration : IEntityTypeConfiguration<NotificationMessage>
{
	public void Configure(EntityTypeBuilder<NotificationMessage> b)
	{
		RelationalEntityTypeBuilderExtensions.ToTable<NotificationMessage>(b, "Notifications");
		b.HasKey((Expression<Func<NotificationMessage, object>>)((NotificationMessage x) => x.Id));
		b.Property<string>((Expression<Func<NotificationMessage, string>>)((NotificationMessage x) => x.TenantKey)).HasMaxLength(64).IsRequired(true);
		b.HasIndex((Expression<Func<NotificationMessage, object>>)((NotificationMessage x) => x.TenantKey));
		b.Property<string>((Expression<Func<NotificationMessage, string>>)((NotificationMessage x) => x.ShipmentReference)).HasMaxLength(64).IsRequired(true);
		b.HasIndex((Expression<Func<NotificationMessage, object>>)((NotificationMessage x) => x.ShipmentReference));
		b.Property<NotificationEventType>((Expression<Func<NotificationMessage, NotificationEventType>>)((NotificationMessage x) => x.EventType)).HasConversion<string>().HasMaxLength(32);
		b.Property<NotificationChannel>((Expression<Func<NotificationMessage, NotificationChannel>>)((NotificationMessage x) => x.Channel)).HasConversion<string>().HasMaxLength(32);
		b.Property<NotificationDeliveryStatus>((Expression<Func<NotificationMessage, NotificationDeliveryStatus>>)((NotificationMessage x) => x.Status)).HasConversion<string>().HasMaxLength(32);
		b.Property<string>((Expression<Func<NotificationMessage, string>>)((NotificationMessage x) => x.Address)).HasMaxLength(512).IsRequired(true);
		b.Property<string>((Expression<Func<NotificationMessage, string>>)((NotificationMessage x) => x.Subject)).HasMaxLength(512).IsRequired(true);
		RelationalPropertyBuilderExtensions.HasColumnType<string>(b.Property<string>((Expression<Func<NotificationMessage, string>>)((NotificationMessage x) => x.Body)), "nvarchar(max)").IsRequired(true);
		b.Property<string>((Expression<Func<NotificationMessage, string>>)((NotificationMessage x) => x.ErrorMessage)).HasMaxLength(2048);
		b.Property<DateTimeOffset>((Expression<Func<NotificationMessage, DateTimeOffset>>)((NotificationMessage x) => x.CreatedAtUtc)).IsRequired(true);
		b.HasIndex((Expression<Func<NotificationMessage, object>>)((NotificationMessage x) => x.CreatedAtUtc));
	}
}
