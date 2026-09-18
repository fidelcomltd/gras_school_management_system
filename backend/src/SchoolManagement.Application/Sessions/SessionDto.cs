using SchoolManagement.Domain.Sessions;

namespace SchoolManagement.Application.Sessions;

/// <summary>
/// The wire shape of a term (spec 6.3.4), shared by every term-facing endpoint and nested inside
/// <see cref="SessionDetailDto"/>.
/// </summary>
/// <param name="Id">Opaque identifier.</param>
/// <param name="SessionId">The owning session's id.</param>
/// <param name="Ordinal">1, 2 or 3. Immutable.</param>
/// <param name="Name">Editable label; logic keys off <paramref name="Ordinal"/>, never this.</param>
/// <param name="StartDate">Inside the session's range.</param>
/// <param name="EndDate">Later than <paramref name="StartDate"/>.</param>
/// <param name="TimesSchoolOpened"><see langword="null"/> while blank; 1..200 once set; immutable once closed.</param>
/// <param name="NextResumptionDate"><see langword="null"/> until filled in.</param>
/// <param name="State">upcoming, active or closed. The client tolerates an unknown member (§8).</param>
/// <param name="ClosedAtUtc"><see langword="null"/> unless <paramref name="State"/> is closed.</param>
/// <param name="ClosedBy">The admin who closed it, or <see langword="null"/>.</param>
public sealed record TermDto(
    string Id,
    string SessionId,
    int Ordinal,
    string Name,
    DateOnly StartDate,
    DateOnly EndDate,
    int? TimesSchoolOpened,
    DateOnly? NextResumptionDate,
    TermState State,
    DateTimeOffset? ClosedAtUtc,
    string? ClosedBy);

/// <summary>
/// The list-item shape of a session (spec 6.3.8). <see cref="ArmCount"/> was added by TASK-0039, now
/// that <c>Arm</c> exists; enrolled-pupil and publication counts stay absent — spec 6.3.8 asks for
/// them too, but Pupil and result sets do not exist in this codebase yet, and this module ships
/// nothing it cannot populate honestly (never a fabricated <c>0</c>). See TASK-0035's Log for the
/// original deferral and TASK-0039's Log for the arm half landing.
/// </summary>
/// <param name="Id">Opaque identifier.</param>
/// <param name="Name"><c>YYYY/YYYY</c>.</param>
/// <param name="StartDate">Inside the first named year.</param>
/// <param name="EndDate">Inside the second named year.</param>
/// <param name="State">upcoming, active or closed. The client tolerates an unknown member (§8).</param>
/// <param name="ArmCount">The number of arms (any status) that exist for this session (spec 6.3.8).</param>
public sealed record SessionDto(
    string Id,
    string Name,
    DateOnly StartDate,
    DateOnly EndDate,
    SessionState State,
    int ArmCount);

/// <summary>
/// The detail shape of a session (spec 6.3.8, 6.3.10): its own fields plus its three terms.
/// <see cref="ArmCount"/> was added by TASK-0039; arms grouped by level, enrolment counts and the
/// publication position stay deferred for the same reason as <see cref="SessionDto"/> — the promotion
/// panel is all of TASK-0036.
/// </summary>
/// <param name="Id">Opaque identifier.</param>
/// <param name="Name"><c>YYYY/YYYY</c>.</param>
/// <param name="StartDate">Inside the first named year.</param>
/// <param name="EndDate">Inside the second named year.</param>
/// <param name="State">upcoming, active or closed. The client tolerates an unknown member (§8).</param>
/// <param name="Terms">Exactly three, ordered by ordinal — a session is never created without them.</param>
/// <param name="ArmCount">The number of arms (any status) that exist for this session (spec 6.3.8).</param>
public sealed record SessionDetailDto(
    string Id,
    string Name,
    DateOnly StartDate,
    DateOnly EndDate,
    SessionState State,
    IReadOnlyList<TermDto> Terms,
    int ArmCount);
