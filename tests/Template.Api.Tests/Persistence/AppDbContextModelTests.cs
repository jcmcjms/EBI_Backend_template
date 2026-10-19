using Microsoft.EntityFrameworkCore;
using Template.Api.Features.Auth.Domain;
using Template.Api.Features.Roles.Domain;
using Template.Api.Features.Users.Domain;
using Template.Api.Infrastructure.Persistence;

namespace Template.Api.Tests.Persistence;

/// <summary>
/// Validates the EF Core model against the SQL Server provider's metadata rules without connecting
/// to a database. This catches mapping defects (keys, indexes, relationships, required properties)
/// at unit-test speed. It is not a substitute for the SQL Server integration tests, which exercise
/// real query translation, constraints, and transactions.
/// </summary>
public class AppDbContextModelTests
{
    private static AppDbContext CreateContext()
    {
        var options = new DbContextOptionsBuilder<AppDbContext>()
            .UseSqlServer("Server=127.0.0.1,14330;Database=model-validation-only;User Id=unused;Password=unused;TrustServerCertificate=True")
            .Options;
        return new AppDbContext(options);
    }

    [Fact]
    public void Model_MapsAllEntitiesToExpectedTables()
    {
        using var context = CreateContext();

        // Touching the model triggers full provider-specific validation.
        var model = context.Model;

        Assert.Equal("Users", model.FindEntityType(typeof(User))?.GetTableName());
        Assert.Equal("Roles", model.FindEntityType(typeof(Role))?.GetTableName());
        Assert.Equal("UserRoles", model.FindEntityType(typeof(UserRole))?.GetTableName());
        Assert.Equal("RolePermissions", model.FindEntityType(typeof(PermissionGrant))?.GetTableName());
        Assert.Equal("RefreshTokens", model.FindEntityType(typeof(RefreshToken))?.GetTableName());
    }

    [Fact]
    public void Model_EnforcesUniquenessWhereRequired()
    {
        using var context = CreateContext();
        var model = context.Model;

        var userIndexes = model.FindEntityType(typeof(User))!.GetIndexes().ToList();

        var userEmailIndex = userIndexes.Single(index => index.Properties.Single().Name == "Email");
        Assert.True(userEmailIndex.IsUnique);
        Assert.Null(userEmailIndex.GetFilter());

        // Expand-safe uniqueness for the login handle: nullable column, filtered unique index.
        var usernameIndex = userIndexes.Single(index => index.Properties.Single().Name == "Username");
        Assert.True(usernameIndex.IsUnique);
        Assert.Equal("[Username] IS NOT NULL", usernameIndex.GetFilter());

        var roleNameIndex = model.FindEntityType(typeof(Role))!.GetIndexes().Single();
        Assert.Equal("Name", roleNameIndex.Properties.Single().Name);
        Assert.True(roleNameIndex.IsUnique);

        var refreshTokenIndex = model.FindEntityType(typeof(RefreshToken))!
            .GetIndexes().Single(index => index.Properties.Count == 1);
        Assert.Equal("TokenHash", refreshTokenIndex.Properties.Single().Name);
        Assert.True(refreshTokenIndex.IsUnique);
    }

    [Fact]
    public void Model_UsesCompositeKeysForJoinEntities()
    {
        using var context = CreateContext();
        var model = context.Model;

        Assert.Equal(
            ["UserId", "RoleId"],
            [.. model.FindEntityType(typeof(UserRole))!.FindPrimaryKey()!.Properties.Select(property => property.Name)]);
        Assert.Equal(
            ["RoleId", "Permission"],
            [.. model.FindEntityType(typeof(PermissionGrant))!.FindPrimaryKey()!.Properties.Select(property => property.Name)]);
    }

    [Fact]
    public void Model_RestrictsRoleDeletionWhileAssignmentsExist()
    {
        using var context = CreateContext();
        var model = context.Model;

        var roleForeignKey = model.FindEntityType(typeof(UserRole))!
            .GetForeignKeys().Single(foreignKey => foreignKey.PrincipalEntityType.ClrType == typeof(Role));

        Assert.Equal(DeleteBehavior.Restrict, roleForeignKey.DeleteBehavior);
    }
}
