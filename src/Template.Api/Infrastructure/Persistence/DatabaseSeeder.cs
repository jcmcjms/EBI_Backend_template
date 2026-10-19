using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Options;
using Template.Api.Features.Roles.Domain;
using Template.Api.Features.Users.Domain;
using Template.Api.Infrastructure.Authorization;
using Template.Api.Infrastructure.Observability;
using Template.Api.Infrastructure.Security;

namespace Template.Api.Infrastructure.Persistence;

/// <summary>
/// Idempotent startup seeding:
/// (1) the system "Admin" role holding the full permission catalog, kept in sync with code on every run;
/// (2) an optional admin user when Seed:AdminEmail and Seed:AdminPassword are both configured.
/// Concurrent instances are serialized by unique indexes; a single lost race is retried as a no-op.
/// </summary>
public sealed class DatabaseSeeder(
    AppDbContext dbContext,
    IOptions<SeedOptions> seedOptions,
    PasswordHasher passwordHasher,
    TimeProvider timeProvider,
    ILogger<DatabaseSeeder> logger)
{
    public const string AdminRoleName = "Admin";

    public async Task SeedAsync(CancellationToken cancellationToken)
    {
        var now = timeProvider.GetUtcNow();
        await EnsureAdminRoleAsync(now, cancellationToken);
        await EnsureAdminUserAsync(now, cancellationToken);
    }

    private async Task EnsureAdminRoleAsync(DateTimeOffset now, CancellationToken cancellationToken)
    {
        var adminRole = await dbContext.Roles
            .Include(role => role.PermissionGrants)
            .FirstOrDefaultAsync(role => role.Name == AdminRoleName, cancellationToken);

        if (adminRole is null)
        {
            dbContext.Roles.Add(Role.Create(AdminRoleName, Permissions.All, now, isSystem: true));
            try
            {
                await dbContext.SaveChangesAsync(cancellationToken);
                ApiLog.SeededSystemRole(logger, AdminRoleName, Permissions.All.Count);
            }
            catch (DbUpdateException)
            {
                // Another instance won the startup race and created the role; continue with a clean tracker.
                dbContext.ChangeTracker.Clear();
                if (!await dbContext.Roles.AnyAsync(role => role.Name == AdminRoleName, cancellationToken))
                {
                    throw;
                }
            }

            return;
        }

        if (!adminRole.IsSystem)
        {
            // A user-created role took the reserved name before seeding ran; do not hijack it.
            return;
        }

        var current = adminRole.PermissionGrants
            .Select(grant => grant.Permission)
            .ToHashSet(StringComparer.Ordinal);

        var missing = Permissions.All.Where(permission => !current.Contains(permission)).ToList();
        var stale = adminRole.PermissionGrants.Where(grant => !Permissions.IsKnown(grant.Permission)).ToList();

        if (missing.Count == 0 && stale.Count == 0)
        {
            return;
        }

        foreach (var grant in stale)
        {
            dbContext.PermissionGrants.Remove(grant);
        }

        foreach (var permission in missing)
        {
            dbContext.PermissionGrants.Add(PermissionGrant.Create(adminRole.Id, permission, now));
        }

        await dbContext.SaveChangesAsync(cancellationToken);
        ApiLog.UpdatedSystemRolePermissions(logger, AdminRoleName);
    }

    private async Task EnsureAdminUserAsync(DateTimeOffset now, CancellationToken cancellationToken)
    {
        var seed = seedOptions.Value;
        if (string.IsNullOrWhiteSpace(seed.AdminEmail) || string.IsNullOrWhiteSpace(seed.AdminPassword))
        {
            return;
        }

        var email = User.NormalizeEmail(seed.AdminEmail);
        if (await dbContext.Users.AnyAsync(user => user.Email == email, cancellationToken))
        {
            return;
        }

        var adminRole = await dbContext.Roles.FirstAsync(role => role.Name == AdminRoleName, cancellationToken);

        // Bootstrap profile values: the username defaults to the email local part when usable, so
        // the seeded admin can log in with either identifier. Names are fixed placeholders that
        // administrators can change via PUT /api/users/{id}/profile.
        var username = DeriveUsername(seed.AdminUsername, email);
        var adminUser = User.Create(email, username, "Admin", "Admin", passwordHasher.Hash(seed.AdminPassword), now);
        dbContext.Users.Add(adminUser);
        dbContext.UserRoles.Add(UserRole.Assign(adminUser.Id, adminRole.Id, now));

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
            ApiLog.SeededAdminUser(logger, email);
        }
        catch (DbUpdateException)
        {
            dbContext.ChangeTracker.Clear();
            if (!await dbContext.Users.AnyAsync(user => user.Email == email, cancellationToken))
            {
                // A unique-index conflict here means the derived username collides with an existing
                // account: fail loudly at startup instead of silently creating a half-configured admin.
                throw;
            }
        }
    }

    private static string DeriveUsername(string configuredUsername, string normalizedEmail)
    {
        if (!string.IsNullOrWhiteSpace(configuredUsername))
        {
            return configuredUsername;
        }

        var localPart = normalizedEmail.Split('@')[0];
        return User.IsValidUsername(localPart) ? localPart : "admin";
    }
}
