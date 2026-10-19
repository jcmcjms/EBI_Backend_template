namespace Template.Api.Infrastructure.Observability;

/// <summary>
/// Source-generated log messages for security-relevant and operational events.
/// Stable event ids and structured fields; no secrets or tokens are ever logged.
/// </summary>
internal static partial class ApiLog
{
    [LoggerMessage(EventId = 1000, Level = LogLevel.Error, Message = "Unhandled exception while processing request {TraceIdentifier}.")]
    public static partial void UnhandledRequestFailure(ILogger logger, string traceIdentifier, Exception exception);

    [LoggerMessage(EventId = 1001, Level = LogLevel.Warning, Message = "Login failed for identifier {Identifier}: {Reason}.")]
    public static partial void LoginFailed(ILogger logger, string identifier, string reason);

    [LoggerMessage(EventId = 1002, Level = LogLevel.Warning, Message = "Refresh token reuse detected for user {UserId}. All active sessions for this user were revoked.")]
    public static partial void RefreshTokenReuseDetected(ILogger logger, Guid userId);

    [LoggerMessage(EventId = 1003, Level = LogLevel.Information, Message = "Database initialized: migrations applied and seed completed.")]
    public static partial void DatabaseInitialized(ILogger logger);

    [LoggerMessage(EventId = 1004, Level = LogLevel.Information, Message = "Seeded admin user {Email}.")]
    public static partial void SeededAdminUser(ILogger logger, string email);

    [LoggerMessage(EventId = 1005, Level = LogLevel.Information, Message = "Seeded system role {RoleName} with {PermissionCount} permissions.")]
    public static partial void SeededSystemRole(ILogger logger, string roleName, int permissionCount);

    [LoggerMessage(EventId = 1006, Level = LogLevel.Information, Message = "Updated system role {RoleName} permissions to match the catalog.")]
    public static partial void UpdatedSystemRolePermissions(ILogger logger, string roleName);

    [LoggerMessage(EventId = 1007, Level = LogLevel.Warning, Message = "All active sessions for user {UserId} were revoked.")]
    public static partial void SessionsRevoked(ILogger logger, Guid userId);
}
