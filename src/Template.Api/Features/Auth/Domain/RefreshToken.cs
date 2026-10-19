using Template.Api.Features.Roles.Domain;
using Template.Api.Features.Users.Domain;

namespace Template.Api.Features.Auth.Domain;

/// <summary>
/// Persisted refresh token. Only the SHA-256 hash of the secret is stored. Supports single-use
/// rotation (a used token is revoked and points at its replacement) and revocation on logout,
/// disable, or password change.
/// </summary>
public sealed class RefreshToken
{
    public Guid Id { get; private set; }

    public Guid UserId { get; private set; }

    public string TokenHash { get; private set; } = null!;

    public DateTimeOffset IssuedAt { get; private set; }

    public DateTimeOffset ExpiresAt { get; private set; }

    public DateTimeOffset? RevokedAt { get; private set; }

    public Guid? ReplacedByTokenId { get; private set; }

    private RefreshToken()
    {
    }

    public static RefreshToken Issue(Guid userId, string tokenHash, DateTimeOffset now, TimeSpan lifetime) =>
        new()
        {
            Id = Guid.NewGuid(),
            UserId = userId,
            TokenHash = tokenHash,
            IssuedAt = now,
            ExpiresAt = now + lifetime,
        };

    public bool IsActive(DateTimeOffset now) => RevokedAt is null && ExpiresAt > now;

    public void Revoke(DateTimeOffset now) => RevokedAt ??= now;

    /// <summary>Marks this token as consumed by rotation and records its replacement.</summary>
    public void MarkReplacedBy(Guid newTokenId, DateTimeOffset now)
    {
        ReplacedByTokenId = newTokenId;
        RevokedAt ??= now;
    }
}
