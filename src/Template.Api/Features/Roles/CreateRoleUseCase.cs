using System.ComponentModel.DataAnnotations;
using Microsoft.EntityFrameworkCore;
using Template.Api.Features.Roles.Domain;
using Template.Api.Infrastructure.Persistence;
using PermissionCatalog = Template.Api.Infrastructure.Authorization.Permissions;

namespace Template.Api.Features.Roles;

public sealed record SaveRoleRequest
{
    [Required]
    [StringLength(Role.MaxNameLength, MinimumLength = 1)]
    public required string Name { get; init; }

    /// <summary>Permissions granted to the role. Every value must be part of the code-defined catalog.</summary>
    [Required]
    public required IReadOnlyList<string> Permissions { get; init; }
}

public abstract record CreateRoleOutcome
{
    public sealed record Success(RoleSummaryResponse Role) : CreateRoleOutcome;

    public sealed record NameAlreadyExists : CreateRoleOutcome;

    public sealed record UnknownPermissions(IReadOnlyList<string> Permissions) : CreateRoleOutcome;
}

/// <summary>
/// Creates a custom role. The permission set is validated against the code catalog before anything
/// is written; name uniqueness is enforced race-safely by the unique index.
/// </summary>
public sealed class CreateRoleUseCase(AppDbContext dbContext, TimeProvider timeProvider)
{
    public async Task<CreateRoleOutcome> ExecuteAsync(SaveRoleRequest request, CancellationToken cancellationToken)
    {
        var unknown = PermissionCatalog.FindUnknown(request.Permissions);
        if (unknown.Count > 0)
        {
            return new CreateRoleOutcome.UnknownPermissions(unknown);
        }

        var now = timeProvider.GetUtcNow();
        var role = Role.Create(request.Name, request.Permissions, now);
        dbContext.Roles.Add(role);

        try
        {
            await dbContext.SaveChangesAsync(cancellationToken);
        }
        catch (DbUpdateException)
        {
            dbContext.ChangeTracker.Clear();
            var name = Role.NormalizeName(request.Name);
            if (await dbContext.Roles.AnyAsync(candidate => candidate.Name == name, cancellationToken))
            {
                return new CreateRoleOutcome.NameAlreadyExists();
            }

            throw;
        }

        return new CreateRoleOutcome.Success(new RoleSummaryResponse(
            role.Id,
            role.Name,
            role.IsSystem,
            AssignedUsersCount: 0,
            [.. role.PermissionGrants.Select(grant => grant.Permission).Order(StringComparer.Ordinal)]));
    }
}
