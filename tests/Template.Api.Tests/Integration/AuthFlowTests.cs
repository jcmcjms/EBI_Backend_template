using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using Template.Api.Features.Auth;
using Template.Api.Features.Users;
using Template.Api.Infrastructure.Authorization;

namespace Template.Api.Tests.Integration;

/// <summary>End-to-end authentication flows against real SQL Server: login by email or username, token rotation, reuse detection, logout, password change/reset.</summary>
[Collection(SqlServerIntegrationCollectionDefinition.Name)]
public sealed class AuthFlowTests(SqlServerIntegrationFixture fixture)
{
    private const string NewPassword = "Rotated-Password-123!";
    private const string InitialPassword = "Initial-Password-123!";

    /// <summary>Username the fixture's seeded admin derives from its email local part.</summary>
    private const string SeededAdminUsername = "integration-admin";

    private async Task<HttpClient> CreateAdminClientAsync()
    {
        var client = fixture.CreateClient();
        var tokens = await IntegrationHttp.LoginAsync(
            client,
            SqlServerIntegrationFixture.SeedAdminEmail,
            SqlServerIntegrationFixture.SeedAdminPassword);
        return client.WithBearer(tokens.AccessToken);
    }

    private async Task<(HttpClient Client, AuthTokenResponse Tokens, string Email, string Username)> CreateAndLoginUserAsync(string prefix)
    {
        using var admin = await CreateAdminClientAsync();
        var email = IntegrationHttp.UniqueEmail(prefix);
        var username = IntegrationHttp.UniqueUsername(prefix);
        var createResponse = await admin.PostAsJsonAsync("/api/users", new CreateUserRequest
        {
            Email = email,
            Username = username,
            FirstName = "Integration",
            LastName = "User",
            Password = InitialPassword,
        });
        Assert.Equal(HttpStatusCode.Created, createResponse.StatusCode);

        var client = fixture.CreateClient();
        var tokens = await IntegrationHttp.LoginAsync(client, email, InitialPassword);
        return (client, tokens, email, username);
    }

    [Fact]
    public async Task Startup_SeedsAdmin_WhoCanLoginAndSeeFullRbacProfile()
    {
        fixture.SkipIfUnavailable();

        using var client = fixture.CreateClient();
        var tokens = await IntegrationHttp.LoginAsync(
            client,
            SqlServerIntegrationFixture.SeedAdminEmail,
            SqlServerIntegrationFixture.SeedAdminPassword);

        Assert.False(string.IsNullOrWhiteSpace(tokens.AccessToken));
        Assert.False(string.IsNullOrWhiteSpace(tokens.RefreshToken));
        Assert.Equal("Bearer", tokens.TokenType);
        Assert.True(tokens.ExpiresInSeconds > 0);

        var me = await client.WithBearer(tokens.AccessToken)
            .GetFromJsonAsync<CurrentUserResponse>("/api/users/me", IntegrationHttp.Json);
        Assert.NotNull(me);
        Assert.Equal(SqlServerIntegrationFixture.SeedAdminEmail, me.Email);
        Assert.Equal(SeededAdminUsername, me.Username);
        Assert.Equal("Admin", me.FirstName);
        Assert.Equal("Admin", me.LastName);
        Assert.Contains("Admin", me.Roles);
        Assert.Equal(
            [.. Permissions.All.Order(StringComparer.Ordinal)],
            [.. me.Permissions.Order(StringComparer.Ordinal)]);
    }

    [Fact]
    public async Task Login_WithUsername_SucceedsLikeEmail()
    {
        fixture.SkipIfUnavailable();
        var (_, _, email, username) = await CreateAndLoginUserAsync("byname");

        using var client = fixture.CreateClient();
        var byUsername = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest
        {
            Identifier = username.ToUpperInvariant(), // usernames normalize case-insensitively
            Password = InitialPassword,
        });
        Assert.Equal(HttpStatusCode.OK, byUsername.StatusCode);

        var byEmail = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest { Identifier = email, Password = InitialPassword });
        Assert.Equal(HttpStatusCode.OK, byEmail.StatusCode);
    }

    [Fact]
    public async Task Login_WithWrongPassword_Returns401WithoutLeakingTheReason()
    {
        fixture.SkipIfUnavailable();
        using var client = fixture.CreateClient();

        var response = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest
        {
            Identifier = SqlServerIntegrationFixture.SeedAdminEmail,
            Password = "IntegrationAdminPassword123-WRONG",
        });

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        var problem = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal("Invalid credentials", problem.RootElement.GetProperty("title").GetString());
    }

    [Fact]
    public async Task Login_WithUnknownIdentifier_ReturnsTheSame401AsWrongPassword()
    {
        fixture.SkipIfUnavailable();
        using var client = fixture.CreateClient();

        var byUnknownEmail = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest
        {
            Identifier = IntegrationHttp.UniqueEmail("ghost"),
            Password = "does-not-matter-123!",
        });
        var byUnknownUsername = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest
        {
            Identifier = IntegrationHttp.UniqueUsername("ghost"),
            Password = "does-not-matter-123!",
        });

        Assert.Equal(HttpStatusCode.Unauthorized, byUnknownEmail.StatusCode);
        Assert.Equal(HttpStatusCode.Unauthorized, byUnknownUsername.StatusCode);
        var problem = JsonDocument.Parse(await byUnknownEmail.Content.ReadAsStringAsync());
        Assert.Equal("Invalid credentials", problem.RootElement.GetProperty("title").GetString());
    }

    [Fact]
    public async Task Refresh_RotatesTokens_AndReusingARotatedTokenRevokesEverySession()
    {
        fixture.SkipIfUnavailable();
        var (client, tokens, _, _) = await CreateAndLoginUserAsync("rotate");

        // First refresh succeeds and returns a new pair.
        var refreshResponse = await client.PostAsJsonAsync("/api/auth/refresh", new RefreshSessionRequest { RefreshToken = tokens.RefreshToken });
        Assert.Equal(HttpStatusCode.OK, refreshResponse.StatusCode);
        var rotated = await refreshResponse.Content.ReadFromJsonAsync<AuthTokenResponse>(IntegrationHttp.Json);
        Assert.NotNull(rotated);
        Assert.NotEqual(tokens.RefreshToken, rotated.RefreshToken);

        // The rotated token works.
        var secondRefresh = await client.PostAsJsonAsync("/api/auth/refresh", new RefreshSessionRequest { RefreshToken = rotated.RefreshToken });
        Assert.Equal(HttpStatusCode.OK, secondRefresh.StatusCode);
        var rotatedAgain = await secondRefresh.Content.ReadFromJsonAsync<AuthTokenResponse>(IntegrationHttp.Json);
        Assert.NotNull(rotatedAgain);

        // Replaying an already-rotated token is treated as theft: 401 and every session dies.
        var reuse = await client.PostAsJsonAsync("/api/auth/refresh", new RefreshSessionRequest { RefreshToken = tokens.RefreshToken });
        Assert.Equal(HttpStatusCode.Unauthorized, reuse.StatusCode);

        var afterReuse = await client.PostAsJsonAsync("/api/auth/refresh", new RefreshSessionRequest { RefreshToken = rotatedAgain.RefreshToken });
        Assert.Equal(HttpStatusCode.Unauthorized, afterReuse.StatusCode);
    }

    [Fact]
    public async Task Logout_RevokesTheRefreshToken_Idempotently()
    {
        fixture.SkipIfUnavailable();
        var (client, tokens, _, _) = await CreateAndLoginUserAsync("logout");

        var first = await client.PostAsJsonAsync("/api/auth/logout", new LogoutRequest { RefreshToken = tokens.RefreshToken });
        Assert.Equal(HttpStatusCode.NoContent, first.StatusCode);

        var second = await client.PostAsJsonAsync("/api/auth/logout", new LogoutRequest { RefreshToken = tokens.RefreshToken });
        Assert.Equal(HttpStatusCode.NoContent, second.StatusCode);

        var refresh = await client.PostAsJsonAsync("/api/auth/refresh", new RefreshSessionRequest { RefreshToken = tokens.RefreshToken });
        Assert.Equal(HttpStatusCode.Unauthorized, refresh.StatusCode);
    }

    [Fact]
    public async Task ChangeOwnPassword_RequiresCurrentPassword_AndRevokesSessions()
    {
        fixture.SkipIfUnavailable();
        var (client, tokens, email, _) = await CreateAndLoginUserAsync("pwchange");
        client.WithBearer(tokens.AccessToken);

        var wrongCurrent = await client.PostAsJsonAsync("/api/users/me/password", new ChangeOwnPasswordRequest
        {
            CurrentPassword = "not-the-current-one!",
            NewPassword = NewPassword,
        });
        Assert.Equal(HttpStatusCode.BadRequest, wrongCurrent.StatusCode);

        var correct = await client.PostAsJsonAsync("/api/users/me/password", new ChangeOwnPasswordRequest
        {
            CurrentPassword = InitialPassword,
            NewPassword = NewPassword,
        });
        Assert.Equal(HttpStatusCode.NoContent, correct.StatusCode);

        // Every session must re-authenticate after a password change, including the current one.
        var refresh = await client.PostAsJsonAsync("/api/auth/refresh", new RefreshSessionRequest { RefreshToken = tokens.RefreshToken });
        Assert.Equal(HttpStatusCode.Unauthorized, refresh.StatusCode);

        using var freshClient = fixture.CreateClient();
        var oldPasswordLogin = await freshClient.PostAsJsonAsync("/api/auth/login", new LoginRequest { Identifier = email, Password = InitialPassword });
        Assert.Equal(HttpStatusCode.Unauthorized, oldPasswordLogin.StatusCode);
        var newPasswordLogin = await freshClient.PostAsJsonAsync("/api/auth/login", new LoginRequest { Identifier = email, Password = NewPassword });
        Assert.Equal(HttpStatusCode.OK, newPasswordLogin.StatusCode);
    }

    [Fact]
    public async Task AdminPasswordReset_RevokesSessions_AndEnforcesThePolicy()
    {
        fixture.SkipIfUnavailable();
        var (userClient, userTokens, email, _) = await CreateAndLoginUserAsync("pwreset");
        var userId = await GetUserIdAsync(userClient.WithBearer(userTokens.AccessToken));
        using var admin = await CreateAdminClientAsync();

        var tooWeak = await admin.PostAsJsonAsync($"/api/users/{userId}/password", new ResetUserPasswordRequest { NewPassword = "short" });
        Assert.Equal(HttpStatusCode.BadRequest, tooWeak.StatusCode);

        var reset = await admin.PostAsJsonAsync($"/api/users/{userId}/password", new ResetUserPasswordRequest { NewPassword = NewPassword });
        Assert.Equal(HttpStatusCode.NoContent, reset.StatusCode);

        var refresh = await userClient.PostAsJsonAsync("/api/auth/refresh", new RefreshSessionRequest { RefreshToken = userTokens.RefreshToken });
        Assert.Equal(HttpStatusCode.Unauthorized, refresh.StatusCode);

        using var freshClient = fixture.CreateClient();
        var login = await freshClient.PostAsJsonAsync("/api/auth/login", new LoginRequest { Identifier = email, Password = NewPassword });
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
    }

    private static async Task<Guid> GetUserIdAsync(HttpClient client)
    {
        var me = await client.GetFromJsonAsync<CurrentUserResponse>("/api/users/me", IntegrationHttp.Json);
        Assert.NotNull(me);
        return me.Id;
    }
}
