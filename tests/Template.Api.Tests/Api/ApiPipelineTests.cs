using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Template.Api.Features.Users;
using Template.Api.Infrastructure.Authorization;
using Template.Api.Tests.Testing;

namespace Template.Api.Tests.Api;

/// <summary>
/// HTTP-level tests against the real pipeline (routing, JWT validation, authorization, request
/// validation, Problem Details error shapes) that do not require a database. Database-backed
/// behavior lives in the SQL Server integration tests.
/// </summary>
public sealed class ApiPipelineTests(ApiTestFactory factory) : IClassFixture<ApiTestFactory>
{
    private HttpClient CreateClient() => factory.CreateClient();

    [Fact]
    public async Task Liveness_ReturnsHealthyWithoutTouchingTheDatabase()
    {
        using var client = CreateClient();

        var response = await client.GetAsync("/health/live");

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.Equal("Healthy", await response.Content.ReadAsStringAsync());
    }

    [Fact]
    public async Task Readiness_ReportsServiceUnavailableWhenDatabaseIsUnreachable()
    {
        using var client = CreateClient();

        var response = await client.GetAsync("/health/ready");

        Assert.Equal(HttpStatusCode.ServiceUnavailable, response.StatusCode);
    }

    [Fact]
    public async Task Login_WithMissingPassword_Returns400()
    {
        using var client = CreateClient();

        var response = await client.PostAsJsonAsync("/api/auth/login", new { email = "someone@example.com" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
    }

    [Fact]
    public async Task Login_WithOversizedIdentifier_Returns400ValidationProblemDetails()
    {
        using var client = CreateClient();

        var response = await client.PostAsJsonAsync(
            "/api/auth/login",
            new { identifier = new string('a', 321), password = "whatever-123" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.StartsWith("application/problem+json", response.Content.Headers.ContentType?.MediaType ?? "", StringComparison.Ordinal);

        var problem = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        var errors = problem.RootElement.GetProperty("errors");
        Assert.True(errors.TryGetProperty("Identifier", out _) || errors.TryGetProperty("identifier", out _));
    }

    [Fact]
    public async Task ProtectedEndpoint_WithoutToken_Returns401ProblemDetails()
    {
        using var client = CreateClient();

        var response = await client.GetAsync("/api/users/me");

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
        Assert.StartsWith("application/problem+json", response.Content.Headers.ContentType?.MediaType ?? "", StringComparison.Ordinal);

        var problem = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal("Unauthenticated", problem.RootElement.GetProperty("title").GetString());
    }

    [Fact]
    public async Task PermissionProtectedEndpoint_WithoutRequiredPermission_Returns403ProblemDetails()
    {
        using var client = CreateClient();
        var token = TestAuth.CreateAccessToken(Guid.NewGuid(), "user@example.com", roles: [], permissions: []);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        var response = await client.GetAsync("/api/users");

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.StartsWith("application/problem+json", response.Content.Headers.ContentType?.MediaType ?? "", StringComparison.Ordinal);

        var problem = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        Assert.Equal("Forbidden", problem.RootElement.GetProperty("title").GetString());
    }

    [Fact]
    public async Task DatabaseFailure_SurfacesAsGeneric500ProblemDetailsWithoutLeakingInternals()
    {
        using var client = CreateClient();
        var token = TestAuth.CreateAccessToken(Guid.NewGuid(), "user@example.com", permissions: [Permissions.UsersRead]);
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

        // Authorization succeeds, then the use case hits the (unreachable) database.
        var response = await client.GetAsync("/api/users");

        Assert.Equal(HttpStatusCode.InternalServerError, response.StatusCode);
        var body = await response.Content.ReadAsStringAsync();
        var problem = JsonDocument.Parse(body);
        Assert.Equal("Internal server error", problem.RootElement.GetProperty("title").GetString());

        // No infrastructure details may leak to clients.
        Assert.DoesNotContain("SqlException", body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("stack", body, StringComparison.OrdinalIgnoreCase);
        Assert.DoesNotContain("127.0.0.1", body, StringComparison.Ordinal);
    }

    [Fact]
    public async Task OpenApiDocument_IsNotServedOutsideDevelopment()
    {
        using var client = CreateClient();

        var response = await client.GetAsync("/openapi/v1.json");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }

    [Fact]
    public async Task ScalarApiReference_IsNotServedOutsideDevelopment()
    {
        using var client = CreateClient();

        var response = await client.GetAsync("/scalar/v1");

        Assert.Equal(HttpStatusCode.NotFound, response.StatusCode);
    }
}
