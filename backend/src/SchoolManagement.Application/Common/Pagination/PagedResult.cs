namespace SchoolManagement.Application.Common.Pagination;

/// <summary>
/// The one pagination response envelope for the whole API. Consistency here is what lets the
/// frontend write a single generic paging hook instead of one per endpoint.
/// </summary>
/// <typeparam name="TItem">The item DTO. Never a domain entity.</typeparam>
/// <param name="Items">The page of items. Empty (never null) when the page is past the end.</param>
/// <param name="Page">The 1-based page number that produced this response.</param>
/// <param name="PageSize">The page size that produced this response, after clamping.</param>
/// <param name="TotalCount">
/// Total matching rows across all pages. <see cref="long"/> because a table can exceed
/// <see cref="int"/> rows, and discovering that in production is not the moment to find out.
/// </param>
public sealed record PagedResult<TItem>(
    IReadOnlyList<TItem> Items,
    int Page,
    int PageSize,
    long TotalCount)
{
    /// <summary>Total number of pages available at this page size. Zero when there are no rows.</summary>
    public int TotalPages => PageSize <= 0
        ? 0
        : (int)Math.Ceiling(TotalCount / (double)PageSize);

    /// <summary>Whether a page after this one exists.</summary>
    public bool HasNextPage => Page < TotalPages;

    /// <summary>Whether a page before this one exists.</summary>
    public bool HasPreviousPage => Page > PageRequest.FirstPage;
}
