using Microsoft.EntityFrameworkCore;
using Template.Api.Infrastructure.Persistence;

namespace Template.Api.Features.Users;

/// <summary>Which unique constraint a failed user write collided with.</summary>
internal enum UserConflict
{
    Email,
    Username,
}

/// <summary>
/// After a unique-index violation (the race-safe source of truth), identifies which constrained
/// value actually collides so the use case can return the matching 409. Runs only on the rare
/// conflict path; both checks are single indexed queries.
/// </summary>
internal static class UserUniqueConflicts
{
    public static async Task<UserConflict?> FindAsync(
        AppDbContext dbContext,
        string email,
        string? username,
        Guid? excludedUserId,
        CancellationToken cancellationToken)
    {
        var emailTaken = await dbContext.Users.AnyAsync(
            user => user.Email == email && (excludedUserId == null || user.Id != excludedUserId),
            cancellationToken);
        if (emailTaken)
        {
            return UserConflict.Email;
        }

        if (username is not null)
        {
            var usernameTaken = await dbContext.Users.AnyAsync(
                user => user.Username == username && (excludedUserId == null || user.Id != excludedUserId),
                cancellationToken);
            if (usernameTaken)
            {
                return UserConflict.Username;
            }
        }

        return null;
    }
}
