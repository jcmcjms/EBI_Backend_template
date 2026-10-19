using Microsoft.EntityFrameworkCore;
using Template.Api.Infrastructure.Paging;
using Template.Api.Infrastructure.Persistence;

namespace Template.Api.Features.Users;

/// <summary>Paged user list for administration screens: projection only, deterministic order, bounded page size.</summary>
public sealed class ListUsersUseCase(AppDbContext dbContext)
{
    public async Task<PagedResponse<UserResponse>> ExecuteAsync(PagedRequest paging, CancellationToken cancellationToken)
    {
        var pageSize = paging.EffectivePageSize;
        var totalCount = await dbContext.Users.CountAsync(cancellationToken);

        if (!paging.TryGetOffset(out var offset) || offset >= totalCount)
        {
            return new PagedResponse<UserResponse>([], totalCount, paging.EffectivePage, pageSize);
        }

        var users = await dbContext.Users
            .AsNoTracking()
            .OrderBy(user => user.CreatedAt)
            .ThenBy(user => user.Id)
            .Skip((int)offset)
            .Take(pageSize)
            .Select(user => new UserResponse(
                user.Id,
                user.Email,
                user.Username,
                user.FirstName,
                user.MiddleName,
                user.LastName,
                user.Suffix,
                user.JobTitle,
                user.IsDisabled,
                user.CreatedAt,
                user.DisabledAt,
                user.UserRoles
                    .Select(userRole => new RoleAssignmentResponse(userRole.Role.Id, userRole.Role.Name))
                    .OrderBy(role => role.Name)
                    .ToList()))
            .ToListAsync(cancellationToken);

        return new PagedResponse<UserResponse>(users, totalCount, paging.EffectivePage, pageSize);
    }
}
