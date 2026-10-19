using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Template.Api.Features.Auth.Domain;
using Template.Api.Features.Users.Domain;
using Template.Api.Infrastructure.Observability;
using Template.Api.Infrastructure.Persistence;
using Template.Api.Infrastructure.Security;

namespace Template.Api.Features.Auth;

public sealed record RefreshSessionRequest
{
    [Required]
    [StringLength(128, MinimumLength = 20)]
    public required string RefreshToken { get; init; }
}

public abstract record RefreshSessionOutcome
{
    public sealed record Success(AuthTokenResponse Tokens) : RefreshSessionOutcome;

    /// <summary>Single generic failure for unknown, expired, revoked, or reused tokens.</summary>
    public sealed record InvalidToken : RefreshSessionOutcome;
}

/// <summary>
/// Rotates refresh tokens: each presented token is exchanged for a new pair and the old token is
/// revoked with a pointer to its replacement. Presenting an already-revoked token is treated as
/// theft (reuse detection) and revokes every active session of that user.
/// </summary>
public sealed class RefreshSessionUseCase(
    AppDbContext dbContext,
    JwtAccessTokenIssuer tokenIssuer,
    IOptions<AuthOptions> authOptions,
    TimeProvider timeProvider,
    ILogger<RefreshSessionUseCase> logger)
{
    public async Task<RefreshSessionOutcome> ExecuteAsync(RefreshSessionRequest request, CancellationToken cancellationToken)
    {
        var tokenHash = RefreshTokenGenerator.ComputeHash(request.RefreshToken);
        var now = timeProvider.GetUtcNow();

        var storedToken = await dbContext.RefreshTokens
            .FirstOrDefaultAsync(token => token.TokenHash == tokenHash, cancellationToken);

        if (storedToken is null)
        {
            return new RefreshSessionOutcome.InvalidToken();
        }

        if (storedToken.RevokedAt is not null)
        {
            ApiLog.RefreshTokenReuseDetected(logger, storedToken.UserId);
            await RefreshTokenRevocation.RevokeAllForUserAsync(dbContext, storedToken.UserId, now, cancellationToken);
            return new RefreshSessionOutcome.InvalidToken();
        }

        if (storedToken.ExpiresAt <= now)
        {
            return new RefreshSessionOutcome.InvalidToken();
        }

        var user = await dbContext.Users
            .Include(current => current.UserRoles)
                .ThenInclude(userRole => userRole.Role)
                    .ThenInclude(role => role.PermissionGrants)
            .FirstOrDefaultAsync(current => current.Id == storedToken.UserId, cancellationToken);

        if (user is null || user.IsDisabled)
        {
            await RefreshTokenRevocation.RevokeAllForUserAsync(dbContext, storedToken.UserId, now, cancellationToken);
            return new RefreshSessionOutcome.InvalidToken();
        }

        var roles = user.UserRoles
            .Select(userRole => userRole.Role.Name)
            .Distinct(StringComparer.Ordinal)
            .ToList();
        var permissions = user.UserRoles
            .SelectMany(userRole => userRole.Role.PermissionGrants)
            .Select(grant => grant.Permission)
            .Distinct(StringComparer.Ordinal)
            .ToList();

        var accessToken = tokenIssuer.Issue(user, roles, permissions);

        var refreshToken = RefreshTokenGenerator.Generate();
        var newStoredToken = RefreshToken.Issue(
            user.Id,
            RefreshTokenGenerator.ComputeHash(refreshToken),
            now,
            authOptions.Value.RefreshTokenLifetime);

        storedToken.MarkReplacedBy(newStoredToken.Id, now);
        dbContext.RefreshTokens.Add(newStoredToken);

        await dbContext.SaveChangesAsync(cancellationToken);

        return new RefreshSessionOutcome.Success(
            AuthTokenResponse.Create(accessToken.Token, refreshToken, authOptions.Value.AccessTokenLifetime));
    }
}
