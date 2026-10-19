using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;

namespace Template.Api.Infrastructure.Http;

/// <summary>
/// Writes RFC 9457 Problem Details responses from pipeline components that run outside endpoint execution
/// (JWT bearer events, rate-limiter rejections). Inside endpoints, use <c>Results.Problem(...)</c>.
/// </summary>
public static class ProblemDetailsResponses
{
    public static async Task WriteAsync(
        HttpContext httpContext,
        int statusCode,
        string title,
        string? detail = null,
        CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(httpContext);

        // IProblemDetailsService writes only the body; the caller owns the status code.
        // (Authentication challenge handlers call HandleResponse(), which suppresses the
        // framework's default status assignment.)
        httpContext.Response.StatusCode = statusCode;

        var problemDetailsService = httpContext.RequestServices.GetService<IProblemDetailsService>();
        if (problemDetailsService is not null)
        {
            await problemDetailsService.WriteAsync(new ProblemDetailsContext
            {
                HttpContext = httpContext,
                ProblemDetails =
                {
                    Status = statusCode,
                    Title = title,
                    Detail = detail,
                },
            });
            return;
        }

        httpContext.Response.StatusCode = statusCode;
        await httpContext.Response.WriteAsJsonAsync(
            new ProblemDetails { Status = statusCode, Title = title, Detail = detail },
            options: null,
            contentType: "application/problem+json",
            cancellationToken);
    }
}
