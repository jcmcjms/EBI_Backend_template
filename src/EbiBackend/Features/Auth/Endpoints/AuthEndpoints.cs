using System.Security.Claims;
using EbiBackend.Features.Auth.Contracts;
using EbiBackend.Features.Auth.Services;
using EbiBackend.Features.Auth.Validation;

namespace EbiBackend.Features.Auth.Endpoints;

public static class AuthEndpoints
{
    public static void MapAuthEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/auth").WithTags("Auth");

        group.MapPost("/register", async (
            RegisterRequest request,
            IAuthService authService,
            CancellationToken cancellationToken) =>
        {
            var validationErrors = AuthValidator.ValidateRegisterRequest(request);
            if (validationErrors.Length > 0)
                return Results.BadRequest(new { Errors = validationErrors });

            var result = await authService.RegisterAsync(request, cancellationToken);

            return result.IsSuccess
                ? Results.Created($"/auth/me", result.Value)
                : Results.Conflict(new { Error = result.Error });
        })
        .WithName("Register")
        .Produces<UserProfileResponse>(StatusCodes.Status201Created)
        .Produces(StatusCodes.Status400BadRequest)
        .Produces(StatusCodes.Status409Conflict);

        group.MapPost("/login", async (
            LoginRequest request,
            IAuthService authService,
            CancellationToken cancellationToken) =>
        {
            var validationErrors = AuthValidator.ValidateLoginRequest(request);
            if (validationErrors.Length > 0)
                return Results.BadRequest(new { Errors = validationErrors });

            var result = await authService.LoginAsync(request, cancellationToken);

            return result.IsSuccess
                ? Results.Ok(result.Value)
                : Results.Unauthorized();
        })
        .WithName("Login")
        .Produces<TokenResponse>(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status400BadRequest)
        .Produces(StatusCodes.Status401Unauthorized);

        group.MapPost("/refresh", async (
            RefreshRequest request,
            IAuthService authService,
            CancellationToken cancellationToken) =>
        {
            var validationErrors = AuthValidator.ValidateRefreshRequest(request);
            if (validationErrors.Length > 0)
                return Results.BadRequest(new { Errors = validationErrors });

            var result = await authService.RefreshAsync(request, cancellationToken);

            return result.IsSuccess
                ? Results.Ok(result.Value)
                : Results.Unauthorized();
        })
        .WithName("Refresh")
        .Produces<TokenResponse>(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status400BadRequest)
        .Produces(StatusCodes.Status401Unauthorized);

        group.MapGet("/me", async (
            ClaimsPrincipal user,
            IAuthService authService,
            CancellationToken cancellationToken) =>
        {
            var userIdClaim = user.FindFirst(ClaimTypes.NameIdentifier)
                              ?? user.FindFirst("sub");

            if (userIdClaim is null || !Guid.TryParse(userIdClaim.Value, out var userId))
                return Results.Unauthorized();

            var result = await authService.GetProfileAsync(userId, cancellationToken);

            return result.IsSuccess
                ? Results.Ok(result.Value)
                : Results.NotFound(new { Error = result.Error });
        })
        .RequireAuthorization()
        .WithName("GetProfile")
        .Produces<UserProfileResponse>(StatusCodes.Status200OK)
        .Produces(StatusCodes.Status401Unauthorized)
        .Produces(StatusCodes.Status404NotFound);
    }
}