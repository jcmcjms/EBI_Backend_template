using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using Template.Api.Features.Users.Domain;
using Template.Api.Infrastructure.Security;
using Template.Api.Tests.Testing;

namespace Template.Api.Tests.Security;

public class JwtAccessTokenIssuerTests
{
    // Anchored to the real clock so lifetime validation succeeds; all assertions are relative to this value.
    private static readonly DateTimeOffset FixedNow = DateTimeOffset.UtcNow;

    private static (JwtAccessTokenIssuer Issuer, AuthOptions Options) CreateIssuer(DateTimeOffset? now = null)
    {
        var options = new AuthOptions
        {
            Issuer = TestAuth.Issuer,
            Audience = TestAuth.Audience,
            SigningKey = TestAuth.SigningKey,
            AccessTokenLifetime = TimeSpan.FromMinutes(15),
        };
        var issuer = new JwtAccessTokenIssuer(
            Options.Create(options),
            new StubTimeProvider(now ?? FixedNow));
        return (issuer, options);
    }

    [Fact]
    public async Task Issue_ProducesTokenThatValidatesWithIdentityAndRbacClaims()
    {
        var (issuer, options) = CreateIssuer();
        var user = User.Create("admin@example.com", "ada", "Ada", "Lovelace", "not-a-real-hash", FixedNow);

        var issued = issuer.Issue(user, ["Admin", "Auditor"], ["users.read", "roles.read"]);

        var handler = new JsonWebTokenHandler { MapInboundClaims = false }; // mirrors production JwtBearerOptions
        var result = await handler.ValidateTokenAsync(issued.Token, new TokenValidationParameters
        {
            ValidateIssuer = true,
            ValidIssuer = options.Issuer,
            ValidateAudience = true,
            ValidAudience = options.Audience,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = JwtAccessTokenIssuer.CreateSigningKey(options),
            ValidateLifetime = true,
            ClockSkew = TimeSpan.Zero,
            RoleClaimType = AuthClaims.Role,
        });

        Assert.True(result.IsValid, result.Exception?.Message);

        Assert.Equal(user.Id.ToString("D"), SingleClaim(result, AuthClaims.Subject));
        Assert.Equal("admin@example.com", SingleClaim(result, AuthClaims.Email));
        Assert.Equal("ada", SingleClaim(result, AuthClaims.Username));
        Assert.Equal(
            ["Admin", "Auditor"],
            [.. ClaimValues(result, AuthClaims.Role).Order(StringComparer.Ordinal)]);
        Assert.Equal(
            ["roles.read", "users.read"],
            [.. ClaimValues(result, AuthClaims.Permission).Order(StringComparer.Ordinal)]);

        Assert.Equal(TruncateToSeconds(FixedNow.Add(options.AccessTokenLifetime)), issued.ExpiresAt);
        var jwt = new JsonWebToken(issued.Token);
        Assert.Equal(TruncateToSeconds(FixedNow.Add(options.AccessTokenLifetime)).UtcDateTime, jwt.ValidTo);
    }

    private static DateTimeOffset TruncateToSeconds(DateTimeOffset value) =>
        value.AddTicks(-(value.UtcTicks % TimeSpan.TicksPerSecond));

    [Fact]
    public async Task Issue_RejectsTokenSignedWithDifferentKey()
    {
        var (issuer, _) = CreateIssuer();
        var user = User.Create("user@example.com", "user1", "Test", "User", "hash", FixedNow);

        var issued = issuer.Issue(user, [], []);

        var handler = new JsonWebTokenHandler { MapInboundClaims = false }; // mirrors production JwtBearerOptions
        var result = await handler.ValidateTokenAsync(issued.Token, new TokenValidationParameters
        {
            ValidateIssuer = false,
            ValidateAudience = false,
            ValidateIssuerSigningKey = true,
            IssuerSigningKey = new SymmetricSecurityKey(
                System.Text.Encoding.UTF8.GetBytes("a-completely-different-test-key-0123456789")),
            ValidateLifetime = true,
        });

        Assert.False(result.IsValid);
    }

    private static IEnumerable<string> ClaimValues(TokenValidationResult result, string claimType)
    {
        var identity = result.ClaimsIdentity ?? throw new InvalidOperationException("Token validation produced no identity.");
        return identity.FindAll(claimType).Select(claim => claim.Value);
    }

    private static string SingleClaim(TokenValidationResult result, string claimType) =>
        Assert.Single(ClaimValues(result, claimType));
}
