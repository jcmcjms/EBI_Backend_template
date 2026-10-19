using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;
using Template.Api.Features.Users.Domain;
using Template.Api.Infrastructure.Persistence;

namespace Template.Api.Features.Users;

/// <summary>
/// Full-replacement profile update (same contract style as role updates): required fields must be
/// supplied, optional fields are cleared when omitted or null. Partial updates would need a
/// patch-document format to distinguish "unchanged" from "cleared".
/// </summary>
public sealed record UpdateUserProfileRequest
{
    [Required]
    [StringLength(User.MaxUsernameLength, MinimumLength = User.MinUsernameLength)]
    public required string Username { get; init; }

    [Required]
    [StringLength(User.MaxNameLength)]
    public required string FirstName { get; init; }

    [StringLength(User.MaxNameLength)]
    public string? MiddleName { get; init; }

    [Required]
    [StringLength(User.MaxNameLength)]
    public required string LastName { get; init; }

    [StringLength(User.MaxSuffixLength)]
    public string? Suffix { get; init; }

    [StringLength(User.MaxJobTitleLength)]
    public string? JobTitle { get; init; }
}

public abstract record UpdateUserProfileOutcome
{
    public sealed record Success(UserResponse User) : UpdateUserProfileOutcome;

    public sealed record NotFound : UpdateUserProfileOutcome;

    public sealed record UsernameAlreadyExists : UpdateUserProfileOutcome;

    public sealed record InvalidUsername : UpdateUserProfileOutcome;
}

/// <summary>
/// Administration profile update. Username uniqueness is enforced race-safely by the unique
/// filtered index; the new username takes effect for login immediately, and appears in the
/// user's next issued access token.
/// </summary>
public sealed class UpdateUserProfileUseCase(AppDbContext dbContext)
{
    public async Task<UpdateUserProfileOutcome> ExecuteAsync(Guid userId, UpdateUserProfileRequest request, CancellationToken cancellationToken)
    {
        if (!User.IsValidUsername(request.Username.Trim().ToLowerInvariant()))
        {
            return new UpdateUserProfileOutcome.InvalidUsername();
        }

        var user = await dbContext.Users
            .Include(current => current.UserRoles)
                .ThenInclude(userRole => userRole.Role)
            .FirstOrDefaultAsync(current => current.Id == userId, cancellationToken);

        if (user is null)
        {
            return new UpdateUserProfileOutcome.NotFound();
        }

        user.ChangeProfile(
            request.Username,
            request.FirstName,
            request.LastName,
            request.MiddleName,
            request.Suffix,
            request.JobTitle);

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            dbContext.ChangeTracker.Clear();
            var conflict = await UserUniqueConflicts.FindAsync(dbContext, user.Email, user.Username, excludedUserId: userId, cancellationToken);
            if (conflict == UserConflict.Username)
            {
                return new UpdateUserProfileOutcome.UsernameAlreadyExists();
            }

            throw;
        }

        return new UpdateUserProfileOutcome.Success(UserResponse.FromEntity(user));
    }
}
