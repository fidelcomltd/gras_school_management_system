namespace SchoolManagement.Application.Common.Pagination;

/// <summary>
/// The cursor-pagination response envelope (spec 9.5). <see cref="NextCursor"/> is opaque to the
/// client — it must be echoed back verbatim as the next request's cursor, and never parsed or
/// constructed by hand.
/// </summary>
/// <typeparam name="TItem">The item DTO. Never a domain entity.</typeparam>
/// <param name="Items">The page of items, newest first. Empty (never null) when there is nothing more to return.</param>
/// <param name="NextCursor"><see langword="null"/> when this is the last page.</param>
public sealed record CursorPage<TItem>(IReadOnlyList<TItem> Items, string? NextCursor);
