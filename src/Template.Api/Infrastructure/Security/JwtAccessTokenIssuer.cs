using System.Security.Claims;
using System.Text;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using Template.Api.Features.Users.Domain;

namespace Template.Api.Infrastructure.Security;

/// <summary>Claim types used in issued access tokens. Kept as short JWT-style names (inbound claim mapping is disabled).</summary>
public static class AuthClaims
{
    public const string Subject = "sub";
    public const string Email = "email";
    public const string Username = "username";
    public const string Role = "role";
    public const string Permission = "perm";
}

/// <summary>
/// Issues signed JWT access tokens (HS256) carrying the user id, email, role names, and permission claims.
/// Access tokens are intentionally short-lived; role/permission changes take effect on the next issued token.
/// </summary>
public sealed class JwtAccessTokenIssuer
{
    private readonly AuthOptions _options;
    private readonly TimeProvider _timeProvider;
    private readonly JsonWebTokenHandler _tokenHandler = new();

    public JwtAccessTokenIssuer(IOptions<AuthOptions> options, TimeProvider timeProvider)
    {
        ArgumentNullException.ThrowIfNull(options);
        ArgumentNullException.ThrowIfNull(timeProvider);
        _options = options.Value;
        _timeProvider = timeProvider;
    }

    public static SymmetricSecurityKey CreateSigningKey(AuthOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);
        return new SymmetricSecurityKey(Encoding.UTF8.GetBytes(options.SigningKey));
    }

    public IssuedAccessToken Issue(User user, IReadOnlyCollection<string> roles, IReadOnlyCollection<string> permissions)
    {
        ArgumentNullException.ThrowIfNull(user);
        ArgumentNullException.ThrowIfNull(roles);
        ArgumentNullException.ThrowIfNull(permissions);

        // JWT time claims ("iat", "nbf", "exp") carry whole-second precision. Truncate so the
        // returned ExpiresAt matches the token's "exp" exactly and validation is unambiguous.
        var now = TruncateToSeconds(_timeProvider.GetUtcNow());
        var expires = TruncateToSeconds(now.Add(_options.AccessTokenLifetime));

        var identity = new ClaimsIdentity();
        identity.AddClaim(new Claim(AuthClaims.Subject, user.Id.ToString("D")));
        identity.AddClaim(new Claim(AuthClaims.Email, user.Email));
        if (user.Username is not null)
        {
            identity.AddClaim(new Claim(AuthClaims.Username, user.Username));
        }

        foreach (var role in roles)
        {
            identity.AddClaim(new Claim(AuthClaims.Role, role));
        }

        foreach (var permission in permissions)
        {
            identity.AddClaim(new Claim(AuthClaims.Permission, permission));
        }

        var token = _tokenHandler.CreateToken(new SecurityTokenDescriptor
        {
            Subject = identity,
            Issuer = _options.Issuer,
            Audience = _options.Audience,
            IssuedAt = now.UtcDateTime,
            NotBefore = now.UtcDateTime,
            Expires = expires.UtcDateTime,
            SigningCredentials = new SigningCredentials(CreateSigningKey(_options), SecurityAlgorithms.HmacSha256),
        });

        return new IssuedAccessToken(token, expires);
    }

    private static DateTimeOffset TruncateToSeconds(DateTimeOffset value) =>
        value.AddTicks(-(value.UtcTicks % TimeSpan.TicksPerSecond));

    public sealed record IssuedAccessToken(string Token, DateTimeOffset ExpiresAt);
}
