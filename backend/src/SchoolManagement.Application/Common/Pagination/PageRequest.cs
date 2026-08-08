namespace SchoolManagement.Application.Common.Pagination;

/// <summary>
/// The one pagination request shape for the whole API. Every collection endpoint takes this;
/// there are no unbounded list endpoints.
/// </summary>
/// <remarks>
/// Page size is guarded twice, on purpose:
/// <list type="bullet">
/// <item>the request validator REJECTS a page size above <see cref="MaxPageSize"/> with 422, so a
/// client is told plainly that it asked for too much rather than silently receiving less than it
/// requested and mis-paging as a result;</item>
/// <item><see cref="Clamp"/> is applied in the repository as a backstop, so even a code path that
/// somehow skipped validation cannot issue an unbounded query.</item>
/// </list>
/// </remarks>
public sealed record PageRequest
{
    /// <summary>Hard ceiling on page size. A request above this is rejected, never truncated silently.</summary>
    public const int MaxPageSize = 100;

    /// <summary>Page size used when the client does not specify one.</summary>
    public const int DefaultPageSize = 20;

    /// <summary>First page number. Pages are 1-based, not 0-based, because the query string is public API.</summary>
    public const int FirstPage = 1;

    /// <summary>1-based page number.</summary>
    public int Page { get; init; } = FirstPage;

    /// <summary>Number of items per page, at most <see cref="MaxPageSize"/>.</summary>
    public int PageSize { get; init; } = DefaultPageSize;

    /// <summary>Number of rows to skip. Derived; never sent by the client.</summary>
    public int Skip => (Page - FirstPage) * PageSize;

    /// <summary>
    /// Builds a request from optional query-string values, applying defaults.
    /// </summary>
    /// <param name="page">Requested page, or null for the first page.</param>
    /// <param name="pageSize">Requested page size, or null for <see cref="DefaultPageSize"/>.</param>
    public static PageRequest From(int? page, int? pageSize) => new()
    {
        Page = page ?? FirstPage,
        PageSize = pageSize ?? DefaultPageSize,
    };

    /// <summary>
    /// Returns a copy with <see cref="Page"/> and <see cref="PageSize"/> forced into legal range.
    /// The defensive backstop described in the type remarks.
    /// </summary>
    public PageRequest Clamp() => new()
    {
        Page = Page < FirstPage ? FirstPage : Page,
        PageSize = PageSize is < 1 ? DefaultPageSize : Math.Min(PageSize, MaxPageSize),
    };
}
