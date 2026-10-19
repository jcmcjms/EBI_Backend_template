using Microsoft.EntityFrameworkCore;
using Template.Api.Features.Roles.Domain;
using Template.Api.Infrastructure.Persistence;
using PermissionCatalog = Template.Api.Infrastructure.Authorization.Permissions;

namespace Template.Api.Features.Roles;

public abstract record UpdateRoleOutcome
{
    public sealed record Success(RoleSummaryResponse Role) : UpdateRoleOutcome;

    public sealed record NotFound : UpdateRoleOutcome;

    public sealed record NameAlreadyExists : UpdateRoleOutcome;

    public sealed record UnknownPermissions(IReadOnlyList<string> Permissions) : UpdateRoleOutcome;

    public sealed record SystemRoleIsImmutable : UpdateRoleOutcome;
}

/// <summary>
/// Full replacement update of a custom role (name + permission set). System roles are immutable
/// through the API; their permissions follow the code catalog via seeding.
/// </summary>
public sealed class UpdateRoleUseCase(AppDbContext dbContext, TimeProvider timeProvider)
{
    public async Task<UpdateRoleOutcome> ExecuteAsync(Guid roleId, SaveRoleRequest request, CancellationToken cancellationToken)
    {
        var unknown = PermissionCatalog.FindUnknown(request.Permissions);
        if (unknown.Count > 0)
        {
            return new UpdateRoleOutcome.UnknownPermissions(unknown);
        }

        var role = await dbContext.Roles
            .Include(current => current.PermissionGrants)
            .FirstOrDefaultAsync(current => current.Id == roleId, cancellationToken);

        if (role is null)
        {
            return new UpdateRoleOutcome.NotFound();
        }

        if (role.IsSystem)
        {
            return new UpdateRoleOutcome.SystemRoleIsImmutable();
        }

        var now = timeProvider.GetUtcNow();
        role.Rename(request.Name);
        role.SetPermissions(request.Permissions, now);

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            dbContext.ChangeTracker.Clear();
            var name = Role.NormalizeName(request.Name);
            if (await dbContext.Roles.AnyAsync(candidate => candidate.Name == name && candidate.Id != roleId, cancellationToken))
            {
                return new UpdateRoleOutcome.NameAlreadyExists();
            }

            throw;
        }

        var assignedUsersCount = await dbContext.UserRoles.CountAsync(userRole => userRole.RoleId == roleId, cancellationToken);

        return new UpdateRoleOutcome.Success(new RoleSummaryResponse(
            role.Id,
            role.Name,
            role.IsSystem,
            assignedUsersCount,
            [.. role.PermissionGrants.Select(grant => grant.Permission).Order(StringComparer.Ordinal)]));
    }
}
