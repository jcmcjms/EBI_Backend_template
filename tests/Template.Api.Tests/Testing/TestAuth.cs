using System.Security.Claims;
using System.Text;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using Template.Api.Infrastructure.Security;

namespace Template.Api.Tests.Testing;

/// <summary>
/// Test authentication material. The signing key below exists only for tests; production
/// configuration is validated to reject the shipped development key.
/// </summary>
public static class TestAuth
{
    public const string Issuer = "Template.Api.Tests";
    public const string Audience = "Template.Api.Tests.Clients";
    public const string SigningKey = "test-only-signing-key-0123456789-abcdefghijklmnopqrstuvwxyz";

    /// <summary>
    /// Forges an access token with the exact claim shape the production issuer uses, so pipeline
    /// tests exercise real token validation and authorization without a database.
    /// </summary>
    public static string CreateAccessToken(
        Guid userId,
        string email,
        IReadOnlyList<string>? roles = null,
        IReadOnlyList<string>? permissions = null,
        DateTimeOffset? expiresAt = null,
        string? username = null)
    {
        var now = DateTimeOffset.UtcNow;

        var identity = new ClaimsIdentity();
        identity.AddClaim(new Claim(AuthClaims.Subject, userId.ToString("D")));
        identity.AddClaim(new Claim(AuthClaims.Email, email));
        if (username is not null)
        {
            identity.AddClaim(new Claim(AuthClaims.Username, username));
        }

        foreach (var role in roles ?? [])
        {
            identity.AddClaim(new Claim(AuthClaims.Role, role));
        }

        foreach (var permission in permissions ?? [])
        {
            identity.AddClaim(new Claim(AuthClaims.Permission, permission));
        }

        var handler = new JsonWebTokenHandler();
        return handler.CreateToken(new SecurityTokenDescriptor
        {
            Subject = identity,
            Issuer = Issuer,
            Audience = Audience,
            IssuedAt = now.UtcDateTime,
            NotBefore = now.UtcDateTime,
            Expires = (expiresAt ?? now.AddMinutes(5)).UtcDateTime,
            SigningCredentials = new SigningCredentials(
                new SymmetricSecurityKey(Encoding.UTF8.GetBytes(SigningKey)),
                SecurityAlgorithms.HmacSha256),
        });
    }
}
