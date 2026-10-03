using System.Net;
using System.Net.Http.Json;
using EbiBackend.Features.Auth.Contracts;
using FluentAssertions;

namespace EbiBackend.Tests.Features.Auth;

public class RefreshEndpointTests : IClassFixture<Helpers.TestWebApplicationFactory>
{
    private readonly HttpClient _client;

    public RefreshEndpointTests(Helpers.TestWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Refresh_WithValidToken_ReturnsNewTokens()
    {
        var registerRequest = new RegisterRequest("refresh@example.com", "Password123!", "Refresh User");
        await _client.PostAsJsonAsync("/auth/register", registerRequest);

        var loginRequest = new LoginRequest("refresh@example.com", "Password123!");
        var loginResponse = await _client.PostAsJsonAsync("/auth/login", loginRequest);
        var tokens = await loginResponse.Content.ReadFromJsonAsync<TokenResponse>();

        var refreshRequest = new RefreshRequest(tokens!.RefreshToken);
        var response = await _client.PostAsJsonAsync("/auth/refresh", refreshRequest);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var newTokens = await response.Content.ReadFromJsonAsync<TokenResponse>();
        newTokens.Should().NotBeNull();
        newTokens!.AccessToken.Should().NotBeNullOrWhiteSpace();
        newTokens.RefreshToken.Should().NotBeNullOrWhiteSpace();
        newTokens.RefreshToken.Should().NotBe(tokens.RefreshToken);
    }

    [Fact]
    public async Task Refresh_WithRevokedToken_ReturnsUnauthorized()
    {
        var registerRequest = new RegisterRequest("revoke@example.com", "Password123!", "Revoke User");
        await _client.PostAsJsonAsync("/auth/register", registerRequest);

        var loginRequest = new LoginRequest("revoke@example.com", "Password123!");
        var loginResponse = await _client.PostAsJsonAsync("/auth/login", loginRequest);
        var tokens = await loginResponse.Content.ReadFromJsonAsync<TokenResponse>();

        var refreshRequest = new RefreshRequest(tokens!.RefreshToken);
        await _client.PostAsJsonAsync("/auth/refresh", refreshRequest);

        var response = await _client.PostAsJsonAsync("/auth/refresh", refreshRequest);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Refresh_WithInvalidToken_ReturnsUnauthorized()
    {
        var refreshRequest = new RefreshRequest("invalid-token");
        var response = await _client.PostAsJsonAsync("/auth/refresh", refreshRequest);

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }
}