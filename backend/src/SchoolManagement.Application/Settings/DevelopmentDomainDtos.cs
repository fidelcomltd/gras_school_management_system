using SchoolManagement.Domain.Settings;

namespace SchoolManagement.Application.Settings;

/// <summary>One indicator, both inside <see cref="DevelopmentDomainDto"/> and as an element of an ordered array (spec 6.2.13).</summary>
/// <param name="Id">Opaque id.</param>
/// <param name="Name">The printed row label, for example <c>Potty trained</c>.</param>
/// <param name="DisplayOrder">Printed order within the domain.</param>
/// <param name="Status">Active or archived. An archived indicator leaves new entry screens but stays on historical sheets.</param>
public sealed record DevelopmentIndicatorDto(string Id, string Name, int DisplayOrder, DevelopmentIndicatorStatus Status);

/// <summary>One domain, both inside <see cref="SettingsDto"/>'s envelope and as an element of <see cref="SettingsDevelopmentDomainGroupDto"/>'s <c>Domains</c> array (spec 6.2.13).</summary>
/// <param name="Id">Opaque id.</param>
/// <param name="SectionId">The owning section's opaque id.</param>
/// <param name="Section">The owning section's display name, for a client that does not want to join against the section list itself.</param>
/// <param name="Name">For example <c>Personal &amp; Physical Development</c>.</param>
/// <param name="DisplayOrder">Printed block order within the section.</param>
/// <param name="RatingScaleId">The scale this domain's indicators are rated against.</param>
/// <param name="AllowsIndicatorComment">Whether the entry screen prints a per-indicator Comments column for this domain.</param>
/// <param name="Status">Active or archived. An archived domain leaves new entry screens but stays on historical sheets.</param>
/// <param name="ActiveIndicatorCount">
/// The count the &gt;60 entry-screen warning reads (spec 6.2.13). Active indicators only, and always
/// <c>0</c> for an archived domain regardless of its indicators' own individual status — an archived
/// domain never appears on an entry screen, so nothing there is asking for a rating.
/// </param>
/// <param name="Indicators">Every indicator on this domain, ordered by <c>displayOrder</c>.</param>
public sealed record DevelopmentDomainDto(
    string Id,
    string SectionId,
    string Section,
    string Name,
    int DisplayOrder,
    string RatingScaleId,
    bool AllowsIndicatorComment,
    DevelopmentDomainStatus Status,
    int ActiveIndicatorCount,
    IReadOnlyList<DevelopmentIndicatorDto> Indicators);

/// <summary>
/// The development-domains group, both inside <see cref="SettingsDto"/> and as
/// <c>PUT /api/v1/settings/development-domains</c>'s own success body (spec 6.2.13).
/// </summary>
/// <param name="Domains">Every domain, ordered by section then <c>displayOrder</c>.</param>
/// <param name="VersionNumber">
/// The development-domains group's current optimistic-concurrency pointer. Echo this back as
/// <c>expectedVersion</c> on the next save.
/// </param>
public sealed record SettingsDevelopmentDomainGroupDto(IReadOnlyList<DevelopmentDomainDto> Domains, int VersionNumber);
