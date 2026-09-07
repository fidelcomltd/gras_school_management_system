using System.Globalization;
using System.Text.RegularExpressions;
using SchoolManagement.Domain.Common;

namespace SchoolManagement.Domain.Sessions;

/// <summary>
/// A school year and the anchor every later module hangs off (spec 6.3.1, 6.3.3): an arm belongs to
/// a session, a mark belongs to a term, a result set belongs to an arm and a term.
/// </summary>
/// <remarks>
/// <para>
/// Promotion (spec 6.3.7, <c>promotion_batch_id</c>) and the end-of-session archive
/// (<c>archived_at</c>, spec 9.8) are TASK-0036, blocked on entities that do not exist yet (arms,
/// pupils, enrolments, annual results) — this type carries neither column. Adding one later is an
/// additive migration; carrying an always-null column now would be speculative.
/// </para>
/// <para>
/// <see cref="State"/> is largely a DERIVED field, not a free-standing status an endpoint sets
/// directly (spec 6.3.5): it starts <see cref="SessionState.Upcoming"/>, becomes
/// <see cref="SessionState.Active"/> when this session's First Term opens, and becomes
/// <see cref="SessionState.Closed"/> only as a SIDE EFFECT of the NEXT session's First Term opening —
/// see <see cref="Activate"/>/<see cref="Close"/> and the term-open handler that calls them.
/// </para>
/// </remarks>
public sealed partial class AcademicSession : Entity<Guid>, IAuditableEntity
{
    /// <summary>Spec 6.3.3: <c>name</c> is <c>String 9</c> — exactly <c>YYYY/YYYY</c>.</summary>
    public const int NameLength = 9;

    private AcademicSession(Guid id, string name, DateOnly startDate, DateOnly endDate)
        : base(id)
    {
        Name = name;
        StartDate = startDate;
        EndDate = endDate;
        State = SessionState.Upcoming;
    }

    // EF Core materialisation constructor.
    private AcademicSession()
        : base() => Name = null!;

    /// <summary><c>YYYY/YYYY</c>, the second year exactly the first plus one (spec 6.3.3). Unique.</summary>
    public string Name { get; private set; }

    /// <summary>Must fall inside the first named year; earlier than <see cref="EndDate"/> (spec 6.3.3).</summary>
    public DateOnly StartDate { get; private set; }

    /// <summary>Must fall inside the second named year (spec 6.3.3).</summary>
    public DateOnly EndDate { get; private set; }

    /// <summary>See the type remarks — this is a derived field, not a directly-set status.</summary>
    public SessionState State { get; private set; }

    /// <inheritdoc />
    public DateTimeOffset CreatedAtUtc { get; set; }

    /// <inheritdoc />
    public string? CreatedBy { get; set; }

    /// <inheritdoc />
    public DateTimeOffset? ModifiedAtUtc { get; set; }

    /// <inheritdoc />
    public string? ModifiedBy { get; set; }

    /// <summary>
    /// Creates a new session in state <see cref="SessionState.Upcoming"/> (spec 6.3.5). Sibling terms
    /// are a separate concern — <c>CreateSessionCommandHandler</c> creates all three, in the same
    /// transaction, immediately after this succeeds; there is no route that creates a session alone.
    /// </summary>
    /// <param name="id">The new session's identifier.</param>
    /// <param name="name">Format <c>YYYY/YYYY</c>; the second year must be exactly the first plus one.</param>
    /// <param name="startDate">Must fall inside the first named year, earlier than <paramref name="endDate"/>.</param>
    /// <param name="endDate">Must fall inside the second named year.</param>
    public static Result<AcademicSession> Create(Guid id, string name, DateOnly startDate, DateOnly endDate)
    {
        if (id == Guid.Empty)
        {
            return Result.Failure<AcademicSession>(Error.Validation("session.id_required", "Id must not be empty."));
        }

        if (!TryValidate(name, startDate, endDate, out var error))
        {
            return Result.Failure<AcademicSession>(error);
        }

        return Result.Success(new AcademicSession(id, name, startDate, endDate));
    }

    /// <summary>
    /// Edits name and dates (spec 6.3.10). The caller must already have rejected this on a
    /// <see cref="SessionState.Closed"/> session (409) and checked name uniqueness/overlap against
    /// other sessions — both need a repository lookup this entity cannot perform.
    /// </summary>
    public Result Reschedule(string name, DateOnly startDate, DateOnly endDate)
    {
        if (!TryValidate(name, startDate, endDate, out var error))
        {
            return Result.Failure(error);
        }

        Name = name;
        StartDate = startDate;
        EndDate = endDate;
        return Result.Success();
    }

    /// <summary>Moves to <see cref="SessionState.Active"/> — called when this session's First Term opens.</summary>
    public void Activate() => State = SessionState.Active;

    /// <summary>
    /// Moves to <see cref="SessionState.Closed"/> — called on the PREVIOUSLY active session as a side
    /// effect of a successor session's First Term opening (spec 6.3.5), never directly by an endpoint.
    /// </summary>
    public void Close() => State = SessionState.Closed;

    private static bool TryValidate(string name, DateOnly startDate, DateOnly endDate, out Error error)
    {
        ArgumentNullException.ThrowIfNull(name);

        var match = NamePattern().Match(name);

        if (!match.Success)
        {
            error = Error.Validation(
                "session.name_invalid_format",
                $"Name must be in the format YYYY/YYYY, for example '2026/2027'.");
            return false;
        }

        var firstYear = int.Parse(match.Groups[1].Value, CultureInfo.InvariantCulture);
        var secondYear = int.Parse(match.Groups[2].Value, CultureInfo.InvariantCulture);

        if (secondYear != firstYear + 1)
        {
            error = Error.Validation(
                "session.name_years_not_consecutive",
                $"A session runs across two calendar years. {name} is not valid.");
            return false;
        }

        if (startDate.Year != firstYear)
        {
            error = Error.Validation(
                "session.start_date_wrong_year",
                $"Start date must fall inside {firstYear}.");
            return false;
        }

        if (endDate.Year != secondYear)
        {
            error = Error.Validation(
                "session.end_date_wrong_year",
                $"End date must fall inside {secondYear}.");
            return false;
        }

        // No separate "start before end" check: once both year memberships above hold, start falls
        // somewhere in `firstYear` and end somewhere in `secondYear` (`= firstYear + 1`), and every
        // date in one calendar year precedes every date in the next — spec 6.3.3's "must be earlier
        // than end_date" is therefore guaranteed by construction, not a reachable failure branch.
        error = Error.None;
        return true;
    }

    [GeneratedRegex(@"^(\d{4})/(\d{4})$")]
    private static partial Regex NamePattern();
}
