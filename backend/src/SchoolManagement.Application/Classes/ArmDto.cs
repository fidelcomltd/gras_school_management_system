using SchoolManagement.Domain.Classes;

namespace SchoolManagement.Application.Classes;

/// <summary>
/// The wire shape of an <see cref="Arm"/> (spec 6.4.3, 6.4.9). One shape for the list item and the
/// detail read — roster, subjects in effect, result-set states and the transfer log (spec 6.4.5) are
/// out of scope until pupils, enrolments, subject mappings and results exist.
/// </summary>
/// <param name="Id">Opaque identifier.</param>
/// <param name="ClassLevelId">Opaque identifier of the owning level — supply this back on a write.</param>
/// <param name="ClassLevel">The level's CURRENT name, denormalised for display, same convention as <c>LevelDto.Section</c>.</param>
/// <param name="SessionId">Opaque identifier of the owning session.</param>
/// <param name="Label">1..16 characters, as stored (already normalised on save).</param>
/// <param name="DisplayName">Composed by <see cref="ArmDisplayName"/> at read time — never stored (spec 6.4.3).</param>
/// <param name="Capacity">1..100, a soft limit (spec 6.4.6).</param>
/// <param name="FormTeacherAdminId"><see langword="null"/> when unassigned.</param>
/// <param name="Status">active, inactive or closed. The client tolerates an unknown member (§8).</param>
public sealed record ArmDto(
    string Id,
    string ClassLevelId,
    string ClassLevel,
    string SessionId,
    string Label,
    string DisplayName,
    int Capacity,
    string? FormTeacherAdminId,
    ArmStatus Status);
