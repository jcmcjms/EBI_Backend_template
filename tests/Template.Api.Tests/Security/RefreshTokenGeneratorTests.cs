using Template.Api.Infrastructure.Security;

namespace Template.Api.Tests.Security;

public class RefreshTokenGeneratorTests
{
    [Fact]
    public void Generate_ProducesUniqueHighEntropyTokens()
    {
        var tokens = Enumerable.Range(0, 100)
            .Select(_ => RefreshTokenGenerator.Generate())
            .ToList();

        Assert.Equal(100, tokens.Distinct(StringComparer.Ordinal).Count());
        // 32 random bytes, base64url-encoded without padding.
        Assert.All(tokens, token => Assert.Equal(43, token.Length));
        Assert.All(tokens, token => Assert.Matches("^[A-Za-z0-9_-]+$", token));
    }

    [Fact]
    public void ComputeHash_IsDeterministicAndTokenSpecific()
    {
        var token = RefreshTokenGenerator.Generate();

        var first = RefreshTokenGenerator.ComputeHash(token);
        var second = RefreshTokenGenerator.ComputeHash(token);
        var other = RefreshTokenGenerator.ComputeHash(RefreshTokenGenerator.Generate());

        Assert.Equal(first, second, StringComparer.Ordinal);
        Assert.NotEqual(first, other, StringComparer.Ordinal);
        // SHA-256 hex digest.
        Assert.Equal(64, first.Length);
        Assert.Matches("^[0-9A-F]{64}$", first);
    }

    [Fact]
    public void ComputeHash_DoesNotRevealTheToken()
    {
        var token = RefreshTokenGenerator.Generate();

        var hash = RefreshTokenGenerator.ComputeHash(token);

        Assert.DoesNotContain(token, hash, StringComparison.Ordinal);
    }
}
