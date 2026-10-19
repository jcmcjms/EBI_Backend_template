using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Template.Api.Infrastructure.Health;
using Template.Api.Infrastructure.Persistence;

namespace Template.Api.Composition;

/// <summary>
/// Registers persistence concerns: the EF Core <see cref="AppDbContext"/>, database options,
/// the SQL Server readiness health check, and the startup seeder.
/// </summary>
public static class PersistenceComposition
{
    public static IServiceCollection AddPersistence(this IServiceCollection services, IConfiguration configuration)
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);

        var connectionString = configuration.GetConnectionString(DatabaseOptions.ConnectionStringName);
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            throw new InvalidOperationException(
                $"Missing required connection string 'ConnectionStrings:{DatabaseOptions.ConnectionStringName}'. " +
                "Configure it via environment variables, user secrets, or a platform secret store.");
        }

        var databaseOptions = configuration.GetSection(DatabaseOptions.SectionName).Get<DatabaseOptions>() ?? new DatabaseOptions();
        services.AddSingleton(Options.Create(databaseOptions));

        services.AddDbContext<AppDbContext>(options =>
            options.UseSqlServer(connectionString, sql =>
            {
                // Transient-failure resilience for SQL Server; command timeout bounds every database call.
                sql.EnableRetryOnFailure(maxRetryCount: 3);
                sql.CommandTimeout(DatabaseOptions.CommandTimeoutSeconds);
            }));

        services.AddScoped<DatabaseSeeder>();

        services.AddHealthChecks()
            .AddCheck<SqlServerReadinessCheck>("sql-server", tags: ["ready"]);

        return services;
    }
}
