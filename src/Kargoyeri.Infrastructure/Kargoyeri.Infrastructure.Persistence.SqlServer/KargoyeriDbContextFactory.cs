using System;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Design;
using Microsoft.EntityFrameworkCore.Infrastructure;
using Microsoft.EntityFrameworkCore.SqlServer.Infrastructure.Internal;

namespace Kargoyeri.Infrastructure.Persistence.SqlServer;

public sealed class KargoyeriDbContextFactory : IDesignTimeDbContextFactory<KargoyeriDbContext>
{
	public KargoyeriDbContext CreateDbContext(string[] args)
	{
		DbContextOptions<KargoyeriDbContext> options = SqlServerDbContextOptionsExtensions.UseSqlServer<KargoyeriDbContext>(new DbContextOptionsBuilder<KargoyeriDbContext>(), "Server=(localdb)\\mssqllocaldb;Database=KargoyeriDev;Trusted_Connection=True;", (Action<SqlServerDbContextOptionsBuilder>)delegate(SqlServerDbContextOptionsBuilder sql)
		{
			((RelationalDbContextOptionsBuilder<SqlServerDbContextOptionsBuilder, SqlServerOptionsExtension>)(object)sql).MigrationsAssembly(typeof(KargoyeriDbContext).Assembly.FullName);
		}).Options;
		return new KargoyeriDbContext(options);
	}
}
