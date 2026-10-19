namespace Template.Api.Infrastructure.Http;

/// <summary>
/// Thrown when an endpoint receives an outcome type its mapping does not handle. Reaching this at
/// runtime is a mapping defect (a new outcome was added without updating the endpoint); it surfaces
/// as a logged 500 rather than a silently wrong response.
/// </summary>
public sealed class UnreachableOutcomeException(object? outcome)
    : InvalidOperationException($"Unhandled outcome type '{outcome?.GetType().FullName ?? "null"}' in endpoint mapping.");
