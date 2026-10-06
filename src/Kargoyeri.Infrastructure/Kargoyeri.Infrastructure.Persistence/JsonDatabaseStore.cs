using System.IO;
using System.Text.Json;
using System.Threading;
using System.Threading.Tasks;

namespace Kargoyeri.Infrastructure.Persistence;

internal sealed class JsonDatabaseStore
{
	private static readonly JsonSerializerOptions SerializerOptions = new JsonSerializerOptions
	{
		WriteIndented = true,
		PropertyNameCaseInsensitive = true
	};

	private readonly string _filePath;

	private readonly SemaphoreSlim _commitGate = new SemaphoreSlim(1, 1);

	public JsonDatabaseStore(string filePath)
	{
		_filePath = filePath;
		string directoryName = Path.GetDirectoryName(_filePath);
		if (!string.IsNullOrWhiteSpace(directoryName))
		{
			Directory.CreateDirectory(directoryName);
		}
	}

	public async Task<JsonDatabaseDocument> LoadAsync(CancellationToken cancellationToken)
	{
		if (!File.Exists(_filePath))
		{
			return new JsonDatabaseDocument();
		}
		FileStream stream = File.OpenRead(_filePath);
		JsonDatabaseDocument result;
		try
		{
			result = (await JsonSerializer.DeserializeAsync<JsonDatabaseDocument>(stream, SerializerOptions, cancellationToken)) ?? new JsonDatabaseDocument();
		}
		finally
		{
			if (stream != null)
			{
				await stream.DisposeAsync();
			}
		}
		return result;
	}

	public async Task SaveAsync(JsonDatabaseDocument document, CancellationToken cancellationToken)
	{
		await _commitGate.WaitAsync(cancellationToken);
		try
		{
			FileStream stream = File.Create(_filePath);
			try
			{
				await JsonSerializer.SerializeAsync(stream, document, SerializerOptions, cancellationToken);
			}
			finally
			{
				if (stream != null)
				{
					await stream.DisposeAsync();
				}
			}
		}
		finally
		{
			_commitGate.Release();
		}
	}
}
