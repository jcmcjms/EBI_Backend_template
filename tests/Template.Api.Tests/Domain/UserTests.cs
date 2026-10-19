using Template.Api.Features.Users.Domain;

namespace Template.Api.Tests.Domain;

public class UserTests
{
    private static readonly DateTimeOffset Now = new(2026, 3, 1, 12, 0, 0, TimeSpan.Zero);

    private static User CreateUser(string email = "  Admin@Example.COM ", string username = " Ada.Lovelace ") =>
        User.Create(email, username, "  Ada ", "  Lovelace ", "hash", Now, "  Augusta  ", "  ", "  Analyst  ");

    [Fact]
    public void Create_NormalizesIdentityAndProfileFields()
    {
        var user = CreateUser();

        Assert.Equal("admin@example.com", user.Email);
        Assert.Equal("ada.lovelace", user.Username);
        Assert.Equal("Ada", user.FirstName);
        Assert.Equal("Augusta", user.MiddleName);
        Assert.Equal("Lovelace", user.LastName);
        Assert.Equal("Analyst", user.JobTitle);
        Assert.Equal("hash", user.PasswordHash);
        Assert.Equal(Now, user.CreatedAt);
        Assert.False(user.IsDisabled);
        Assert.Null(user.DisabledAt);
        Assert.NotEqual(Guid.Empty, user.Id);
    }

    [Fact]
    public void Create_NormalizesBlankOptionalFieldsToNull()
    {
        var user = User.Create("a@example.com", "user1", "A", "B", "hash", Now, "   ", null, "  ");

        Assert.Null(user.MiddleName);
        Assert.Null(user.Suffix);
        Assert.Null(user.JobTitle);
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void Create_RejectsMissingEmail(string? email)
    {
        Assert.ThrowsAny<ArgumentException>(() => User.Create(email!, "user1", "A", "B", "hash", Now));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("  ")]
    public void Create_RejectsMissingNames(string? firstName)
    {
        Assert.ThrowsAny<ArgumentException>(() => User.Create("a@example.com", "user1", firstName!, "B", "hash", Now));
    }

    [Fact]
    public void Create_RejectsMissingPasswordHash()
    {
        Assert.ThrowsAny<ArgumentException>(() => User.Create("a@example.com", "user1", "A", "B", "", Now));
    }

    [Theory]
    [InlineData("ab")]            // too short
    [InlineData("bad@name")]      // '@' is reserved for email identifiers
    [InlineData("has space")]
    [InlineData("unicode-üser")]
    public void Create_RejectsInvalidUsernames(string username)
    {
        Assert.ThrowsAny<ArgumentException>(
            () => User.Create("a@example.com", username, "A", "B", "hash", Now));
    }

    [Fact]
    public void Create_RejectsOverlongUsername()
    {
        var username = new string('a', User.MaxUsernameLength + 1);

        Assert.Throws<ArgumentException>(
            () => User.Create("a@example.com", username, "A", "B", "hash", Now));
    }

    [Fact]
    public void IsValidUsername_MatchesTheDocumentedCharset()
    {
        Assert.True(User.IsValidUsername("ada.lovelace_1843-x"));
        Assert.True(User.IsValidUsername(new string('a', User.MaxUsernameLength)));
        Assert.False(User.IsValidUsername(new string('a', User.MaxUsernameLength + 1)));
        Assert.False(User.IsValidUsername("ab"));
        Assert.False(User.IsValidUsername("bad+char"));
        Assert.False(User.IsValidUsername(null));
    }

    [Fact]
    public void NormalizeEmail_RejectsOverlongEmails()
    {
        var email = new string('a', User.MaxEmailLength) + "@example.com";

        Assert.Throws<ArgumentException>(() => User.NormalizeEmail(email));
    }

    [Fact]
    public void ChangeProfile_ReplacesAllFields_AndClearsOmittedOptionals()
    {
        var user = CreateUser();

        user.ChangeProfile("grace.hopper", "Grace", "Hopper", middleName: null, suffix: "Jr.", jobTitle: null);

        Assert.Equal("grace.hopper", user.Username);
        Assert.Equal("Grace", user.FirstName);
        Assert.Equal("Hopper", user.LastName);
        Assert.Null(user.MiddleName);
        Assert.Equal("Jr.", user.Suffix);
        Assert.Null(user.JobTitle);
    }

    [Fact]
    public void ChangeProfile_RejectsInvalidUsername()
    {
        var user = CreateUser();

        Assert.ThrowsAny<ArgumentException>(() => user.ChangeProfile("bad name", "A", "B", null, null, null));
    }

    [Fact]
    public void Disable_IsIdempotent_AndPreservesFirstDisabledTimestamp()
    {
        var user = CreateUser();

        user.Disable(Now);
        user.Disable(Now.AddHours(1));

        Assert.True(user.IsDisabled);
        Assert.Equal(Now, user.DisabledAt);
    }

    [Fact]
    public void Enable_ClearsDisabledState()
    {
        var user = CreateUser();
        user.Disable(Now);

        user.Enable();

        Assert.False(user.IsDisabled);
        Assert.Null(user.DisabledAt);
    }

    [Fact]
    public void ChangePassword_RejectsEmptyHash()
    {
        var user = CreateUser();

        Assert.ThrowsAny<ArgumentException>(() => user.ChangePassword(""));
    }
}
