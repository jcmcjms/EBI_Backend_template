using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using EbiBackend.Features.Auth.Contracts;
using EbiBackend.Features.Users.Contracts;
using FluentAssertions;

namespace EbiBackend.Tests.Features.Users;

public class UserEndpointTests : IClassFixture<Helpers.TestWebApplicationFactory>
{
    private readonly HttpClient _client;

    public UserEndpointTests(Helpers.TestWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
    }

    private async Task<(string AccessToken, Guid UserId)> RegisterAndLoginAsync(string email = "usercrud@example.com", string password = "Password123!", string displayName = "CRUD User")
    {
        var registerRequest = new RegisterRequest(email, password, displayName);
        var registerResponse = await _client.PostAsJsonAsync("/auth/register", registerRequest);
        var registered = await registerResponse.Content.ReadFromJsonAsync<UserProfileResponse>();

        var loginRequest = new LoginRequest(email, password);
        var loginResponse = await _client.PostAsJsonAsync("/auth/login", loginRequest);
        var tokens = await loginResponse.Content.ReadFromJsonAsync<TokenResponse>();
        return (tokens!.AccessToken, registered!.Id);
    }

    [Fact]
    public async Task GetUsers_WithoutAuth_ReturnsUnauthorized()
    {
        var response = await _client.GetAsync("/users");

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task GetUsers_WithAuth_ReturnsOk()
    {
        var (token, _) = await RegisterAndLoginAsync();
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await _client.GetAsync("/users");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var users = await response.Content.ReadFromJsonAsync<List<UserProfileResponse>>();
        users.Should().NotBeNull();
        users.Should().NotBeEmpty();
    }

    [Fact]
    public async Task GetUserById_WithAuth_ReturnsUser()
    {
        var (token, userId) = await RegisterAndLoginAsync("getbyid@example.com");
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await _client.GetAsync($"/users/{userId}");

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var user = await response.Content.ReadFromJsonAsync<UserProfileResponse>();
        user.Should().NotBeNull();
        user!.Id.Should().Be(userId);
    }

    [Fact]
    public async Task GetUserById_WithInvalidId_ReturnsNotFound()
    {
        var (token, _) = await RegisterAndLoginAsync("notfound@example.com");
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await _client.GetAsync($"/users/{Guid.NewGuid()}");

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
    }

    [Fact]
    public async Task UpdateUser_WithAuth_ReturnsUpdatedUser()
    {
        var (token, userId) = await RegisterAndLoginAsync("update@example.com");
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var updateRequest = new UpdateUserRequest("Updated Name", "update@example.com");
        var response = await _client.PutAsJsonAsync($"/users/{userId}", updateRequest);

        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var updated = await response.Content.ReadFromJsonAsync<UserProfileResponse>();
        updated.Should().NotBeNull();
        updated!.DisplayName.Should().Be("Updated Name");
    }

    [Fact]
    public async Task DeleteUser_WithAuth_ReturnsNoContent()
    {
        var (token, userId) = await RegisterAndLoginAsync("delete@example.com");
        _client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await _client.DeleteAsync($"/users/{userId}");

        response.StatusCode.Should().Be(HttpStatusCode.NoContent);
    }
}