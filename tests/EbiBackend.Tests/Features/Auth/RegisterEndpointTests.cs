using System.Net;
using System.Net.Http.Json;
using EbiBackend.Features.Auth.Contracts;
using FluentAssertions;

namespace EbiBackend.Tests.Features.Auth;

public class RegisterEndpointTests : IClassFixture<Helpers.TestWebApplicationFactory>
{
    private readonly HttpClient _client;

    public RegisterEndpointTests(Helpers.TestWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Register_WithValidRequest_ReturnsCreated()
    {
        var request = new RegisterRequest("test@example.com", "Password123!", "Test User");

        var response = await _client.PostAsJsonAsync("/auth/register", request);

        response.StatusCode.Should().Be(HttpStatusCode.Created);
        var user = await response.Content.ReadFromJsonAsync<UserProfileResponse>();
        user.Should().NotBeNull();
        user!.Email.Should().Be("test@example.com");
        user.DisplayName.Should().Be("Test User");
    }

    [Fact]
    public async Task Register_WithDuplicateEmail_ReturnsConflict()
    {
        var request = new RegisterRequest("duplicate@example.com", "Password123!", "User One");

        await _client.PostAsJsonAsync("/auth/register", request);
        var response = await _client.PostAsJsonAsync("/auth/register", request);

        response.StatusCode.Should().Be(HttpStatusCode.Conflict);
    }

    [Fact]
    public async Task Register_WithInvalidEmail_ReturnsBadRequest()
    {
        var request = new RegisterRequest("invalid-email", "Password123!", "Test User");

        var response = await _client.PostAsJsonAsync("/auth/register", request);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Register_WithShortPassword_ReturnsBadRequest()
    {
        var request = new RegisterRequest("test2@example.com", "short", "Test User");

        var response = await _client.PostAsJsonAsync("/auth/register", request);

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }
}