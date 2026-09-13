using EBI.Template.Application.Common;
using Microsoft.EntityFrameworkCore;

namespace EBI.Template.API.Extensions;

public static class PaginationExtensions
{
    public static async Task<PagedResponse<T>> PaginateAsync<T>(
        this IQueryable<T> query,
        int pageNumber,
        int pageSize) where T : class
    {
        var totalCount = await query.CountAsync();
        var items = await query
            .Skip((pageNumber - 1) * pageSize)
            .Take(pageSize)
            .AsNoTracking()
            .ToListAsync();

        return new PagedResponse<T>
        {
            Items = items,
            TotalCount = totalCount,
            PageNumber = pageNumber,
            PageSize = pageSize
        };
    }
}
