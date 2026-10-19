using Template.Api.Infrastructure.Security;
using Template.Api.Tests.Testing;

namespace Template.Api.Tests.Security;

public class AuthOptionsValidatorTests
{
    private static AuthOptions ValidOptions() => new()
    {
        Issuer = "Template.Api",
        Audience = "Template.Api.Clients",
        SigningKey = TestAuth.SigningKey,
        AccessTokenLifetime = TimeSpan.FromMinutes(15),
        RefreshTokenLifetime = TimeSpan.FromDays(14),
    };

    [Fact]
    public void Validate_AcceptsCompleteOptionsOutsideProduction()
    {
        AuthOptionsValidator.Validate(ValidOptions(), new StubHostEnvironment("Development"));
        AuthOptionsValidator.Validate(ValidOptions(), new StubHostEnvironment("Production"));
    }

    [Fact]
    public void Validate_RejectsDevelopmentSigningKeyInProduction()
    {
        var withDevKey = new AuthOptions
        {
            Issuer = "Template.Api",
            Audience = "Template.Api.Clients",
            SigningKey = AuthOptions.DevelopmentSigningKey,
        };

        var development = new StubHostEnvironment("Development");
        AuthOptionsValidator.Validate(withDevKey, development); // allowed for local development

        var production = new StubHostEnvironment("Production");
        var exception = Assert.Throws<InvalidOperationException>(() => AuthOptionsValidator.Validate(withDevKey, production));
        Assert.Contains("development key", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Validate_RejectsMissingIssuer()
    {
        var options = new AuthOptions
        {
            Issuer = "",
            Audience = "Template.Api.Clients",
            SigningKey = TestAuth.SigningKey,
        };

        var exception = Assert.Throws<InvalidOperationException>(
            () => AuthOptionsValidator.Validate(options, new StubHostEnvironment("Development")));

        Assert.Contains("Issuer", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Validate_RejectsShortSigningKey()
    {
        var options = new AuthOptions
        {
            Issuer = "i",
            Audience = "a",
            SigningKey = "too-short",
        };

        var exception = Assert.Throws<InvalidOperationException>(
            () => AuthOptionsValidator.Validate(options, new StubHostEnvironment("Development")));

        Assert.Contains("at least 32", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Validate_RejectsIncoherentLifetimes()
    {
        var tooLongAccess = new AuthOptions
        {
            Issuer = "i",
            Audience = "a",
            SigningKey = TestAuth.SigningKey,
            AccessTokenLifetime = TimeSpan.FromHours(2),
            RefreshTokenLifetime = TimeSpan.FromDays(14),
        };
        Assert.Throws<InvalidOperationException>(
            () => AuthOptionsValidator.Validate(tooLongAccess, new StubHostEnvironment("Development")));

        var refreshShorterThanAccess = new AuthOptions
        {
            Issuer = "i",
            Audience = "a",
            SigningKey = TestAuth.SigningKey,
            AccessTokenLifetime = TimeSpan.FromMinutes(15),
            RefreshTokenLifetime = TimeSpan.FromMinutes(10),
        };
        var exception = Assert.Throws<InvalidOperationException>(
            () => AuthOptionsValidator.Validate(refreshShorterThanAccess, new StubHostEnvironment("Development")));
        Assert.Contains("RefreshTokenLifetime", exception.Message, StringComparison.Ordinal);
    }
}
