using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Diagnostics.HealthChecks;
using Template.Api.Infrastructure.Persistence;

namespace Template.Api.Infrastructure.Health;

/// <summary>
/// Readiness check verifying SQL Server connectivity within a bounded timeout, so a hung database
/// cannot stall orchestrator health probes.
/// </summary>
public sealed class SqlServerReadinessCheck(AppDbContext dbContext) : IHealthCheck
{
    private static readonly TimeSpan CheckTimeout = TimeSpan.FromSeconds(3);

    public async Task<HealthCheckResult> CheckHealthAsync(HealthCheckContext context, CancellationToken cancellationToken = default)
    {
        using var timeout = CancellationTokenSource.CreateLinkedTokenSource(cancellationToken);
        timeout.CancelAfter(CheckTimeout);

        try
        {
            var canConnect = await dbContext.Database.CanConnectAsync(timeout.Token);
            return canConnect
                ? HealthCheckResult.Healthy("SQL Server is reachable.")
                : HealthCheckResult.Unhealthy("SQL Server is not reachable.");
        }
        catch (OperationCanceledException) when (!cancellationToken.IsCancellationRequested)
        {
            return HealthCheckResult.Unhealthy("SQL Server health check timed out.");
        }
        catch (Exception exception)
        {
            return HealthCheckResult.Unhealthy("SQL Server health check failed.", exception);
        }
    }
}
