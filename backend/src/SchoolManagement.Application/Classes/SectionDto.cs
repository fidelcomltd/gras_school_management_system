namespace SchoolManagement.Application.Classes;

/// <summary>The wire shape of a <see cref="Domain.Classes.Section"/> (spec 6.4.2, 6.4.9).</summary>
/// <param name="Id">Opaque identifier.</param>
/// <param name="Name">2..40 characters.</param>
/// <param name="RatesTraits">
/// Whether an arm of this section rates traits (TASK-0083 ruling R1). Additive property — every
/// existing caller that ignores it keeps working.
/// </param>
public sealed record SectionDto(string Id, string Name, bool RatesTraits);

/// <summary>
/// <c>GET /api/v1/sections</c>'s response (spec 6.4.9): "Not paged — a two-row seeded list a school
/// extends rarely," so this is a plain wrapped list, not a <see cref="Common.Pagination.CursorPage{TItem}"/> —
/// the same shape <c>PrivilegeRegisterResponse</c> uses for the other fixed, small register in this API.
/// </summary>
/// <param name="Sections">Every section, ordered by name.</param>
public sealed record SectionListResponse(IReadOnlyList<SectionDto> Sections);
