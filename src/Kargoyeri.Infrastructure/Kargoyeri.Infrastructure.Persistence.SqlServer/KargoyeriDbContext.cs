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
		modelBuilder.ApplyConfiguration<CargoShipment>((IEntityTypeConfiguration<CargoShipment>)(object)new _003CKargoyeriDbContext_003EFE8CB336D941F77E2D0277DD0E9818290F28BFC1DEF100CA3C7C86F572D2D8342__CargoShipmentConfiguration());
		modelBuilder.ApplyConfiguration<CustomerTenant>((IEntityTypeConfiguration<CustomerTenant>)(object)new _003CKargoyeriDbContext_003EFE8CB336D941F77E2D0277DD0E9818290F28BFC1DEF100CA3C7C86F572D2D8342__CustomerTenantConfiguration());
		modelBuilder.ApplyConfiguration<NotificationMessage>((IEntityTypeConfiguration<NotificationMessage>)(object)new _003CKargoyeriDbContext_003EFE8CB336D941F77E2D0277DD0E9818290F28BFC1DEF100CA3C7C86F572D2D8342__NotificationMessageConfiguration());
		modelBuilder.ApplyConfiguration<ShipmentOperationLog>((IEntityTypeConfiguration<ShipmentOperationLog>)(object)new _003CKargoyeriDbContext_003EFE8CB336D941F77E2D0277DD0E9818290F28BFC1DEF100CA3C7C86F572D2D8342__ShipmentOperationLogConfiguration());
		modelBuilder.ApplyConfiguration<ProviderCredential>((IEntityTypeConfiguration<ProviderCredential>)(object)new _003CKargoyeriDbContext_003EFE8CB336D941F77E2D0277DD0E9818290F28BFC1DEF100CA3C7C86F572D2D8342__ProviderCredentialConfiguration());
	}
}
