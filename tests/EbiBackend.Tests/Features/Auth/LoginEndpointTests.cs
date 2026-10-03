using System.Net;
using System.Net.Http.Json;
using EbiBackend.Features.Auth.Contracts;
using FluentAssertions;

namespace EbiBackend.Tests.Features.Auth;

public class LoginEndpointTests : IClassFixture<Helpers.TestWebApplicationFactory>
{
    private readonly HttpClient _client;

    public LoginEndpointTests(Helpers.TestWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Login_WithValidCredentials_ReturnsTokens()
    {
        var registerRequest = new RegisterRequest("login@example.com", "Password123!", "Login User");
        await _client.PostAsJsonAsync("/auth/register", registerRequest);

        var loginRequest = new LoginRequest("login@example.com", "Password123!");
        var response = await _client.PostAsJsonAsync("/auth/login", loginRequest);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var tokens = await response.Content.ReadFromJsonAsync<TokenResponse>();
        tokens.Should().NotBeNull();
        tokens!.AccessToken.Should().NotBeNullOrWhiteSpace();
        tokens.RefreshToken.Should().NotBeNullOrWhiteSpace();
    }

    [Fact]
    public async Task Login_WithWrongPassword_ReturnsUnauthorized()
    {
        var registerRequest = new RegisterRequest("wrong@example.com", "Password123!", "Wrong User");
        await _client.PostAsJsonAsync("/auth/register", registerRequest);

        var loginRequest = new LoginRequest("wrong@example.com", "WrongPassword!");
        var response = await _client.PostAsJsonAsync("/auth/login", loginRequest);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Login_WithNonexistentEmail_ReturnsUnauthorized()
    {
        var loginRequest = new LoginRequest("noone@example.com", "Password123!");
        var response = await _client.PostAsJsonAsync("/auth/login", loginRequest);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}