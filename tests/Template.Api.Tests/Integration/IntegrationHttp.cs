using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;
using Template.Api.Features.Auth;

namespace Template.Api.Tests.Integration;

/// <summary>Shared HTTP helpers for the integration tests. Reuses the production response contracts.</summary>
internal static class IntegrationHttp
{
    public static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    public static HttpClient WithBearer(this HttpClient client, string accessToken)
    {
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", accessToken);
        return client;
    }

    public static async Task<AuthTokenResponse> LoginAsync(HttpClient client, string identifier, string password)
    {
        var response = await client.PostAsJsonAsync("/api/auth/login", new LoginRequest { Identifier = identifier, Password = password });
        response.EnsureSuccessStatusCode();
        return await response.Content.ReadFromJsonAsync<AuthTokenResponse>(Json)
            ?? throw new InvalidOperationException("Login succeeded but returned no token payload.");
    }

    public static string UniqueEmail(string prefix) => $"{prefix}-{Guid.NewGuid():N}@integration.test";

    /// <summary>Unique username inside the allowed charset (lowercase letters/digits), 3–64 chars.</summary>
    public static string UniqueUsername(string prefix) =>
        $"{prefix.ToLowerInvariant()}{Guid.NewGuid():N}"[..Math.Min(64, prefix.Length + 32)];
}
