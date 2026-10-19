using System.Buffers.Text;
using System.Security.Cryptography;
using System.Text;

namespace Template.Api.Infrastructure.Security;

/// <summary>
/// Generates opaque refresh-token secrets and computes their storage hashes.
/// Only the SHA-256 hash is persisted, so a database leak does not expose usable tokens.
/// </summary>
public static class RefreshTokenGenerator
{
    private const int TokenSizeInBytes = 32;

    public static string Generate() => Base64Url.EncodeToString(RandomNumberGenerator.GetBytes(TokenSizeInBytes));

    public static string ComputeHash(string token)
    {
        ArgumentException.ThrowIfNullOrEmpty(token);
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(token)));
    }
}
