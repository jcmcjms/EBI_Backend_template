using Template.Api.Features.Roles.Domain;

namespace Template.Api.Features.Users.Domain;

/// <summary>Assignment of a role to a user. Owned by the Users feature, which manages assignments.</summary>
public sealed class UserRole
{
    public Guid UserId { get; private set; }

    public Guid RoleId { get; private set; }

    public DateTimeOffset AssignedAt { get; private set; }

    public Role Role { get; private set; } = null!;

    private UserRole()
    {
    }

    public static UserRole Assign(Guid userId, Guid roleId, DateTimeOffset now) =>
        new()
        {
            UserId = userId,
            RoleId = roleId,
            AssignedAt = now,
        };
}
