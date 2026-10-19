using Template.Api.Features.Users.Domain;
using Template.Api.Infrastructure.Authorization;

namespace Template.Api.Features.Roles.Domain;

/// <summary>
/// Named set of catalog permissions that can be assigned to users. System roles (seeded "Admin")
/// are immutable through the API; their permissions are kept in sync with the code catalog at startup.
/// </summary>
public sealed class Role
{
    public const int MaxNameLength = 64;

    public Guid Id { get; private set; }

    public string Name { get; private set; } = null!;

    public bool IsSystem { get; private set; }

    public DateTimeOffset CreatedAt { get; private set; }

    public List<PermissionGrant> PermissionGrants { get; } = [];

    public List<UserRole> UserRoles { get; } = [];

    private Role()
    {
    }

    public static Role Create(string name, IEnumerable<string> permissions, DateTimeOffset now, bool isSystem = false)
    {
        var role = new Role
        {
            Id = Guid.NewGuid(),
            Name = NormalizeName(name),
            IsSystem = isSystem,
            CreatedAt = now,
        };

        foreach (var permission in NormalizePermissions(permissions))
        {
            role.PermissionGrants.Add(PermissionGrant.Create(role.Id, permission, now));
        }

        return role;
    }

    public static string NormalizeName(string name)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(name);

        var normalized = name.Trim();
        if (normalized.Length > MaxNameLength)
        {
            throw new ArgumentException($"Role name must be at most {MaxNameLength} characters.", nameof(name));
        }

        return normalized;
    }

    public void Rename(string name) => Name = NormalizeName(name);

    /// <summary>Replaces the granted permission set. Every permission must be part of the code-defined catalog.</summary>
    public void SetPermissions(IEnumerable<string> permissions, DateTimeOffset now)
    {
        PermissionGrants.Clear();
        foreach (var permission in NormalizePermissions(permissions))
        {
            PermissionGrants.Add(PermissionGrant.Create(Id, permission, now));
        }
    }

    private static List<string> NormalizePermissions(IEnumerable<string> permissions)
    {
        ArgumentNullException.ThrowIfNull(permissions);

        var distinct = permissions.Distinct(StringComparer.Ordinal).ToList();
        var unknown = Permissions.FindUnknown(distinct);
        if (unknown.Count > 0)
        {
            throw new ArgumentException($"Unknown permissions: {string.Join(", ", unknown)}.", nameof(permissions));
        }

        return distinct;
    }
}
