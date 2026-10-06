using System;
using System.Linq.Expressions;
using System.Text.Json;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Storage.ValueConversion;

namespace Kargoyeri.Infrastructure.Persistence.SqlServer;

internal static class _003CKargoyeriDbContext_003EFE8CB336D941F77E2D0277DD0E9818290F28BFC1DEF100CA3C7C86F572D2D8342__JsonConvert
{
	private static readonly JsonSerializerOptions Opts = new JsonSerializerOptions
	{
		WriteIndented = false,
		PropertyNamingPolicy = JsonNamingPolicy.CamelCase
	};

	public static ValueConverter<T, string> Converter<T>() where T : new()
	{
		return new ValueConverter<T, string>((Expression<Func<T, string>>)((T v) => JsonSerializer.Serialize(v, Opts)), (Expression<Func<string, T>>)((string s) => (T)(string.IsNullOrEmpty(s) ? ((object)new T()) : ((object)(JsonSerializer.Deserialize<T>(s, Opts) ?? new T())))), (ConverterMappingHints)null);
	}

	public static ValueComparer<T> ListComparer<T>()
	{
		return new ValueComparer<T>((Expression<Func<T, T, bool>>)((T a, T b) => JsonSerializer.Serialize(a, Opts) == JsonSerializer.Serialize(b, Opts)), (Expression<Func<T, int>>)((T v) => ((object)v == null) ? 0 : JsonSerializer.Serialize(v, Opts).GetHashCode()), (Expression<Func<T, T>>)((T v) => (T)(((object)v == null) ? ((object)default(T)) : ((object)JsonSerializer.Deserialize<T>(JsonSerializer.Serialize(v, Opts), Opts)))));
	}
}
