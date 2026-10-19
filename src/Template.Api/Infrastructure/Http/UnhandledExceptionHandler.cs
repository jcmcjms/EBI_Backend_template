using Microsoft.AspNetCore.Diagnostics;
using Template.Api.Infrastructure.Observability;

namespace Template.Api.Infrastructure.Http;

/// <summary>
/// Last-resort handler for unexpected exceptions: logs with correlation id and returns a generic
/// 500 Problem Details response. Internal details (stack traces, SQL, connection strings) never reach clients.
/// </summary>
public sealed class UnhandledExceptionHandler(ILogger<UnhandledExceptionHandler> logger) : IExceptionHandler
{
    public async ValueTask<bool> TryHandleAsync(HttpContext httpContext, Exception exception, CancellationToken cancellationToken)
    {
        ApiLog.UnhandledRequestFailure(logger, httpContext.TraceIdentifier, exception);

        if (httpContext.Response.HasStarted)
        {
            return false;
        }

        await ProblemDetailsResponses.WriteAsync(
            httpContext,
            StatusCodes.Status500InternalServerError,
            "Internal server error",
            "An unexpected error occurred while processing the request.",
            cancellationToken);
        return true;
    }
}
