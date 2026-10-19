using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Routing;
using Template.Api.Infrastructure.Authorization;
using Template.Api.Infrastructure.Http;
using Template.Api.Infrastructure.Paging;

namespace Template.Api.Features.Roles;

/// <summary>Role administration endpoints and the read-only permission catalog.</summary>
public static class RolesEndpoints
{
    public static IEndpointRouteBuilder MapRolesEndpoints(this IEndpointRouteBuilder endpoints)
    {
        // The catalog is not a secret — every authenticated principal already carries its own
        // permissions as claims — so authentication without a specific permission is sufficient.
        endpoints.MapGet("/api/permissions", () => Results.Ok(Permissions.All))
            .WithTags("Roles")
            .RequireAuthorization()
            .Produces<IReadOnlyList<string>>(StatusCodes.Status200OK)
            .ProducesProblem(StatusCodes.Status401Unauthorized)
            .WithSummary("Returns the code-defined permission catalog assignable to roles.");

        var group = endpoints.MapGroup("/api/roles")
            .WithTags("Roles")
            .RequireAuthorization();

        group.MapGet("", async ([AsParameters] PagedRequest paging, ListRolesUseCase useCase, CancellationToken cancellationToken) =>
                Results.Ok(await useCase.ExecuteAsync(paging, cancellationToken)))
            .RequirePermission(Permissions.RolesRead)
            .Produces<PagedResponse<RoleSummaryResponse>>(StatusCodes.Status200OK)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .WithSummary("Lists roles with their permissions and assignment counts (paged).");

        group.MapPost("", async (SaveRoleRequest request, CreateRoleUseCase useCase, CancellationToken cancellationToken) =>
            {
                var outcome = await useCase.ExecuteAsync(request, cancellationToken);
                return outcome switch
                {
                    CreateRoleOutcome.Success success => Results.Created($"/api/roles/{success.Role.Id}", success.Role),
                    CreateRoleOutcome.NameAlreadyExists => RoleNameConflict(),
                    CreateRoleOutcome.UnknownPermissions unknownPermissions => UnknownPermissionsProblem(unknownPermissions.Permissions),
                    _ => throw new UnreachableOutcomeException(outcome),
                };
            })
            .RequirePermission(Permissions.RolesManage)
            .Produces<RoleSummaryResponse>(StatusCodes.Status201Created)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .WithSummary("Creates a custom role with a catalog-validated permission set.");

        group.MapPut("/{roleId:guid}", async (Guid roleId, SaveRoleRequest request, UpdateRoleUseCase useCase, CancellationToken cancellationToken) =>
            {
                var outcome = await useCase.ExecuteAsync(roleId, request, cancellationToken);
                return outcome switch
                {
                    UpdateRoleOutcome.Success success => Results.Ok(success.Role),
                    UpdateRoleOutcome.NotFound => RoleNotFound(),
                    UpdateRoleOutcome.NameAlreadyExists => RoleNameConflict(),
                    UpdateRoleOutcome.UnknownPermissions unknownPermissions => UnknownPermissionsProblem(unknownPermissions.Permissions),
                    UpdateRoleOutcome.SystemRoleIsImmutable => SystemRoleProblem(),
                    _ => throw new UnreachableOutcomeException(outcome),
                };
            })
            .RequirePermission(Permissions.RolesManage)
            .Produces<RoleSummaryResponse>(StatusCodes.Status200OK)
            .ProducesValidationProblem()
            .ProducesProblem(StatusCodes.Status400BadRequest)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .WithSummary("Replaces a custom role's name and permission set. System roles are immutable.");

        group.MapDelete("/{roleId:guid}", async (Guid roleId, DeleteRoleUseCase useCase, CancellationToken cancellationToken) =>
            {
                var outcome = await useCase.ExecuteAsync(roleId, cancellationToken);
                return outcome switch
                {
                    DeleteRoleOutcome.Success => Results.NoContent(),
                    DeleteRoleOutcome.NotFound => RoleNotFound(),
                    DeleteRoleOutcome.SystemRoleIsImmutable => SystemRoleProblem(),
                    DeleteRoleOutcome.RoleInUse inUse => Results.Problem(
                        statusCode: StatusCodes.Status409Conflict,
                        title: "Role is in use",
                        detail: $"The role is still assigned to {inUse.AssignedUsersCount} user(s). Remove those assignments first."),
                    _ => throw new UnreachableOutcomeException(outcome),
                };
            })
            .RequirePermission(Permissions.RolesManage)
            .Produces(StatusCodes.Status204NoContent)
            .ProducesProblem(StatusCodes.Status403Forbidden)
            .ProducesProblem(StatusCodes.Status404NotFound)
            .ProducesProblem(StatusCodes.Status409Conflict)
            .WithSummary("Deletes an unassigned custom role.");

        return endpoints;
    }

    private static IResult RoleNotFound() => Results.Problem(
        statusCode: StatusCodes.Status404NotFound,
        title: "Role not found",
        detail: "No role exists with the specified id.");

    private static IResult RoleNameConflict() => Results.Problem(
        statusCode: StatusCodes.Status409Conflict,
        title: "Role name already exists",
        detail: "Another role with this name already exists.");

    private static IResult SystemRoleProblem() => Results.Problem(
        statusCode: StatusCodes.Status409Conflict,
        title: "System role cannot be modified",
        detail: "System roles are managed by the application. Create a custom role instead.");

    private static IResult UnknownPermissionsProblem(IReadOnlyList<string> permissions) => Results.Problem(
        statusCode: StatusCodes.Status400BadRequest,
        title: "Unknown permissions",
        detail: $"Permissions are not part of the catalog: {string.Join(", ", permissions)}.");
}
