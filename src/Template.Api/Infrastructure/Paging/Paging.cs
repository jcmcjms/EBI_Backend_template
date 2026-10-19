using System.ComponentModel.DataAnnotations;

namespace Template.Api.Infrastructure.Paging;

/// <summary>Bounds for offset pagination on admin list endpoints.</summary>
public static class PagingLimits
{
    public const int DefaultPageSize = 25;
    public const int MaxPageSize = 100;
}

/// <summary>
/// Offset pagination request for admin list endpoints. Offset (rather than keyset) pagination is
/// deliberate here: administration screens need arbitrary page navigation and the listed tables are
/// small, indexed, and queried with a deterministic order. Properties are nullable because query
/// binding may leave them unset; defaults are applied in code so they survive any binder behavior.
/// </summary>
public record PagedRequest
{
    [Range(1, int.MaxValue)]
    public int? Page { get; set; }

    [Range(1, PagingLimits.MaxPageSize)]
    public int? PageSize { get; set; }

    public int EffectivePage => Page ?? 1;

    /// <summary>Effective page size clamped to the transport bound (defense in depth behind request validation).</summary>
    public int EffectivePageSize => Math.Clamp(PageSize ?? PagingLimits.DefaultPageSize, 1, PagingLimits.MaxPageSize);

    /// <summary>
    /// Row offset as a 64-bit value so hostile page numbers cannot overflow. Returns false when the
    /// requested page lies beyond any representable offset.
    /// </summary>
    public bool TryGetOffset(out long offset)
    {
        offset = (long)(EffectivePage - 1) * EffectivePageSize;
        return offset <= int.MaxValue;
    }
}

/// <summary>Standard envelope for paginated list responses.</summary>
/// <typeparam name="T">Item contract of the list.</typeparam>
public sealed record PagedResponse<T>(IReadOnlyList<T> Items, int TotalCount, int Page, int PageSize);
