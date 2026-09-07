namespace SchoolManagement.Application.Common.Pagination;

/// <summary>
/// The cursor-pagination request shape (spec 9.5: "Cursor pagination on every list endpoint... Offset
/// pagination is not used, because a register that grows while an administrator pages through it
/// will skip rows"). <c>GET /config-versions</c> is the first endpoint to use this; copy it for any
/// future list endpoint rather than reaching for <see cref="PageRequest"/>, which is the OFFSET shape
/// the reference slice still uses and §9.5 forbids for real product endpoints.
/// </summary>
public static class CursorPageRequest
{
    /// <summary>Page size used when the client does not specify one.</summary>
    public const int DefaultPageSize = 25;

    /// <summary>Hard ceiling on page size. A request above this is rejected, never truncated silently.</summary>
    public const int MaxPageSize = 100;
}
