using Microsoft.EntityFrameworkCore;
using Template.Api.Features.Users.Domain;
using Template.Api.Infrastructure.Persistence;

namespace Template.Api.Features.Users;

public sealed record RoleAssignmentResponse(Guid Id, string Name);

/// <summary>
/// Administration read model for a user. One contract serves list items, detail, and write
/// responses: every field belongs to the same boundary (admin user management) and is produced
/// by a single projection — a separate summary/detail split would duplicate identical semantics.
/// </summary>
public sealed record UserResponse(
    Guid Id,
    string Email,
    string? Username,
    string FirstName,
    string? MiddleName,
    string LastName,
    string? Suffix,
    string? JobTitle,
    bool IsDisabled,
    DateTimeOffset CreatedAt,
    DateTimeOffset? DisabledAt,
    IReadOnlyList<RoleAssignmentResponse> Roles)
{
    /// <summary>Maps a tracked user whose <c>UserRoles.Role</c> navigations are loaded.</summary>
    public static UserResponse FromEntity(User user)
    {
        ArgumentNullException.ThrowIfNull(user);
        return new UserResponse(
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
            [.. user.UserRoles
                .Select(userRole => new RoleAssignmentResponse(userRole.RoleId, userRole.Role.Name))
                .OrderBy(role => role.Name, StringComparer.Ordinal)]);
    }
}

public abstract record GetUserOutcome
{
    public sealed record Success(UserResponse User) : GetUserOutcome;

    public sealed record NotFound : GetUserOutcome;
}

public sealed class GetUserUseCase(AppDbContext dbContext)
{
    public async Task<GetUserOutcome> ExecuteAsync(Guid userId, CancellationToken cancellationToken)
    {
        var user = await dbContext.Users
            .AsNoTracking()
            .Where(candidate => candidate.Id == userId)
            .Select(candidate => new UserResponse(
                candidate.Id,
                candidate.Email,
                candidate.Username,
                candidate.FirstName,
                candidate.MiddleName,
                candidate.LastName,
                candidate.Suffix,
                candidate.JobTitle,
                candidate.IsDisabled,
                candidate.CreatedAt,
                candidate.DisabledAt,
                candidate.UserRoles
                    .Select(userRole => new RoleAssignmentResponse(userRole.Role.Id, userRole.Role.Name))
                    .OrderBy(role => role.Name)
                    .ToList()))
            .FirstOrDefaultAsync(cancellationToken);

        return user is null
            ? new GetUserOutcome.NotFound()
            : new GetUserOutcome.Success(user);
    }
}
