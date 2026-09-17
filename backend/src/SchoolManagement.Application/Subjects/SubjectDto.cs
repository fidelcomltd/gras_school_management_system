using SchoolManagement.Domain.Subjects;

namespace SchoolManagement.Application.Subjects;

/// <summary>The wire shape of a <see cref="Subject"/> (spec 6.6.2, 6.6.7, 6.6.9).</summary>
/// <param name="Id">Opaque identifier.</param>
/// <param name="Name">Unique, case-insensitive.</param>
/// <param name="Code">
/// <see langword="null"/> when not supplied (the seed's own state — TASK-0070 delta amendment 1).
/// </param>
/// <param name="Description">Free text, for the administrator's benefit only. Never printed.</param>
/// <param name="Status">active or inactive. The client tolerates an unknown member (§8).</param>
/// <param name="MappedLevelCount">
/// Number of levels this subject is actively mapped to in the requested term. <see langword="null"/>
/// when the request carried no <c>termId</c> — TASK-0070 delta amendment 3: this endpoint never
/// guesses a term.
/// </param>
/// <param name="ArmExceptionCount"><see langword="null"/> under the same condition as <paramref name="MappedLevelCount"/>.</param>
/// <param name="PupilsTakingCount"><see langword="null"/> under the same condition as <paramref name="MappedLevelCount"/>.</param>
public sealed record SubjectDto(
    string Id,
    string Name,
    string? Code,
    string? Description,
    SubjectStatus Status,
    int? MappedLevelCount,
    int? ArmExceptionCount,
    int? PupilsTakingCount);
