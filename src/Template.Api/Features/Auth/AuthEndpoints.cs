using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Template.Api.Infrastructure.Security;

namespace Template.Api.Features.Auth;

/// <summary>
/// Credential endpoints: login, refresh-token rotation, and logout. Anonymous by design and
/// rate limited per client IP as brute-force protection.
/// </summary>
public static class AuthEndpoints
{
    public static IEndpointRouteBuilder MapAuthEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/auth")
            .WithTags("Auth")
            .WithSummary("Authentication: obtain, rotate, and revoke tokens.")
            .AllowAnonymous()
            .RequireRateLimiting(RateLimitPolicies.Authentication);

        group.MapPost("/login", async (LoginRequest request, LoginUseCase useCase, CancellationToken cancellationToken) =>
            {
                var outcome = await useCase.ExecuteAsync(request, cancellationToken);
                return outcome switch
                {
                    LoginOutcome.Success success => Results.Ok(success.Tokens),
                    _ => Results.Problem(
                        statusCode: StatusCodes.Status401Unauthorized,
                        title: "Invalid credentials",
                        detail: "The email/password combination was not accepted."),
                };
            })
            .Produces<AuthTokenResponse>(StatusCodes.Status200OK)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .WithSummary("Exchanges credentials for an access token and a refresh token.");

        group.MapPost("/refresh", async (RefreshSessionRequest request, RefreshSessionUseCase useCase, CancellationToken cancellationToken) =>
            {
                var outcome = await useCase.ExecuteAsync(request, cancellationToken);
                return outcome switch
                {
                    RefreshSessionOutcome.Success success => Results.Ok(success.Tokens),
                    _ => Results.Problem(
                        statusCode: StatusCodes.Status401Unauthorized,
                        title: "Invalid refresh token",
                        detail: "The refresh token is unknown, expired, or has already been used."),
                };
            })
            .Produces<AuthTokenResponse>(StatusCodes.Status200OK)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .WithSummary("Rotates a refresh token and returns a new token pair. Reusing a rotated token revokes all of the user's sessions.");

        group.MapPost("/logout", async (LogoutRequest request, LogoutUseCase useCase, CancellationToken cancellationToken) =>
            {
                await useCase.ExecuteAsync(request, cancellationToken);
                return Results.NoContent();
            })
            .Produces(StatusCodes.Status204NoContent)
            .ProducesValidationProblem()
            .WithSummary("Revokes the presented refresh token. Idempotent: unknown tokens also return 204.");

        return endpoints;
    }
}
