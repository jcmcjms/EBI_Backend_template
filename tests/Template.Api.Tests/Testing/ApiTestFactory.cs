using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;

namespace Template.Api.Tests.Testing;

/// <summary>
/// Hosts the real application for HTTP-level tests that do not need a database:
/// token validation, authorization, request validation, and health/liveness.
/// The connection string points at a closed port; any test that reaches the database
/// proves the failure path (500 Problem Details) instead.
/// </summary>
public sealed class ApiTestFactory : WebApplicationFactory<Program>
{
    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.UseEnvironment("Testing");
        builder.UseSetting("ConnectionStrings:Database", "Server=127.0.0.1,14330;Database=pipeline-tests-unused;User Id=unused;Password=unused;TrustServerCertificate=True");
        builder.UseSetting("Database:ApplyMigrations", "false");
        builder.UseSetting("Auth:Issuer", TestAuth.Issuer);
        builder.UseSetting("Auth:Audience", TestAuth.Audience);
        builder.UseSetting("Auth:SigningKey", TestAuth.SigningKey);
        // Keep brute-force limits from coupling pipeline test outcomes to execution order.
        builder.UseSetting("RateLimiting:AuthenticationPermitLimit", "100000");
    }
}
