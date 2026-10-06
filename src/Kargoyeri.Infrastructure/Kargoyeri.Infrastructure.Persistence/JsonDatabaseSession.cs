using System.Threading;
using System.Threading.Tasks;

namespace Kargoyeri.Infrastructure.Persistence;

internal sealed class JsonDatabaseSession
{
	private readonly JsonDatabaseStore _store;

	private JsonDatabaseDocument? _document;

	public JsonDatabaseSession(JsonDatabaseStore store)
	{
		_store = store;
	}

	public async Task<JsonDatabaseDocument> GetDocumentAsync(CancellationToken cancellationToken)
	{
		JsonDatabaseDocument document = _document;
		JsonDatabaseDocument jsonDatabaseDocument = document;
		if (jsonDatabaseDocument == null)
		{
			jsonDatabaseDocument = (_document = await _store.LoadAsync(cancellationToken));
		}
		_ = jsonDatabaseDocument;
		return _document;
	}

	public async Task CommitAsync(CancellationToken cancellationToken)
	{
		if (_document != null)
		{
			await _store.SaveAsync(_document, cancellationToken);
		}
	}
}
