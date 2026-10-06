using System.Threading;
using System.Threading.Tasks;
using Kargoyeri.Application.Abstractions.Persistence;

namespace Kargoyeri.Infrastructure.Persistence;

internal sealed class JsonUnitOfWork : IUnitOfWork
{
	private readonly JsonDatabaseSession _session;

	public JsonUnitOfWork(JsonDatabaseSession session)
	{
		_session = session;
	}

	public Task CommitAsync(CancellationToken cancellationToken)
	{
		return _session.CommitAsync(cancellationToken);
	}
}
