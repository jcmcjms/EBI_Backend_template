using Microsoft.EntityFrameworkCore;
using Template.Api.Infrastructure.Persistence;

namespace Template.Api.Features.Auth;

/// <summary>
/// Set-based revocation of every active refresh token for one user. Shared by session rotation
/// (reuse detection) and the Users feature (disable, password reset, password change).
/// </summary>
public static class RefreshTokenRevocation
{
    public static Task<int> RevokeAllForUserAsync(
        AppDbContext dbContext,
        Guid userId,
        DateTimeOffset now,
        CancellationToken cancellationToken) =>
        dbContext.RefreshTokens
            .Where(token => token.UserId == userId && token.RevokedAt == null)
            .ExecuteUpdateAsync(setters => setters.SetProperty(token => token.RevokedAt, now), cancellationToken);
}
