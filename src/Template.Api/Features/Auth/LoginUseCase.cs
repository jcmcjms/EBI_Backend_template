using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Template.Api.Features.Auth.Domain;
using Template.Api.Features.Users.Domain;
using Template.Api.Infrastructure.Observability;
using Template.Api.Infrastructure.Persistence;
using Template.Api.Infrastructure.Security;

namespace Template.Api.Features.Auth;

public sealed record LoginRequest
{
    /// <summary>
    /// Email address or username. Identifiers containing '@' are matched against emails only;
    /// all others against usernames only (usernames can never contain '@'), so a value can never
    /// ambiguously match two different accounts.
    /// </summary>
    [Required]
    [StringLength(User.MaxEmailLength)]
    public required string Identifier { get; init; }

    /// <summary>Transport bound only (DoS protection); the actual password policy is enforced by use cases.</summary>
    [Required]
    [StringLength(PasswordPolicyOptions.TransportMaxLength, MinimumLength = 1)]
    public required string Password { get; init; }
}

/// <summary>Token pair returned by login and refresh.</summary>
public sealed record AuthTokenResponse(string AccessToken, string RefreshToken, int ExpiresInSeconds, string TokenType)
{
    public static AuthTokenResponse Create(string accessToken, string refreshToken, TimeSpan accessTokenLifetime) =>
        new(accessToken, refreshToken, (int)accessTokenLifetime.TotalSeconds, "Bearer");
}

public abstract record LoginOutcome
{
    public sealed record Success(AuthTokenResponse Tokens) : LoginOutcome;

    /// <summary>
    /// Single generic failure for unknown identifier, wrong password, and disabled account so
    /// responses never reveal which condition occurred. The precise reason is logged server-side only.
    /// </summary>
    public sealed record Rejected : LoginOutcome;
}

/// <summary>
/// Verifies credentials (email or username), issues a JWT access token plus a rotating refresh
/// token, and transparently rehashes passwords stored with outdated PBKDF2 parameters.
/// </summary>
public sealed class LoginUseCase(
    AppDbContext dbContext,
    PasswordHasher passwordHasher,
    JwtAccessTokenIssuer tokenIssuer,
    IOptions<AuthOptions> authOptions,
    TimeProvider timeProvider,
    ILogger<LoginUseCase> logger)
{
    public async Task<LoginOutcome> ExecuteAsync(LoginRequest request, CancellationToken cancellationToken)
    {
        var identifier = request.Identifier.Trim();
        var normalized = identifier.ToLowerInvariant();
        var isEmail = identifier.Contains('@', StringComparison.Ordinal);

        var user = await (isEmail
            ? UsersWithRbac().FirstOrDefaultAsync(current => current.Email == normalized, cancellationToken)
            : UsersWithRbac().FirstOrDefaultAsync(current => current.Username == normalized, cancellationToken));

        if (user is null)
        {
            ApiLog.LoginFailed(logger, normalized, isEmail ? "unknown email" : "unknown username");
            return new LoginOutcome.Rejected();
        }

        if (user.IsDisabled)
        {
            ApiLog.LoginFailed(logger, normalized, "disabled user");
            return new LoginOutcome.Rejected();
        }

        if (!passwordHasher.Verify(request.Password, user.PasswordHash))
        {
            ApiLog.LoginFailed(logger, normalized, "invalid password");
            return new LoginOutcome.Rejected();
        }

        if (passwordHasher.NeedsRehash(user.PasswordHash))
        {
            user.ChangePassword(passwordHasher.Hash(request.Password));
        }

        var now = timeProvider.GetUtcNow();
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
        dbContext.RefreshTokens.Add(RefreshToken.Issue(
            user.Id,
            RefreshTokenGenerator.ComputeHash(refreshToken),
            now,
            authOptions.Value.RefreshTokenLifetime));

        await dbContext.SaveChangesAsync(cancellationToken);

        return new LoginOutcome.Success(
            AuthTokenResponse.Create(accessToken.Token, refreshToken, authOptions.Value.AccessTokenLifetime));
    }

    private IQueryable<User> UsersWithRbac() =>
        dbContext.Users
            .Include(current => current.UserRoles)
                .ThenInclude(userRole => userRole.Role)
                    .ThenInclude(role => role.PermissionGrants);
}
