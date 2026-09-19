using SchoolManagement.Domain.Results;

namespace SchoolManagement.Application.Results;

/// <summary>One saved phrase (TASK-0086 stage B; spec §6.7.7 delta item 4).</summary>
/// <param name="Id">The template's id.</param>
/// <param name="Kind">Which list this phrase belongs to.</param>
/// <param name="Text">The phrase, trimmed.</param>
/// <param name="CreatedAt">When it was added.</param>
public sealed record RemarkTemplateDto(string Id, RemarkKind Kind, string Text, DateTimeOffset CreatedAt);

/// <summary>
/// <c>GET /api/v1/remark-templates?kind=</c>'s response — one kind's list, in creation order, never
/// paginated (admin-configuration-sized, same treatment as <c>SectionListResponse</c>). Ships empty.
/// </summary>
/// <param name="Templates">Every template of the requested kind, in creation order.</param>
public sealed record RemarkTemplateListDto(IReadOnlyList<RemarkTemplateDto> Templates);
