using SchoolManagement.Application.Common.Pagination;
using SchoolManagement.Application.Pupils;
using SchoolManagement.Domain.Pupils;

namespace SchoolManagement.Application.Abstractions.Pupils;

/// <summary>Persistence port for <see cref="Pupil"/>.</summary>
/// <remarks>
/// Pupil-scale, unlike <c>IArmRepository</c>'s "load everything, filter in memory" — reads go through
/// cursor-paged, database-side queries, the same shape <c>IAdminAccountRepository.ListAsync</c>
/// established.
/// </remarks>
public interface IPupilRepository
{
    /// <summary>Adds a new pupil. No <c>SaveChangesAsync</c> — the unit-of-work behaviour commits.</summary>
    Task AddAsync(Pupil pupil, CancellationToken cancellationToken);

    /// <summary>
    /// Loads a TRACKED pupil by id, for a command that will mutate it. Includes a
    /// <see cref="PupilStatus.Pending"/> record — direct-id access is never subject to the
    /// pending-exclusion invariant, only list/report surfaces are (spec 6.5.14 names lists and
    /// reports, not "the record itself").
    /// </summary>
    Task<Pupil?> FindTrackedByIdAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>
    /// As <see cref="FindTrackedByIdAsync"/>, after row-locking the pupil (<c>FOR UPDATE</c>) for the rest of the
    /// transaction, so two status changes or transfers of the same pupil run one after the other and the second reads
    /// the first's outcome (spec 6.5.14).
    /// </summary>
    Task<Pupil?> FindTrackedByIdForUpdateAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>Loads a read-only pupil by id, for a query. Same pending-inclusion rule as <see cref="FindTrackedByIdAsync"/>.</summary>
    Task<Pupil?> FindReadOnlyByIdAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>Whether a (non-deleted) pupil has this id: one indexed probe, for authorisation's scope resolution.</summary>
    Task<bool> ExistsAsync(Guid id, CancellationToken cancellationToken);

    /// <summary>
    /// Cursor-paged, filtered list (spec 6.5.15). Excludes <see cref="PupilStatus.Pending"/> UNLESS
    /// <paramref name="status"/> is explicitly <see cref="PupilStatus.Pending"/> — the structural
    /// default the pending-exclusion invariant requires, applied once here rather than by every
    /// caller.
    /// </summary>
    /// <param name="status"><see langword="null"/> for "every non-pending status."</param>
    /// <param name="search">Matches surname/first/middle name or the registration number, case-insensitively; <see langword="null"/> for none.</param>
    /// <param name="cursor">The opaque <c>nextCursor</c> from a previous page, or <see langword="null"/> for the first page.</param>
    /// <param name="pageSize">Already clamped by the caller.</param>
    /// <param name="asOfDate">"Today", for each row's derived age.</param>
    /// <param name="allowedArmIds">
    /// <see langword="null"/> for no restriction (a school-wide caller). Non-null and non-empty
    /// restricts the result to pupils whose OPEN enrolment (spec 02 §5.2) names one of these arms —
    /// TASK-0059's real arm-scoped <c>pupil.view</c> (<c>PupilAccessGuard</c>'s remarks). The caller
    /// never passes an empty, non-null collection — it returns an empty page itself in that case.
    /// </param>
    /// <param name="cancellationToken">Propagated to the underlying query.</param>
    Task<CursorPage<PupilDto>> ListAsync(
        PupilStatus? status,
        string? search,
        string? cursor,
        int pageSize,
        DateOnly asOfDate,
        IReadOnlyCollection<Guid>? allowedArmIds,
        CancellationToken cancellationToken);

    /// <summary>
    /// The admissions queue (spec 6.5.14, 6.5.17): every <see cref="PupilStatus.Pending"/> record,
    /// unconditionally — the ONE opt-out of the default exclusion that ignores it outright rather
    /// than requiring <c>status=pending</c> on the query string.
    /// </summary>
    Task<CursorPage<PupilDto>> ListAdmissionsQueueAsync(
        string? cursor,
        int pageSize,
        DateOnly asOfDate,
        CancellationToken cancellationToken);

    /// <summary>
    /// Duplicate candidates for admission step 1 (spec 6.5.11): matches by surname AND first name AND
    /// date of birth. INCLUDES pending records deliberately — the whole point is catching a second,
    /// in-progress admission for the same child, which the ordinary list would otherwise hide.
    /// Contact-phone matching (spec 6.5.11's other half) is the next card's — <c>pupil_contact</c>
    /// does not exist yet.
    /// </summary>
    /// <param name="surname">Exact match, case-insensitive.</param>
    /// <param name="firstName">Exact match, case-insensitive.</param>
    /// <param name="dateOfBirth">Exact match.</param>
    /// <param name="maxResults">A small cap — this is a candidates panel, not a paged list.</param>
    /// <param name="asOfDate">"Today", for each row's derived age.</param>
    /// <param name="cancellationToken">Propagated to the underlying query.</param>
    Task<IReadOnlyList<PupilDto>> FindDuplicatesAsync(
        string surname,
        string firstName,
        DateOnly dateOfBirth,
        int maxResults,
        DateOnly asOfDate,
        CancellationToken cancellationToken);

    /// <summary>
    /// How many pupils, in ANY status, currently hold a non-null <c>registration_number</c> beginning
    /// with <paramref name="abbreviationPrefix"/> — spec 6.2.4's confirmation-dialogue count
    /// (<c>abbreviation.issuedCount</c>, TASK-0051). Reads the pupil table, never the counter: a
    /// declined admission never had a number to count, and a corrected number (TASK-0063, out of this
    /// card's scope) is still the pupil's CURRENT number, so neither can inflate this beyond what is
    /// genuinely issued and outstanding. Status-agnostic on purpose — a transferred, withdrawn or
    /// graduated pupil keeps their number, so <see cref="PupilStatus.Pending"/> aside (which can never
    /// match: <see cref="Pupil.RegistrationNumber"/> is null until approval), every status counts.
    /// </summary>
    /// <param name="abbreviationPrefix">The abbreviation to match at the start of the string, verbatim (case-sensitive).</param>
    /// <param name="cancellationToken">The request's cancellation token.</param>
    Task<int> CountByRegistrationNumberPrefixAsync(string abbreviationPrefix, CancellationToken cancellationToken);

    /// <summary>
    /// Whether ANY pupil other than <paramref name="excludingPupilId"/> currently holds
    /// <paramref name="registrationNumber"/> as their live <c>registration_number</c> — half of
    /// TASK-0063's two-table uniqueness check (spec 6.5.10 correction: "must be unique against both
    /// <c>pupil.registration_number</c> and <c>pupil_reg_number_history</c>"). <paramref
    /// name="excludingPupilId"/> is the pupil being corrected: resubmitting that pupil's OWN current
    /// number would otherwise spuriously collide with itself, the same "exclude self" shape
    /// <c>IArmRepository.LabelExistsAsync</c> already uses for its own update-time uniqueness check.
    /// Status-agnostic, like <see cref="CountByRegistrationNumberPrefixAsync"/> — a transferred,
    /// withdrawn or graduated pupil still holds their number and still blocks a collision.
    /// </summary>
    Task<bool> ExistsByRegistrationNumberAsync(
        string registrationNumber, Guid excludingPupilId, CancellationToken cancellationToken);

    /// <summary>
    /// Every ACTIVE pupil with an open enrolment in an arm of <paramref name="sessionId"/>, read-only, with that arm: the
    /// incomplete-records report's population (spec 6.5.12).
    /// </summary>
    Task<IReadOnlyList<(Pupil Pupil, Guid ArmId)>> ListActiveEnrolledInSessionAsync(Guid sessionId, CancellationToken cancellationToken);

    /// <summary>
    /// Which of <paramref name="registrationNumbers"/> a pupil in ANY status already holds: the issuer's pre-check, so a
    /// clash re-draws one serial instead of retrying a whole batch.
    /// </summary>
    Task<IReadOnlyList<string>> ListTakenRegistrationNumbersAsync(
        IReadOnlyCollection<string> registrationNumbers, CancellationToken cancellationToken);

    /// <summary>
    /// Every pupil, in ANY status, born on one of <paramref name="datesOfBirth"/>: bulk import's register-duplicate check
    /// (spec 6.5.13) in one query, matched on name in memory by the caller.
    /// </summary>
    Task<IReadOnlyList<PupilRegisterEntry>> ListByDatesOfBirthAsync(
        IReadOnlyCollection<DateOnly> datesOfBirth, CancellationToken cancellationToken);
}

/// <summary>The fields bulk import's register-duplicate check needs.</summary>
/// <param name="Id">The pupil.</param>
/// <param name="Surname">As stored.</param>
/// <param name="FirstName">As stored.</param>
/// <param name="DateOfBirth">As stored.</param>
/// <param name="RegistrationNumber">Null while pending.</param>
/// <param name="Status">Its status.</param>
public sealed record PupilRegisterEntry(
    Guid Id, string Surname, string FirstName, DateOnly DateOfBirth, string? RegistrationNumber, PupilStatus Status);
