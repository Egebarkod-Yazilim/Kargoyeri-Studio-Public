using System.Threading;
using System.Threading.Tasks;

namespace Kargoyeri.Application.Abstractions.Persistence;

public interface IUnitOfWork
{
	Task CommitAsync(CancellationToken cancellationToken);
}
