using System.Security.Cryptography;
using Microsoft.Extensions.Options;

namespace Template.Api.Infrastructure.Security;

/// <summary>PBKDF2 parameters for password hashing. Bound from the "PasswordHasher" configuration section.</summary>
public sealed class PasswordHasherOptions
{
    public const string SectionName = "PasswordHasher";
    public const int MinimumIterationCount = 100_000;

    /// <summary>Iteration count for new hashes. Defaults to the current OWASP recommendation for PBKDF2-HMAC-SHA256.</summary>
    public int IterationCount { get; init; } = 600_000;
}

/// <summary>Password policy for user-supplied passwords. Bound from the "PasswordPolicy" configuration section.</summary>
public sealed class PasswordPolicyOptions
{
    public const string SectionName = "PasswordPolicy";
    public const int TransportMaxLength = 128;

    public int MinLength { get; init; } = 12;

    public int MaxLength { get; init; } = TransportMaxLength;
}

public static class PasswordHasherOptionsValidator
{
    public static void Validate(PasswordHasherOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        if (options.IterationCount < PasswordHasherOptions.MinimumIterationCount)
        {
            throw new InvalidOperationException(
                $"{PasswordHasherOptions.SectionName}:{nameof(options.IterationCount)} must be at least {PasswordHasherOptions.MinimumIterationCount}.");
        }
    }
}

public static class PasswordPolicyOptionsValidator
{
    public static void Validate(PasswordPolicyOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        if (options.MinLength < 8)
        {
            throw new InvalidOperationException($"{PasswordPolicyOptions.SectionName}:{nameof(options.MinLength)} must be at least 8.");
        }

        if (options.MaxLength < options.MinLength || options.MaxLength > PasswordPolicyOptions.TransportMaxLength)
        {
            throw new InvalidOperationException(
                $"{PasswordPolicyOptions.SectionName}:{nameof(options.MaxLength)} must be between {nameof(options.MinLength)} and {PasswordPolicyOptions.TransportMaxLength}.");
        }
    }
}

/// <summary>
/// Framework-native PBKDF2 password hashing (no external crypto packages).
/// Hashes are self-describing (<c>PBKDF2-SHA256$iterations$salt$subkey</c>) so the iteration count
/// can be raised later without invalidating stored hashes; see <see cref="NeedsRehash"/>.
/// </summary>
public sealed class PasswordHasher
{
    private const string Prefix = "PBKDF2-SHA256";
    private const int SaltSize = 16;
    private const int SubKeySize = 32;

    private readonly PasswordHasherOptions _options;

    public PasswordHasher(IOptions<PasswordHasherOptions> options)
    {
        ArgumentNullException.ThrowIfNull(options);
        _options = options.Value;
    }

    public string Hash(string password)
    {
        ArgumentException.ThrowIfNullOrEmpty(password);

        var salt = RandomNumberGenerator.GetBytes(SaltSize);
        var subKey = Rfc2898DeriveBytes.Pbkdf2(password, salt, _options.IterationCount, HashAlgorithmName.SHA256, SubKeySize);

        return string.Join(
            '$',
            Prefix,
            _options.IterationCount.ToString(System.Globalization.CultureInfo.InvariantCulture),
            Convert.ToBase64String(salt),
            Convert.ToBase64String(subKey));
    }

    /// <summary>Verifies a password against a stored hash in constant time. Returns false for malformed hashes.</summary>
    public bool Verify(string password, string encodedHash)
    {
        if (string.IsNullOrEmpty(password) ||
            !TryParse(encodedHash, out var iterations, out var salt, out var expectedSubKey))
        {
            return false;
        }

        var actualSubKey = Rfc2898DeriveBytes.Pbkdf2(password, salt, iterations, HashAlgorithmName.SHA256, expectedSubKey.Length);
        return CryptographicOperations.FixedTimeEquals(actualSubKey, expectedSubKey);
    }

    /// <summary>True when a stored hash was produced with weaker parameters than currently configured (rehash on next successful login).</summary>
    public bool NeedsRehash(string encodedHash) =>
        !TryParse(encodedHash, out var iterations, out _, out _) || iterations < _options.IterationCount;

    private static bool TryParse(string encodedHash, out int iterations, out byte[] salt, out byte[] subKey)
    {
        iterations = 0;
        salt = [];
        subKey = [];

        if (string.IsNullOrWhiteSpace(encodedHash))
        {
            return false;
        }

        var parts = encodedHash.Split('$');
        if (parts.Length != 4 || !string.Equals(parts[0], Prefix, StringComparison.Ordinal))
        {
            return false;
        }

        if (!int.TryParse(parts[1], System.Globalization.NumberStyles.Integer, System.Globalization.CultureInfo.InvariantCulture, out var parsedIterations) ||
            parsedIterations < PasswordHasherOptions.MinimumIterationCount)
        {
            return false;
        }

        try
        {
            var parsedSalt = Convert.FromBase64String(parts[2]);
            var parsedSubKey = Convert.FromBase64String(parts[3]);
            if (parsedSalt.Length != SaltSize || parsedSubKey.Length != SubKeySize)
            {
                return false;
            }

            iterations = parsedIterations;
            salt = parsedSalt;
            subKey = parsedSubKey;
            return true;
        }
        catch (FormatException)
        {
            return false;
        }
    }
}
