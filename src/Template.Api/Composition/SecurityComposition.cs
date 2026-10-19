using System.Threading.RateLimiting;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Template.Api.Infrastructure.Authorization;
using Template.Api.Infrastructure.Http;
using Template.Api.Infrastructure.Security;

namespace Template.Api.Composition;

/// <summary>
/// Registers security concerns: token/auth options (validated eagerly at startup), password hashing,
/// JWT authentication, permission-based authorization, authentication rate limiting, and CORS.
/// </summary>
public static class SecurityComposition
{
    /// <summary>Name of the CORS policy built from <c>Cors:AllowedOrigins</c>. Only registered when origins are configured.</summary>
    public const string CorsPolicyName = "configured-origins";

    public static IServiceCollection AddSecurity(this IServiceCollection services, IConfiguration configuration, IHostEnvironment environment)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentNullException.ThrowIfNull(environment);

        var authOptions = configuration.GetSection(AuthOptions.SectionName).Get<AuthOptions>() ?? new AuthOptions();
        AuthOptionsValidator.Validate(authOptions, environment);
        services.AddSingleton(Options.Create(authOptions));

        var passwordHasherOptions = configuration.GetSection(PasswordHasherOptions.SectionName).Get<PasswordHasherOptions>() ?? new PasswordHasherOptions();
        PasswordHasherOptionsValidator.Validate(passwordHasherOptions);
        services.AddSingleton(Options.Create(passwordHasherOptions));
        services.AddSingleton<PasswordHasher>();

        var passwordPolicyOptions = configuration.GetSection(PasswordPolicyOptions.SectionName).Get<PasswordPolicyOptions>() ?? new PasswordPolicyOptions();
        PasswordPolicyOptionsValidator.Validate(passwordPolicyOptions);
        services.AddSingleton(Options.Create(passwordPolicyOptions));

        services.AddSingleton<JwtAccessTokenIssuer>();

        services.AddAuthentication(JwtBearerDefaults.AuthenticationScheme)
            .AddJwtBearer(options =>
            {
                // Keep the claim types exactly as issued ("sub", "email", "role", "perm").
                options.MapInboundClaims = false;
                options.TokenValidationParameters = new TokenValidationParameters
                {
                    ValidateIssuer = true,
                    ValidIssuer = authOptions.Issuer,
                    ValidateAudience = true,
                    ValidAudience = authOptions.Audience,
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = JwtAccessTokenIssuer.CreateSigningKey(authOptions),
                    ValidateLifetime = true,
                    // Small skew tolerates clock differences between instances; access tokens are short-lived.
                    ClockSkew = TimeSpan.FromSeconds(30),
                    RoleClaimType = AuthClaims.Role,
                    NameClaimType = AuthClaims.Email,
                };
                options.Events = new JwtBearerEvents
                {
                    OnChallenge = async context =>
                    {
                        context.HandleResponse();
                        await ProblemDetailsResponses.WriteAsync(
                            context.HttpContext,
                            StatusCodes.Status401Unauthorized,
                            "Unauthenticated",
                            "A valid access token is required for this endpoint.");
                    },
                    OnForbidden = async context =>
                    {
                        await ProblemDetailsResponses.WriteAsync(
                            context.HttpContext,
                            StatusCodes.Status403Forbidden,
                            "Forbidden",
                            "The authenticated principal does not have the permissions required by this endpoint.");
                    },
                };
            });

        services.AddAuthorization(options =>
        {
            options.AddPolicy(
                AuthorizationPolicies.RequirePermissions,
                policy => policy.RequireAuthenticatedUser().AddRequirements(new PermissionsRequirement()));
        });
        services.AddSingleton<IAuthorizationHandler, PermissionsAuthorizationHandler>();

        services.AddRateLimiter(options =>
        {
            options.RejectionStatusCode = StatusCodes.Status429TooManyRequests;
            options.OnRejected = async (context, cancellationToken) =>
                await ProblemDetailsResponses.WriteAsync(
                    context.HttpContext,
                    StatusCodes.Status429TooManyRequests,
                    "Too many requests",
                    "The rate limit for this endpoint was exceeded. Retry later.",
                    cancellationToken);

            var rateLimitOptions = configuration.GetSection(RateLimitOptions.SectionName).Get<RateLimitOptions>() ?? new RateLimitOptions();
            RateLimitOptionsValidator.Validate(rateLimitOptions);

            // Brute-force protection for credential endpoints, partitioned per client IP.
            options.AddPolicy(RateLimitPolicies.Authentication, httpContext =>
                RateLimitPartition.GetFixedWindowLimiter(
                    partitionKey: httpContext.Connection.RemoteIpAddress?.ToString() ?? "unknown",
                    factory: _ => new FixedWindowRateLimiterOptions
                    {
                        PermitLimit = rateLimitOptions.AuthenticationPermitLimit,
                        Window = rateLimitOptions.AuthenticationWindow,
                        AutoReplenishment = true,
                        QueueLimit = 0,
                    }));
        });

        var allowedOrigins = configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];
        if (allowedOrigins.Length > 0)
        {
            services.AddCors(options => options.AddPolicy(
                CorsPolicyName,
                policy => policy.WithOrigins(allowedOrigins).AllowAnyHeader().AllowAnyMethod()));
        }

        return services;
    }
}
