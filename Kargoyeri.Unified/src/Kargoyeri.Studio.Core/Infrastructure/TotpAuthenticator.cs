using System.Security.Cryptography;

namespace Kargoyeri.Studio.Core.Infrastructure;

/// <summary>
/// RFC 6238 TOTP (HMAC-SHA1, 30 sn pencere, 6 hane) implementasyonu.
/// Google Authenticator, Microsoft Authenticator, Authy gibi standart uygulamalarla uyumludur.
/// </summary>
public static class TotpAuthenticator
{
    private const int    DigitCount      = 6;
    private const int    StepSeconds     = 30;
    private const int    SecretByteCount = 20; // 160 bit — Google Authenticator standardi
    private const string Base32Alphabet  = "ABCDEFGHIJKLMNOPQRSTUVWXYZ234567";

    /// <summary>Yeni rastgele Base32 kodlanmis secret uretir (160 bit).</summary>
    public static string GenerateSecret()
    {
        var bytes = RandomNumberGenerator.GetBytes(SecretByteCount);
        return Base32Encode(bytes);
    }

    /// <summary>otpauth:// URI uretir — QR kod kutuphanesi ile QR'a donusturulebilir.</summary>
    public static string BuildProvisioningUri(string secret, string accountName, string issuer)
    {
        var encIssuer  = Uri.EscapeDataString(issuer);
        var encAccount = Uri.EscapeDataString(accountName);
        return $"otpauth://totp/{encIssuer}:{encAccount}?secret={secret}&issuer={encIssuer}&algorithm=SHA1&digits={DigitCount}&period={StepSeconds}";
    }

    /// <summary>
    /// Verilen kodu mevcut zaman penceresine ve ±1 step toleransa karsi dogrular.
    /// </summary>
    public static bool Verify(string secret, string code)
    {
        if (string.IsNullOrWhiteSpace(secret) || string.IsNullOrWhiteSpace(code)) return false;
        code = code.Trim();
        if (code.Length != DigitCount || !code.All(char.IsDigit)) return false;

        byte[] key;
        try { key = Base32Decode(secret); }
        catch { return false; }

        var unixSeconds = DateTimeOffset.UtcNow.ToUnixTimeSeconds();
        var step        = unixSeconds / StepSeconds;

        for (int offset = -1; offset <= 1; offset++)
        {
            var candidate = ComputeCode(key, step + offset);
            if (CryptographicOperations.FixedTimeEquals(
                    System.Text.Encoding.ASCII.GetBytes(candidate),
                    System.Text.Encoding.ASCII.GetBytes(code)))
                return true;
        }
        return false;
    }

    private static string ComputeCode(byte[] key, long counter)
    {
        var counterBytes = BitConverter.GetBytes(counter);
        if (BitConverter.IsLittleEndian) Array.Reverse(counterBytes);

        using var hmac = new HMACSHA1(key);
        var hash       = hmac.ComputeHash(counterBytes);
        var offset     = hash[^1] & 0x0F;
        var binary     =
              ((hash[offset]     & 0x7F) << 24)
            | ((hash[offset + 1] & 0xFF) << 16)
            | ((hash[offset + 2] & 0xFF) <<  8)
            |  (hash[offset + 3] & 0xFF);

        var otp = binary % (int)Math.Pow(10, DigitCount);
        return otp.ToString(new string('0', DigitCount));
    }

    // ── Base32 encode/decode ───────────────────────────────────────────────────

    private static string Base32Encode(byte[] data)
    {
        if (data.Length == 0) return string.Empty;
        var sb     = new System.Text.StringBuilder(((data.Length * 8) + 4) / 5);
        int buffer = data[0];
        int next   = 1;
        int bitsLeft = 8;
        while (bitsLeft > 0 || next < data.Length)
        {
            if (bitsLeft < 5)
            {
                if (next < data.Length)
                {
                    buffer    = (buffer << 8) | (data[next++] & 0xFF);
                    bitsLeft += 8;
                }
                else
                {
                    var pad   = 5 - bitsLeft;
                    buffer  <<= pad;
                    bitsLeft += pad;
                }
            }
            var index = 0x1F & (buffer >> (bitsLeft - 5));
            bitsLeft -= 5;
            sb.Append(Base32Alphabet[index]);
        }
        return sb.ToString();
    }

    private static byte[] Base32Decode(string input)
    {
        input = input.Trim().TrimEnd('=').ToUpperInvariant().Replace(" ", "");
        if (input.Length == 0) return Array.Empty<byte>();

        var output    = new byte[input.Length * 5 / 8];
        int buffer    = 0;
        int bitsLeft  = 0;
        int outIdx    = 0;
        foreach (var c in input)
        {
            var idx = Base32Alphabet.IndexOf(c);
            if (idx < 0) throw new FormatException($"Gecersiz Base32 karakter: {c}");
            buffer    = (buffer << 5) | idx;
            bitsLeft += 5;
            if (bitsLeft >= 8)
            {
                output[outIdx++] = (byte)((buffer >> (bitsLeft - 8)) & 0xFF);
                bitsLeft -= 8;
            }
        }
        return output;
    }
}
