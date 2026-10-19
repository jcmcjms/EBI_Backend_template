using Template.Api.Features.Roles.Domain;
using Template.Api.Infrastructure.Authorization;

namespace Template.Api.Tests.Domain;

public class RoleTests
{
    private static readonly DateTimeOffset Now = new(2026, 3, 1, 12, 0, 0, TimeSpan.Zero);

    [Fact]
    public void Create_NormalizesNameAndGrantsDistinctCatalogPermissions()
    {
        var role = Role.Create("  Auditors ", ["users.read", "users.read", "roles.read"], Now);

        Assert.Equal("Auditors", role.Name);
        Assert.False(role.IsSystem);
        Assert.Equal(Now, role.CreatedAt);
        Assert.Equal(
            ["roles.read", "users.read"],
            [.. role.PermissionGrants.Select(grant => grant.Permission).Order(StringComparer.Ordinal)]);
        Assert.All(role.PermissionGrants, grant =>
        {
            Assert.Equal(role.Id, grant.RoleId);
            Assert.Equal(Now, grant.GrantedAt);
        });
    }

    [Fact]
    public void Create_RejectsPermissionsOutsideTheCatalog()
    {
        var exception = Assert.Throws<ArgumentException>(
            () => Role.Create("Auditors", ["users.read", "users.destroy"], Now));

        Assert.Contains("users.destroy", exception.Message, StringComparison.Ordinal);
    }

    [Fact]
    public void Create_SupportsSystemFlag()
    {
        var role = Role.Create("Admin", Permissions.All, Now, isSystem: true);

        Assert.True(role.IsSystem);
        Assert.Equal(Permissions.All.Count, role.PermissionGrants.Count);
    }

    [Fact]
    public void SetPermissions_ReplacesTheWholeGrantSet()
    {
        var role = Role.Create("Auditors", ["users.read", "roles.read"], Now);

        role.SetPermissions(["roles.manage"], Now.AddMinutes(1));

        Assert.Equal(["roles.manage"], [.. role.PermissionGrants.Select(grant => grant.Permission)]);
        Assert.All(role.PermissionGrants, grant => Assert.Equal(Now.AddMinutes(1), grant.GrantedAt));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void NormalizeName_RejectsBlankNames(string? name)
    {
        Assert.ThrowsAny<ArgumentException>(() => Role.NormalizeName(name!));
    }

    [Fact]
    public void NormalizeName_RejectsOverlongNames()
    {
        Assert.Throws<ArgumentException>(() => Role.NormalizeName(new string('x', Role.MaxNameLength + 1)));
    }
}
