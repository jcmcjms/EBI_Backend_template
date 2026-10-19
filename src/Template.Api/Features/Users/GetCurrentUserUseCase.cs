using System.Security.Claims;
using Microsoft.EntityFrameworkCore;
using Template.Api.Infrastructure.Persistence;
using Template.Api.Infrastructure.Security;

namespace Template.Api.Features.Users;

public sealed record CurrentUserResponse(
    Guid Id,
    string Email,
    string? Username,
    string FirstName,
    string? MiddleName,
    string LastName,
    string? Suffix,
    string? JobTitle,
    IReadOnlyList<string> Roles,
    IReadOnlyList<string> Permissions);

public abstract record GetCurrentUserOutcome
{
    public sealed record Success(CurrentUserResponse User) : GetCurrentUserOutcome;

    /// <summary>The token is valid but the account row no longer exists (contract: the database is authoritative).</summary>
    public sealed record NotFound : GetCurrentUserOutcome;
}

/// <summary>
/// Returns the authenticated principal's own profile. Identity and authorization data (roles,
/// permissions) come from validated token claims; profile fields come from a single primary-key
/// lookup so names/title are never stale and never bloat every access token.
/// </summary>
public sealed class GetCurrentUserUseCase(AppDbContext dbContext)
{
    public async Task<GetCurrentUserOutcome> ExecuteAsync(ClaimsPrincipal principal, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(principal);

        var userId = principal.GetUserId();

        var profile = await dbContext.Users
            .AsNoTracking()
            .Where(user => user.Id == userId)
            .Select(user => new
            {
                user.Id,
                user.Email,
                user.Username,
                user.FirstName,
                user.MiddleName,
                user.LastName,
                user.Suffix,
                user.JobTitle,
            })
            .FirstOrDefaultAsync(cancellationToken);

        if (profile is null)
        {
            return new GetCurrentUserOutcome.NotFound();
        }

        return new GetCurrentUserOutcome.Success(new CurrentUserResponse(
            profile.Id,
            profile.Email,
            profile.Username,
            profile.FirstName,
            profile.MiddleName,
            profile.LastName,
            profile.Suffix,
            profile.JobTitle,
            [.. principal.FindAll(AuthClaims.Role).Select(claim => claim.Value)],
            [.. principal.FindAll(AuthClaims.Permission).Select(claim => claim.Value)]));
    }
}
