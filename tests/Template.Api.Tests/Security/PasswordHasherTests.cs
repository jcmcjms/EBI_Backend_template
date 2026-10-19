using Microsoft.Extensions.Options;
using Template.Api.Infrastructure.Security;

namespace Template.Api.Tests.Security;

public class PasswordHasherTests
{
    // Low (but valid) iteration count keeps tests fast; production default is 600k.
    private static PasswordHasher CreateHasher(int iterationCount) =>
        new(Options.Create(new PasswordHasherOptions { IterationCount = iterationCount }));

    [Fact]
    public void Hash_Then_Verify_Roundtrips()
    {
        var hasher = CreateHasher(100_000);

        var encoded = hasher.Hash("correct horse battery staple");

        Assert.True(hasher.Verify("correct horse battery staple", encoded));
    }

    [Fact]
    public void Verify_RejectsWrongPassword()
    {
        var hasher = CreateHasher(100_000);
        var encoded = hasher.Hash("correct horse battery staple");

        Assert.False(hasher.Verify("wrong horse battery staple", encoded));
    }

    [Fact]
    public void Verify_RejectsTamperedHashWithoutThrowing()
    {
        var hasher = CreateHasher(100_000);
        var encoded = hasher.Hash("correct horse battery staple");

        // Flip one character in the subkey segment.
        var lastSeparator = encoded.LastIndexOf('$');
        var tampered = encoded[..(lastSeparator + 1)] +
            (encoded[lastSeparator + 1] == 'A' ? 'B' : 'A') +
            encoded[(lastSeparator + 2)..];

        Assert.False(hasher.Verify("correct horse battery staple", tampered));
    }

    [Theory]
    [InlineData("")]
    [InlineData("not-a-hash")]
    [InlineData("PBKDF2-SHA512$100000$AAAA$BBBB")]
    [InlineData("PBKDF2-SHA256$10$AAAAAAAAAAAAAAAAAAAAAA==$AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA=")]
    [InlineData("PBKDF2-SHA256$notanumber$AAAAAAAAAAAAAAAAAAAAAA==$AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA=")]
    public void Verify_RejectsMalformedHashes(string malformed)
    {
        var hasher = CreateHasher(100_000);

        Assert.False(hasher.Verify("any-password", malformed));
    }

    [Fact]
    public void Hash_UsesRandomSalt_SoIdenticalPasswordsProduceDifferentHashes()
    {
        var hasher = CreateHasher(100_000);

        var first = hasher.Hash("same-password");
        var second = hasher.Hash("same-password");

        Assert.NotEqual(first, second);
        Assert.True(hasher.Verify("same-password", first));
        Assert.True(hasher.Verify("same-password", second));
    }

    [Fact]
    public void Verify_AcceptsHashFromLowerIterationCount_AndNeedsRehashFlagsIt()
    {
        var oldHasher = CreateHasher(100_000);
        var newHasher = CreateHasher(150_000);
        var encoded = oldHasher.Hash("legacy-password");

        // Stored parameters are honored for verification...
        Assert.True(newHasher.Verify("legacy-password", encoded));
        // ...but the hash is flagged for transparent upgrade on successful login.
        Assert.True(newHasher.NeedsRehash(encoded));
        Assert.False(oldHasher.NeedsRehash(encoded));

        var upgraded = newHasher.Hash("legacy-password");
        Assert.False(newHasher.NeedsRehash(upgraded));
    }

    [Fact]
    public void OptionsValidator_RejectsTooFewIterations()
    {
        var exception = Assert.Throws<InvalidOperationException>(
            () => PasswordHasherOptionsValidator.Validate(new PasswordHasherOptions { IterationCount = 1_000 }));

        Assert.Contains("IterationCount", exception.Message, StringComparison.Ordinal);
    }
}
