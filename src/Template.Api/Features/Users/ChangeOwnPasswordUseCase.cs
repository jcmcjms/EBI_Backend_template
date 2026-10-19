using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Template.Api.Features.Auth;
using Template.Api.Infrastructure.Observability;
using Template.Api.Infrastructure.Persistence;
using Template.Api.Infrastructure.Security;

namespace Template.Api.Features.Users;

public sealed record ChangeOwnPasswordRequest
{
    [Required]
    [StringLength(PasswordPolicyOptions.TransportMaxLength)]
    public required string CurrentPassword { get; init; }

    [Required]
    [StringLength(PasswordPolicyOptions.TransportMaxLength)]
    public required string NewPassword { get; init; }
}

public abstract record ChangeOwnPasswordOutcome
{
    public sealed record Success : ChangeOwnPasswordOutcome;

    public sealed record InvalidCurrentPassword : ChangeOwnPasswordOutcome;

    public sealed record WeakPassword(int MinLength) : ChangeOwnPasswordOutcome;
}

/// <summary>
/// Self-service password change. The current password must verify, and all refresh tokens are
/// revoked afterwards so every session re-authenticates with the new password.
/// </summary>
public sealed class ChangeOwnPasswordUseCase(
    AppDbContext dbContext,
    PasswordHasher passwordHasher,
    IOptions<PasswordPolicyOptions> passwordPolicy,
    TimeProvider timeProvider,
    ILogger<ChangeOwnPasswordUseCase> logger)
{
    public async Task<ChangeOwnPasswordOutcome> ExecuteAsync(Guid userId, ChangeOwnPasswordRequest request, CancellationToken cancellationToken)
    {
        if (request.NewPassword.Length < passwordPolicy.Value.MinLength)
        {
            return new ChangeOwnPasswordOutcome.WeakPassword(passwordPolicy.Value.MinLength);
        }

        var user = await dbContext.Users.FirstOrDefaultAsync(candidate => candidate.Id == userId, cancellationToken);
        if (user is null || !passwordHasher.Verify(request.CurrentPassword, user.PasswordHash))
        {
            // Same outcome for unknown user and wrong password: the endpoint is authenticated, but
            // a mismatched "sub" should not produce a distinguishable response.
            return new ChangeOwnPasswordOutcome.InvalidCurrentPassword();
        }

        user.ChangePassword(passwordHasher.Hash(request.NewPassword));
        await dbContext.SaveChangesAsync(cancellationToken);

        var now = timeProvider.GetUtcNow();
        await RefreshTokenRevocation.RevokeAllForUserAsync(dbContext, userId, now, cancellationToken);
        ApiLog.SessionsRevoked(logger, userId);

        return new ChangeOwnPasswordOutcome.Success();
    }
}
