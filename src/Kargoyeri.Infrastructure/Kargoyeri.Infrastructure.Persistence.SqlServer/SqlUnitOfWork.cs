using System.Threading;
using System.Threading.Tasks;
using Kargoyeri.Application.Abstractions.Persistence;
using Microsoft.EntityFrameworkCore;

namespace Kargoyeri.Infrastructure.Persistence.SqlServer;

internal sealed class SqlUnitOfWork : IUnitOfWork
{
	private readonly KargoyeriDbContext _db;

	public SqlUnitOfWork(KargoyeriDbContext db)
	{
		_db = db;
	}

	public async Task CommitAsync(CancellationToken cancellationToken)
	{
		await ((DbContext)_db).SaveChangesAsync(cancellationToken);
	}
}
