using Microsoft.EntityFrameworkCore;
using Template.Api.Infrastructure.Persistence;

namespace Template.Api.Features.Roles;

public abstract record DeleteRoleOutcome
{
    public sealed record Success : DeleteRoleOutcome;

    public sealed record NotFound : DeleteRoleOutcome;

    public sealed record SystemRoleIsImmutable : DeleteRoleOutcome;

    public sealed record RoleInUse(int AssignedUsersCount) : DeleteRoleOutcome;
}

/// <summary>
/// Deletes a custom role. System roles cannot be deleted, and roles still assigned to users must be
/// unassigned first — deletion must never silently strip users of access. The database enforces the
/// same guard via a restrict-delete foreign key.
/// </summary>
public sealed class DeleteRoleUseCase(AppDbContext dbContext)
{
    public async Task<DeleteRoleOutcome> ExecuteAsync(Guid roleId, CancellationToken cancellationToken)
    {
        var role = await dbContext.Roles.FirstOrDefaultAsync(candidate => candidate.Id == roleId, cancellationToken);
        if (role is null)
        {
            return new DeleteRoleOutcome.NotFound();
        }

        if (role.IsSystem)
        {
            return new DeleteRoleOutcome.SystemRoleIsImmutable();
        }

        var assignedUsersCount = await dbContext.UserRoles.CountAsync(userRole => userRole.RoleId == roleId, cancellationToken);
        if (assignedUsersCount > 0)
        {
            return new DeleteRoleOutcome.RoleInUse(assignedUsersCount);
        }

        dbContext.Roles.Remove(role);
        await dbContext.SaveChangesAsync(cancellationToken);

        return new DeleteRoleOutcome.Success();
    }
}
