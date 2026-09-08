using SchoolManagement.Domain.Classes;

namespace SchoolManagement.Application.Classes;

/// <summary>
/// The wire shape of a <see cref="ClassLevel"/> (spec 6.4.2, 6.4.9). One shape for both the list item
/// and the detail read — spec 6.4.9's <c>GET /levels/{id}</c> adds no field beyond what the list
/// already carries (no arm counts: that is TASK-0039).
/// </summary>
/// <param name="Id">Opaque identifier.</param>
/// <param name="Name">2..40 characters.</param>
/// <param name="SectionId">Opaque identifier of the owning section — supply this back on a write.</param>
/// <param name="Section">
/// The section's CURRENT name, denormalised for display (spec 6.4.2's own field is a plain FK; this
/// is a read convenience so a level list does not force N+1 lookups against a two-row admin-editable
/// register). Cross the wire as a string (§8): admin-editable, open-ended, tolerate any value.
/// </param>
/// <param name="ProgressionOrder">1 upward, unique across active levels.</param>
/// <param name="NextLevelId"><see langword="null"/> only on the graduating level.</param>
/// <param name="IsEntryLevel">Derived, never stored (spec 6.4.2) — computed over the active set at read time.</param>
/// <param name="IsGraduatingLevel">Derived, never stored (spec 6.4.2) — computed over the active set at read time.</param>
/// <param name="Status">active or inactive. The client tolerates an unknown member (§8).</param>
public sealed record LevelDto(
    string Id,
    string Name,
    string SectionId,
    string Section,
    int ProgressionOrder,
    string? NextLevelId,
    bool IsEntryLevel,
    bool IsGraduatingLevel,
    LevelStatus Status);
