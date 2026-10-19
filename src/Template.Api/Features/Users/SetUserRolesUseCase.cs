using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;
using Template.Api.Features.Users.Domain;
using Template.Api.Infrastructure.Persistence;

namespace Template.Api.Features.Users;

public sealed record SetUserRolesRequest
{
    /// <summary>Full replacement set of role ids. An empty list removes all role assignments.</summary>
    public required IReadOnlyList<Guid> RoleIds { get; init; }
}

public abstract record SetUserRolesOutcome
{
    public sealed record Success(UserResponse User) : SetUserRolesOutcome;

    public sealed record NotFound : SetUserRolesOutcome;

    public sealed record UnknownRoles(IReadOnlyList<Guid> RoleIds) : SetUserRolesOutcome;
}

/// <summary>
/// Replaces a user's role assignments. Authorization changes take effect in the user's next issued
/// access token; already-issued tokens keep their claims until they expire (bounded by the short
/// access-token lifetime).
/// </summary>
public sealed class SetUserRolesUseCase(AppDbContext dbContext, TimeProvider timeProvider)
{
    public async Task<SetUserRolesOutcome> ExecuteAsync(Guid userId, SetUserRolesRequest request, CancellationToken cancellationToken)
    {
        var roleIds = request.RoleIds.Distinct().ToList();

        List<Guid> knownRoleIds = roleIds.Count == 0
            ? []
            : await dbContext.Roles
                .AsNoTracking()
                .Where(role => roleIds.Contains(role.Id))
                .Select(role => role.Id)
                .ToListAsync(cancellationToken);

        var unknownRoleIds = roleIds.Except(knownRoleIds).ToList();
        if (unknownRoleIds.Count > 0)
        {
            return new SetUserRolesOutcome.UnknownRoles(unknownRoleIds);
        }

        var user = await dbContext.Users
            .Include(current => current.UserRoles)
                .ThenInclude(userRole => userRole.Role)
            .FirstOrDefaultAsync(current => current.Id == userId, cancellationToken);

        if (user is null)
        {
            return new SetUserRolesOutcome.NotFound();
        }

        var now = timeProvider.GetUtcNow();
        var assigned = user.UserRoles.Select(userRole => userRole.RoleId).ToHashSet();

        foreach (var roleId in roleIds.Where(roleId => !assigned.Contains(roleId)))
        {
            dbContext.UserRoles.Add(UserRole.Assign(user.Id, roleId, now));
        }

        var removed = user.UserRoles.Where(userRole => !roleIds.Contains(userRole.RoleId)).ToList();
        if (removed.Count > 0)
        {
            dbContext.UserRoles.RemoveRange(removed);
        }

        await dbContext.SaveChangesAsync(cancellationToken);

        // Reload the assignment set so the response reflects the persisted state, including new role names.
        var roles = await dbContext.UserRoles
            .AsNoTracking()
            .Where(userRole => userRole.UserId == user.Id)
            .Select(userRole => new RoleAssignmentResponse(userRole.RoleId, userRole.Role.Name))
            .OrderBy(role => role.Name)
            .ToListAsync(cancellationToken);

        return new SetUserRolesOutcome.Success(new UserResponse(
            user.Id,
            user.Email,
            user.Username,
            user.FirstName,
            user.MiddleName,
            user.LastName,
            user.Suffix,
            user.JobTitle,
            user.IsDisabled,
            user.CreatedAt,
            user.DisabledAt,
            roles));
    }
}
