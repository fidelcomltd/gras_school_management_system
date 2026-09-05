using System.Security.Cryptography;
using System.Text;

namespace SchoolManagement.Application.Auth;

/// <summary>
/// Generates and hashes the opaque session token (spec 9.1): "Session tokens are 32 bytes from a
/// cryptographically secure source, stored hashed server-side."
/// </summary>
/// <remarks>
/// Pure functions over the base class library only — no external state, so no abstraction/seam is
/// needed for testing beyond checking the shape of what comes out.
/// </remarks>
public static class SessionTokens
{
    /// <summary>Raw token length in bytes, fixed by spec 9.1.</summary>
    public const int TokenLengthBytes = 32;

    /// <summary>
    /// Generates a fresh 32-byte CSPRNG token, base64url-encoded so it is a safe, compact cookie value.
    /// </summary>
    public static string GenerateRawToken()
    {
        Span<byte> bytes = stackalloc byte[TokenLengthBytes];
        RandomNumberGenerator.Fill(bytes);
        return Base64UrlEncode(bytes);
    }

    /// <summary>
    /// Hashes a raw token for storage/lookup. SHA-256 is sufficient here — unlike a password hash, a
    /// CSPRNG token has no low-entropy space for an attacker to search; the point of hashing it is
    /// purely so a database read (backup, log, compromise) does not hand over a live credential.
    /// </summary>
    public static string HashToken(string rawToken)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(rawToken);

        var digest = SHA256.HashData(Encoding.UTF8.GetBytes(rawToken));
        return Convert.ToHexStringLower(digest);
    }

    private static string Base64UrlEncode(ReadOnlySpan<byte> bytes) =>
        Convert.ToBase64String(bytes)
            .TrimEnd('=')
            .Replace('+', '-')
            .Replace('/', '_');
}
