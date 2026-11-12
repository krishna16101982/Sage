using System.Security.Cryptography;
using System.Text;

namespace SageCommonLib.Security;

public static class TokenGenerator
{
    /// <summary>Cryptographically random URL-safe token (default 32 bytes).</summary>
    public static string CreateSecureToken(int bytes = 32)
        => Convert.ToBase64String(RandomNumberGenerator.GetBytes(bytes)).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    /// <summary>SHA-256 hex of a token; only the hash is persisted.</summary>
    public static string Hash(string token) => Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));
}
