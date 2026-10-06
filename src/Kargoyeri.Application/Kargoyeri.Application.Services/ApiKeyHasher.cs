using System;
using System.Security.Cryptography;
using System.Text;

namespace Kargoyeri.Application.Services;

public static class ApiKeyHasher
{
	public static string Hash(string apiKey)
	{
		byte[] inArray = SHA256.HashData(Encoding.UTF8.GetBytes(apiKey));
		return Convert.ToHexString(inArray);
	}
}
