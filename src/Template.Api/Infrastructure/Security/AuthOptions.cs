namespace Template.Api.Infrastructure.Security;

/// <summary>
/// Strongly-typed authentication settings bound from the "Auth" configuration section.
/// Values are validated eagerly at startup by <see cref="AuthOptionsValidator"/>.
/// </summary>
public sealed class AuthOptions
{
    public const string SectionName = "Auth";

    /// <summary>
    /// Development-only signing key shipped with the template. It is public knowledge and is
    /// rejected by <see cref="AuthOptionsValidator"/> when the host environment is Production.
    /// </summary>
    public const string DevelopmentSigningKey = "dev-only-signing-key-not-for-production-use-change-me-0123456789";

    public string Issuer { get; init; } = string.Empty;

    public string Audience { get; init; } = string.Empty;

    /// <summary>Symmetric signing key (HS256). Must be at least 32 characters and must come from a secret store in production.</summary>
    public string SigningKey { get; init; } = string.Empty;

    public TimeSpan AccessTokenLifetime { get; init; } = TimeSpan.FromMinutes(15);

    public TimeSpan RefreshTokenLifetime { get; init; } = TimeSpan.FromDays(14);
}

/// <summary>Fails startup when authentication settings are missing, weak, or development-only in Production.</summary>
public static class AuthOptionsValidator
{
    public const int MinimumSigningKeyLength = 32;

    public static void Validate(AuthOptions options, IHostEnvironment environment)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(environment);

        Require(options.Issuer, nameof(options.Issuer));
        Require(options.Audience, nameof(options.Audience));
        Require(options.SigningKey, nameof(options.SigningKey));

        if (options.SigningKey.Length < MinimumSigningKeyLength)
        {
            throw new InvalidOperationException(
                $"{AuthOptions.SectionName}:{nameof(options.SigningKey)} must be at least {MinimumSigningKeyLength} characters to be used with HS256.");
        }

        if (environment.IsProduction() && options.SigningKey == AuthOptions.DevelopmentSigningKey)
        {
            throw new InvalidOperationException(
                $"{AuthOptions.SectionName}:{nameof(options.SigningKey)} is the public development key. Configure a secret production signing key.");
        }

        if (options.AccessTokenLifetime < TimeSpan.FromMinutes(1) || options.AccessTokenLifetime > TimeSpan.FromHours(1))
        {
            throw new InvalidOperationException(
                $"{AuthOptions.SectionName}:{nameof(options.AccessTokenLifetime)} must be between 1 minute and 1 hour.");
        }

        if (options.RefreshTokenLifetime < TimeSpan.FromHours(1) || options.RefreshTokenLifetime > TimeSpan.FromDays(90))
        {
            throw new InvalidOperationException(
                $"{AuthOptions.SectionName}:{nameof(options.RefreshTokenLifetime)} must be between 1 hour and 90 days.");
        }

        if (options.RefreshTokenLifetime <= options.AccessTokenLifetime)
        {
            throw new InvalidOperationException(
                $"{AuthOptions.SectionName}:{nameof(options.RefreshTokenLifetime)} must be greater than {nameof(options.AccessTokenLifetime)}.");
        }
    }

    private static void Require(string value, string name)
    {
        if (string.IsNullOrWhiteSpace(value))
        {
            throw new InvalidOperationException($"{AuthOptions.SectionName}:{name} is required.");
        }
    }
}
