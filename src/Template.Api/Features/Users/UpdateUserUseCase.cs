using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;
using Template.Api.Features.Auth;
using Template.Api.Features.Users.Domain;
using Template.Api.Infrastructure.Observability;
using Template.Api.Infrastructure.Persistence;

namespace Template.Api.Features.Users;

public sealed record UpdateUserRequest
{
    [EmailAddress]
    [StringLength(User.MaxEmailLength)]
    public string? Email { get; init; }

    public bool? IsDisabled { get; init; }
}

public abstract record UpdateUserOutcome
{
    public sealed record Success(UserResponse User) : UpdateUserOutcome;

    public sealed record NotFound : UpdateUserOutcome;

    public sealed record EmailAlreadyExists : UpdateUserOutcome;

    public sealed record CannotDisableSelf : UpdateUserOutcome;
}

/// <summary>
/// Updates profile state. Disabling a user also revokes all of their refresh tokens, so an active
/// session cannot outlive the disable beyond the current short-lived access token.
/// </summary>
public sealed class UpdateUserUseCase(
    AppDbContext dbContext,
    TimeProvider timeProvider,
    ILogger<UpdateUserUseCase> logger)
{
    public async Task<UpdateUserOutcome> ExecuteAsync(
        Guid userId,
        Guid currentUserId,
        UpdateUserRequest request,
        CancellationToken cancellationToken)
    {
        if (request.IsDisabled == true && userId == currentUserId)
        {
            return new UpdateUserOutcome.CannotDisableSelf();
        }

        var user = await dbContext.Users
            .Include(current => current.UserRoles)
                .ThenInclude(userRole => userRole.Role)
            .FirstOrDefaultAsync(current => current.Id == userId, cancellationToken);

        if (user is null)
        {
            return new UpdateUserOutcome.NotFound();
        }

        var now = timeProvider.GetUtcNow();

        if (request.Email is not null)
        {
            user.RenameEmail(request.Email);
        }

        var wasDisabled = user.IsDisabled;
        if (request.IsDisabled is true)
        {
            user.Disable(now);
        }
        else if (request.IsDisabled is false)
        {
            user.Enable();
        }

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            dbContext.ChangeTracker.Clear();
            if (request.Email is not null)
            {
                var email = User.NormalizeEmail(request.Email);
                var conflict = await UserUniqueConflicts.FindAsync(dbContext, email, username: null, excludedUserId: userId, cancellationToken);
                if (conflict == UserConflict.Email)
                {
                    return new UpdateUserOutcome.EmailAlreadyExists();
                }
            }

            throw;
        }

        if (!wasDisabled && user.IsDisabled)
        {
            await RefreshTokenRevocation.RevokeAllForUserAsync(dbContext, user.Id, now, cancellationToken);
            ApiLog.SessionsRevoked(logger, user.Id);
        }

        return new UpdateUserOutcome.Success(UserResponse.FromEntity(user));
    }
}
