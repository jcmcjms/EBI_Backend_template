namespace Template.Api.Infrastructure.Authorization;

/// <summary>
/// Closed catalog of application permissions. Endpoints require catalog values via
/// <c>RequirePermission(...)</c> (validated at startup) and roles may only grant catalog values,
/// so runtime permission checks never encounter unknown strings. Add new permissions here;
/// the seeded system role picks them up at the next startup.
/// </summary>
public static class Permissions
{
    public const string RolesManage = "roles.manage";
    public const string RolesRead = "roles.read";
    public const string UsersManage = "users.manage";
    public const string UsersRead = "users.read";
    public const string UsersWrite = "users.write";

    public static readonly IReadOnlyList<string> All =
    [
        RolesManage,
        RolesRead,
        UsersManage,
        UsersRead,
        UsersWrite,
    ];

    public static bool IsKnown(string permission) =>
        !string.IsNullOrEmpty(permission) && All.Contains(permission, StringComparer.Ordinal);

    /// <summary>Returns the distinct candidates that are not part of the catalog.</summary>
    public static IReadOnlyList<string> FindUnknown(IEnumerable<string> candidates)
    {
        ArgumentNullException.ThrowIfNull(candidates);
        return [.. candidates.Where(candidate => !IsKnown(candidate)).Distinct(StringComparer.Ordinal)];
    }
}
