using Microsoft.EntityFrameworkCore;
using Template.Api.Infrastructure.Paging;
using Template.Api.Infrastructure.Persistence;

namespace Template.Api.Features.Roles;

public sealed record RoleSummaryResponse(Guid Id, string Name, bool IsSystem, int AssignedUsersCount, IReadOnlyList<string> Permissions);

/// <summary>Paged role list for administration screens: projection only, deterministic order, bounded page size.</summary>
public sealed class ListRolesUseCase(AppDbContext dbContext)
{
    public async Task<PagedResponse<RoleSummaryResponse>> ExecuteAsync(PagedRequest paging, CancellationToken cancellationToken)
    {
        var pageSize = paging.EffectivePageSize;
        var totalCount = await dbContext.Roles.CountAsync(cancellationToken);

        if (!paging.TryGetOffset(out var offset) || offset >= totalCount)
        {
            return new PagedResponse<RoleSummaryResponse>([], totalCount, paging.EffectivePage, pageSize);
        }

        var roles = await dbContext.Roles
            .AsNoTracking()
            .OrderBy(role => role.Name)
            .ThenBy(role => role.Id)
            .Skip((int)offset)
            .Take(pageSize)
            .Select(role => new RoleSummaryResponse(
                role.Id,
                role.Name,
                role.IsSystem,
                role.UserRoles.Count,
                role.PermissionGrants.Select(grant => grant.Permission).OrderBy(permission => permission).ToList()))
            .ToListAsync(cancellationToken);

        return new PagedResponse<RoleSummaryResponse>(roles, totalCount, paging.EffectivePage, pageSize);
    }
}
