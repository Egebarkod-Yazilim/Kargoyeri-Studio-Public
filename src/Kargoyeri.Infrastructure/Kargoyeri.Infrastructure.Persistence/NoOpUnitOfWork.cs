using System.Threading;
using System.Threading.Tasks;
using Kargoyeri.Application.Abstractions.Persistence;

namespace Kargoyeri.Infrastructure.Persistence;

internal sealed class NoOpUnitOfWork : IUnitOfWork
{
	public Task CommitAsync(CancellationToken cancellationToken)
	{
		return Task.CompletedTask;
	}
}
