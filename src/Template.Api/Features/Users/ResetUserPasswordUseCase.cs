using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Template.Api.Features.Auth;
using Template.Api.Infrastructure.Observability;
using Template.Api.Infrastructure.Persistence;
using Template.Api.Infrastructure.Security;

namespace Template.Api.Features.Users;

public sealed record ResetUserPasswordRequest
{
    [Required]
    [StringLength(PasswordPolicyOptions.TransportMaxLength)]
    public required string NewPassword { get; init; }
}

public abstract record ResetUserPasswordOutcome
{
    public sealed record Success : ResetUserPasswordOutcome;

    public sealed record NotFound : ResetUserPasswordOutcome;

    public sealed record WeakPassword(int MinLength) : ResetUserPasswordOutcome;
}

/// <summary>
/// Administrator password reset. All of the user's refresh tokens are revoked so existing sessions
/// must re-authenticate with the new password.
/// </summary>
public sealed class ResetUserPasswordUseCase(
    AppDbContext dbContext,
    PasswordHasher passwordHasher,
    IOptions<PasswordPolicyOptions> passwordPolicy,
    TimeProvider timeProvider,
    ILogger<ResetUserPasswordUseCase> logger)
{
    public async Task<ResetUserPasswordOutcome> ExecuteAsync(Guid userId, ResetUserPasswordRequest request, CancellationToken cancellationToken)
    {
        if (request.NewPassword.Length < passwordPolicy.Value.MinLength)
        {
            return new ResetUserPasswordOutcome.WeakPassword(passwordPolicy.Value.MinLength);
        }

        var user = await dbContext.Users.FirstOrDefaultAsync(candidate => candidate.Id == userId, cancellationToken);
        if (user is null)
        {
            return new ResetUserPasswordOutcome.NotFound();
        }

        user.ChangePassword(passwordHasher.Hash(request.NewPassword));
        await dbContext.SaveChangesAsync(cancellationToken);

        var now = timeProvider.GetUtcNow();
        await RefreshTokenRevocation.RevokeAllForUserAsync(dbContext, user.Id, now, cancellationToken);
        ApiLog.SessionsRevoked(logger, user.Id);

        return new ResetUserPasswordOutcome.Success();
    }
}
