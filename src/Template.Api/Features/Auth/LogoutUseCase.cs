using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;
using Template.Api.Infrastructure.Persistence;
using Template.Api.Infrastructure.Security;

namespace Template.Api.Features.Auth;

public sealed record LogoutRequest
{
    [Required]
    [StringLength(128, MinimumLength = 20)]
    public required string RefreshToken { get; init; }
}

/// <summary>
/// Revokes the presented refresh token. Unknown or already-revoked tokens are ignored, keeping
/// logout idempotent and preventing logout from probing token validity.
/// </summary>
public sealed class LogoutUseCase(AppDbContext dbContext, TimeProvider timeProvider)
{
    public async Task ExecuteAsync(LogoutRequest request, CancellationToken cancellationToken)
    {
        var tokenHash = RefreshTokenGenerator.ComputeHash(request.RefreshToken);
        var now = timeProvider.GetUtcNow();

        await dbContext.RefreshTokens
            .Where(token => token.TokenHash == tokenHash && token.RevokedAt == null)
            .ExecuteUpdateAsync(setters => setters.SetProperty(token => token.RevokedAt, now), cancellationToken);
    }
}
