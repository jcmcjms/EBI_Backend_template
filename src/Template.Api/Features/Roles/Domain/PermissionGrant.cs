namespace Template.Api.Features.Roles.Domain;

/// <summary>A single catalog permission granted to a role (join entity, table "RolePermissions").</summary>
public sealed class PermissionGrant
{
    public const int MaxPermissionLength = 64;

    public Guid RoleId { get; private set; }

    public string Permission { get; private set; } = null!;

    public DateTimeOffset GrantedAt { get; private set; }

    public Role Role { get; private set; } = null!;

    private PermissionGrant()
    {
    }

    public static PermissionGrant Create(Guid roleId, string permission, DateTimeOffset now) =>
        new()
        {
            RoleId = roleId,
            Permission = permission,
            GrantedAt = now,
        };
}
