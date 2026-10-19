using Microsoft.EntityFrameworkCore;
using Template.Api.Features.Auth.Domain;
using Template.Api.Features.Auth.Persistence;
using Template.Api.Features.Roles.Domain;
using Template.Api.Features.Roles.Persistence;
using Template.Api.Features.Users.Domain;
using Template.Api.Features.Users.Persistence;

namespace Template.Api.Infrastructure.Persistence;

/// <summary>
/// Single EF Core context for the modular monolith. Entities live in the Persistence/Domain folders
/// of the feature that owns them and are registered here explicitly (no assembly scanning).
/// </summary>
public sealed class AppDbContext(DbContextOptions<AppDbContext> options) : DbContext(options)
{
    public DbSet<User> Users => Set<User>();

    public DbSet<Role> Roles => Set<Role>();

    public DbSet<UserRole> UserRoles => Set<UserRole>();

    public DbSet<PermissionGrant> PermissionGrants => Set<PermissionGrant>();

    public DbSet<RefreshToken> RefreshTokens => Set<RefreshToken>();

    protected override void OnModelCreating(ModelBuilder modelBuilder)
    {
        modelBuilder.ApplyConfiguration(new UserConfiguration());
        modelBuilder.ApplyConfiguration(new UserRoleConfiguration());
        modelBuilder.ApplyConfiguration(new RoleConfiguration());
        modelBuilder.ApplyConfiguration(new PermissionGrantConfiguration());
        modelBuilder.ApplyConfiguration(new RefreshTokenConfiguration());
    }
}
