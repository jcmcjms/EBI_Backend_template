namespace Template.Api.Infrastructure.Security;

/// <summary>Names of rate-limiter policies applied to endpoints.</summary>
public static class RateLimitPolicies
{
    /// <summary>Per-client-IP fixed window protecting credential endpoints against brute-force attempts.</summary>
    public const string Authentication = "authentication";
}
