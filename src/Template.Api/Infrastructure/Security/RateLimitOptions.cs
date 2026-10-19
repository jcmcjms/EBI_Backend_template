namespace Template.Api.Infrastructure.Security;

/// <summary>Rate-limiter settings bound from the "RateLimiting" configuration section.</summary>
public sealed class RateLimitOptions
{
    public const string SectionName = "RateLimiting";

    /// <summary>Requests allowed per client IP per window on the credential endpoints (login/refresh/logout).</summary>
    public int AuthenticationPermitLimit { get; init; } = 10;

    public TimeSpan AuthenticationWindow { get; init; } = TimeSpan.FromMinutes(1);
}

public static class RateLimitOptionsValidator
{
    public static void Validate(RateLimitOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        if (options.AuthenticationPermitLimit < 1)
        {
            throw new InvalidOperationException($"{RateLimitOptions.SectionName}:{nameof(options.AuthenticationPermitLimit)} must be at least 1.");
        }

        if (options.AuthenticationWindow < TimeSpan.FromSeconds(1) || options.AuthenticationWindow > TimeSpan.FromDays(1))
        {
            throw new InvalidOperationException($"{RateLimitOptions.SectionName}:{nameof(options.AuthenticationWindow)} must be between 1 second and 1 day.");
        }
    }
}
