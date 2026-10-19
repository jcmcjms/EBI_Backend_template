using System.Security.Claims;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Template.Api.Features.Users.Domain;
using Template.Api.Infrastructure.Authorization;
using Template.Api.Infrastructure.Http;
using Template.Api.Infrastructure.Paging;
using Template.Api.Infrastructure.Security;

namespace Template.Api.Features.Users;

/// <summary>
/// User endpoints. Everything under /api/users requires authentication; administration operations
/// additionally require catalog permissions via <c>RequirePermission</c>.
/// </summary>
public static class UsersEndpoints
{
    public static IEndpointRouteBuilder MapUsersEndpoints(this IEndpointRouteBuilder endpoints)
    {
        var group = endpoints.MapGroup("/api/users")
            .WithTags("Users")
            .RequireAuthorization();

        group.MapGet("/me", async (ClaimsPrincipal principal, GetCurrentUserUseCase useCase, CancellationToken cancellationToken) =>
            {
                var outcome = await useCase.ExecuteAsync(principal, cancellationToken);
                return outcome switch
                {
                    GetCurrentUserOutcome.Success success => Results.Ok(success.User),
                    GetCurrentUserOutcome.NotFound => UserNotFound(),
                    _ => throw new UnreachableOutcomeException(outcome),
                };
            })
            .Produces<CurrentUserResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .WithSummary("Returns the authenticated principal's profile. Roles/permissions come from token claims; profile fields are read from the database.");

        group.MapPost("/me/password", async (ChangeOwnPasswordRequest request, ClaimsPrincipal principal, ChangeOwnPasswordUseCase useCase, CancellationToken cancellationToken) =>
            {
                var outcome = await useCase.ExecuteAsync(principal.GetUserId(), request, cancellationToken);
                return outcome switch
                {
                    ChangeOwnPasswordOutcome.Success => Results.NoContent(),
                    ChangeOwnPasswordOutcome.InvalidCurrentPassword => Results.Problem(
                        statusCode: StatusCodes.Status400BadRequest,
                        title: "Invalid current password",
                        detail: "The current password was not accepted."),
                    ChangeOwnPasswordOutcome.WeakPassword weak => PasswordProblem(weak.MinLength),
                    _ => throw new UnreachableOutcomeException(outcome),
                };
            })
            .Produces(StatusCodes.Status204NoContent)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .WithSummary("Changes the authenticated user's password and revokes all refresh sessions.");

        group.MapGet("", async ([AsParameters] PagedRequest paging, ListUsersUseCase useCase, CancellationToken cancellationToken) =>
                Results.Ok(await useCase.ExecuteAsync(paging, cancellationToken)))
            .RequirePermission(Permissions.UsersRead)
            .Produces<PagedResponse<UserResponse>>(StatusCodes.Status200OK)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .WithSummary("Lists users (paged).");

        group.MapPost("", async (CreateUserRequest request, CreateUserUseCase useCase, CancellationToken cancellationToken) =>
            {
                var outcome = await useCase.ExecuteAsync(request, cancellationToken);
                return outcome switch
                {
                    CreateUserOutcome.Success success => Results.Created($"/api/users/{success.User.Id}", success.User),
                    CreateUserOutcome.EmailAlreadyExists => EmailConflict(),
                    CreateUserOutcome.UsernameAlreadyExists => UsernameConflict(),
                    CreateUserOutcome.InvalidUsername => InvalidUsernameProblem(),
                    CreateUserOutcome.WeakPassword weak => PasswordProblem(weak.MinLength),
                    CreateUserOutcome.UnknownRoles unknown => Results.Problem(
                        statusCode: StatusCodes.Status400BadRequest,
                        title: "Unknown roles",
                        detail: $"Role ids do not exist: {string.Join(", ", unknown.RoleIds)}."),
                    _ => throw new UnreachableOutcomeException(outcome),
                };
            })
            .RequirePermission(Permissions.UsersWrite)
            .Produces<UserResponse>(StatusCodes.Status201Created)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .WithSummary("Creates a user with profile details and optional role assignments.");

        group.MapGet("/{userId:guid}", async (Guid userId, GetUserUseCase useCase, CancellationToken cancellationToken) =>
            {
                var outcome = await useCase.ExecuteAsync(userId, cancellationToken);
                return outcome switch
                {
                    GetUserOutcome.Success success => Results.Ok(success.User),
                    GetUserOutcome.NotFound => UserNotFound(),
                    _ => throw new UnreachableOutcomeException(outcome),
                };
            })
            .RequirePermission(Permissions.UsersRead)
            .Produces<UserResponse>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .WithSummary("Returns one user by id.");

        group.MapPatch("/{userId:guid}", async (Guid userId, UpdateUserRequest request, ClaimsPrincipal principal, UpdateUserUseCase useCase, CancellationToken cancellationToken) =>
            {
                var outcome = await useCase.ExecuteAsync(userId, principal.GetUserId(), request, cancellationToken);
                return outcome switch
                {
                    UpdateUserOutcome.Success success => Results.Ok(success.User),
                    UpdateUserOutcome.NotFound => UserNotFound(),
                    UpdateUserOutcome.EmailAlreadyExists => EmailConflict(),
                    UpdateUserOutcome.CannotDisableSelf => Results.Problem(
                        statusCode: StatusCodes.Status409Conflict,
                        title: "Cannot disable own account",
                        detail: "Disabling your own account is not allowed because it would lock you out immediately."),
                    _ => throw new UnreachableOutcomeException(outcome),
                };
            })
            .RequirePermission(Permissions.UsersManage)
            .Produces<UserResponse>(StatusCodes.Status200OK)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .WithSummary("Updates a user's email and/or disabled state. Disabling revokes the user's refresh sessions.");

        group.MapPut("/{userId:guid}/profile", async (Guid userId, UpdateUserProfileRequest request, UpdateUserProfileUseCase useCase, CancellationToken cancellationToken) =>
            {
                var outcome = await useCase.ExecuteAsync(userId, request, cancellationToken);
                return outcome switch
                {
                    UpdateUserProfileOutcome.Success success => Results.Ok(success.User),
                    UpdateUserProfileOutcome.NotFound => UserNotFound(),
                    UpdateUserProfileOutcome.UsernameAlreadyExists => UsernameConflict(),
                    UpdateUserProfileOutcome.InvalidUsername => InvalidUsernameProblem(),
                    _ => throw new UnreachableOutcomeException(outcome),
                };
            })
            .RequirePermission(Permissions.UsersManage)
            .Produces<UserResponse>(StatusCodes.Status200OK)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .WithSummary("Replaces a user's profile details (username, names, suffix, job title). Optional fields are cleared when omitted.");

        group.MapPatch("/{userId:guid}/roles", async (Guid userId, SetUserRolesRequest request, SetUserRolesUseCase useCase, CancellationToken cancellationToken) =>
            {
                var outcome = await useCase.ExecuteAsync(userId, request, cancellationToken);
                return outcome switch
                {
                    SetUserRolesOutcome.Success success => Results.Ok(success.User),
                    SetUserRolesOutcome.NotFound => UserNotFound(),
                    SetUserRolesOutcome.UnknownRoles unknown => Results.Problem(
                        statusCode: StatusCodes.Status400BadRequest,
                        title: "Unknown roles",
                        detail: $"Role ids do not exist: {string.Join(", ", unknown.RoleIds)}."),
                    _ => throw new UnreachableOutcomeException(outcome),
                };
            })
            .RequirePermission(Permissions.UsersManage)
            .Produces<UserResponse>(StatusCodes.Status200OK)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .WithSummary("Replaces a user's role assignments. New claims take effect with the user's next access token.");

        group.MapPost("/{userId:guid}/password", async (Guid userId, ResetUserPasswordRequest request, ResetUserPasswordUseCase useCase, CancellationToken cancellationToken) =>
            {
                var outcome = await useCase.ExecuteAsync(userId, request, cancellationToken);
                return outcome switch
                {
                    ResetUserPasswordOutcome.Success => Results.NoContent(),
                    ResetUserPasswordOutcome.NotFound => UserNotFound(),
                    ResetUserPasswordOutcome.WeakPassword weak => PasswordProblem(weak.MinLength),
                    _ => throw new UnreachableOutcomeException(outcome),
                };
            })
            .RequirePermission(Permissions.UsersManage)
            .Produces(StatusCodes.Status204NoContent)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .WithSummary("Resets a user's password and revokes all of their refresh sessions.");

        return endpoints;
    }

    private static IResult UserNotFound() => Results.Problem(
        statusCode: StatusCodes.Status404NotFound,
        title: "User not found",
        detail: "No user exists with the specified id.");

    private static IResult EmailConflict() => Results.Problem(
        statusCode: StatusCodes.Status409Conflict,
        title: "Email already registered",
        detail: "Another user with this email already exists.");

    private static IResult UsernameConflict() => Results.Problem(
        statusCode: StatusCodes.Status409Conflict,
        title: "Username already taken",
        detail: "Another user with this username already exists.");

    private static IResult InvalidUsernameProblem() => Results.Problem(
        statusCode: StatusCodes.Status400BadRequest,
        title: "Invalid username",
        detail: $"Username must be {User.MinUsernameLength}-{User.MaxUsernameLength} characters and contain only ASCII letters, digits, '.', '_', or '-'.");

    private static IResult PasswordProblem(int minLength) => Results.Problem(
        statusCode: StatusCodes.Status400BadRequest,
        title: "Password too weak",
        detail: $"The password must be at least {minLength} characters long.");
}
