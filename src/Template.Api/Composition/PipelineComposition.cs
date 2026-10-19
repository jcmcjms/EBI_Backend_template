using Microsoft.AspNetCore.Diagnostics.HealthChecks;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Scalar.AspNetCore;
using Template.Api.Features.Auth;
using Template.Api.Features.Roles;
using Template.Api.Features.Users;
using Template.Api.Infrastructure.Observability;
using Template.Api.Infrastructure.Persistence;

namespace Template.Api.Composition;

/// <summary>
/// Builds the HTTP pipeline and the optional startup database initialization.
/// Middleware order is explicit: exception handling first, authentication before authorization,
/// authorization before endpoints.
/// </summary>
public static class PipelineComposition
{
    public static WebApplication ConfigurePipeline(this WebApplication app)
    {
        ArgumentNullException.ThrowIfNull(app);

        app.UseExceptionHandler();

        if (!app.Environment.IsDevelopment())
        {
            app.UseHsts();
            app.UseHttpsRedirection();
        }

        app.UseRateLimiter();

        var allowedOrigins = app.Configuration.GetSection("Cors:AllowedOrigins").Get<string[]>() ?? [];
        if (allowedOrigins.Length > 0)
        {
            app.UseCors(SecurityComposition.CorsPolicyName);
        }

        if (app.Environment.IsDevelopment())
        {
            app.MapOpenApi();
            app.MapScalarApiReference();
        }

        app.UseAuthentication();
        app.UseAuthorization();

        // Liveness never touches dependencies; readiness checks SQL Server within a bounded timeout.
        app.MapHealthChecks("/health/live", new HealthCheckOptions { Predicate = _ => false });
        app.MapHealthChecks("/health/ready", new HealthCheckOptions { Predicate = check => check.Tags.Contains("ready") });

        app.MapAuthEndpoints();
        app.MapUsersEndpoints();
        app.MapRolesEndpoints();

        return app;
    }

    /// <summary>
    /// Applies pending EF Core migrations and runs the idempotent seeder when
    /// <c>Database:ApplyMigrations</c> is enabled. Intended for development and one-shot
    /// deployment jobs, not for simultaneously rolling fleets (see README).
    /// </summary>
    public static async Task InitializeDatabaseAsync(this WebApplication app)
    {
        ArgumentNullException.ThrowIfNull(app);

        var databaseOptions = app.Services.GetRequiredService<IOptions<DatabaseOptions>>().Value;
        if (!databaseOptions.ApplyMigrations)
        {
            return;
        }

        await using var scope = app.Services.CreateAsyncScope();
        var dbContext = scope.ServiceProvider.GetRequiredService<AppDbContext>();
        await dbContext.Database.MigrateAsync(CancellationToken.None);

        var seeder = scope.ServiceProvider.GetRequiredService<DatabaseSeeder>();
        await seeder.SeedAsync(CancellationToken.None);

        var logger = scope.ServiceProvider.GetRequiredService<ILoggerFactory>().CreateLogger("Template.Api.Startup");
        ApiLog.DatabaseInitialized(logger);
    }
}
