using SchoolManagement.Domain.Common;

namespace SchoolManagement.Domain.Sessions;

/// <summary>
/// One of the three terms of an <see cref="AcademicSession"/> (spec 6.3.1, 6.3.4). A mark belongs to
/// a term; a result set belongs to an arm and a term.
/// </summary>
public sealed class Term : Entity<Guid>, IAuditableEntity
{
    /// <summary>Spec 6.3.4: <c>name</c> is <c>String 20</c>.</summary>
    public const int NameMaxLength = 20;

    /// <summary>Spec 6.3.4: <c>times_school_opened</c> lower bound.</summary>
    public const int MinTimesSchoolOpened = 1;

    /// <summary>Spec 6.3.4: <c>times_school_opened</c> upper bound.</summary>
    public const int MaxTimesSchoolOpened = 200;

    /// <summary>Spec 6.3.6: reopening a closed term requires a reason of at least this many characters.</summary>
    public const int ReopenReasonMinLength = 10;

    private Term(
        Guid id,
        Guid sessionId,
        int ordinal,
        string name,
        DateOnly startDate,
        DateOnly endDate,
        DateOnly? nextResumptionDate)
        : base(id)
    {
        SessionId = sessionId;
        Ordinal = ordinal;
        Name = name;
        StartDate = startDate;
        EndDate = endDate;
        NextResumptionDate = nextResumptionDate;
        State = TermState.Upcoming;
    }

    // EF Core materialisation constructor.
    private Term()
        : base() => Name = null!;

    /// <summary>The session this term belongs to. Set once, at creation.</summary>
    public Guid SessionId { get; private set; }

    /// <summary>1, 2 or 3. Immutable — every rule keys off this, never <see cref="Name"/> (spec 6.3.4).</summary>
    public int Ordinal { get; private set; }

    /// <summary>Editable label, defaults to "First/Second/Third Term" (spec 6.3.4).</summary>
    public string Name { get; private set; }

    /// <summary>Inside the session's range; later than the previous term's end date (spec 6.3.4).</summary>
    public DateOnly StartDate { get; private set; }

    /// <summary>Later than <see cref="StartDate"/>; earlier than the next term's start date (spec 6.3.4).</summary>
    public DateOnly EndDate { get; private set; }

    /// <summary>
    /// 1..200. May be blank while <see cref="TermState.Upcoming"/> or <see cref="TermState.Active"/>,
    /// required to close, immutable once <see cref="TermState.Closed"/> (spec 6.3.4, 6.3.6).
    /// </summary>
    public int? TimesSchoolOpened { get; private set; }

    /// <summary>The date the following term begins. Required to publish a result set (spec 6.3.4) — not enforced here; no publish endpoint exists yet.</summary>
    public DateOnly? NextResumptionDate { get; private set; }

    /// <summary>Only one term system-wide may be <see cref="TermState.Active"/> (spec 6.3.9).</summary>
    public TermState State { get; private set; }

    /// <summary>Written on close (spec 6.3.4). Cleared by <see cref="Reopen"/>.</summary>
    public DateTimeOffset? ClosedAtUtc { get; private set; }

    /// <summary>Written on close (spec 6.3.4). Cleared by <see cref="Reopen"/>.</summary>
    public string? ClosedBy { get; private set; }

    /// <inheritdoc />
    public DateTimeOffset CreatedAtUtc { get; set; }

    /// <inheritdoc />
    public string? CreatedBy { get; set; }

    /// <inheritdoc />
    public DateTimeOffset? ModifiedAtUtc { get; set; }

    /// <inheritdoc />
    public string? ModifiedBy { get; set; }

    /// <summary>
    /// Creates a term in state <see cref="TermState.Upcoming"/>, <see cref="TimesSchoolOpened"/> blank
    /// (spec 6.3.5: "times school opened may be left blank and filled in later").
    /// </summary>
    public static Result<Term> Create(
        Guid id,
        Guid sessionId,
        int ordinal,
        string name,
        DateOnly startDate,
        DateOnly endDate,
        DateOnly? nextResumptionDate = null)
    {
        if (id == Guid.Empty)
        {
            return Result.Failure<Term>(Error.Validation("term.id_required", "Id must not be empty."));
        }

        if (sessionId == Guid.Empty)
        {
            return Result.Failure<Term>(Error.Validation("term.session_id_required", "SessionId must not be empty."));
        }

        if (ordinal is < 1 or > 3)
        {
            return Result.Failure<Term>(Error.Validation("term.ordinal_invalid", "Ordinal must be 1, 2 or 3."));
        }

        if (!TryNormalizeName(name, out var trimmedName, out var nameError))
        {
            return Result.Failure<Term>(nameError);
        }

        if (!TryValidateDates(trimmedName, startDate, endDate, out var datesError))
        {
            return Result.Failure<Term>(datesError);
        }

        return Result.Success(new Term(id, sessionId, ordinal, trimmedName, startDate, endDate, nextResumptionDate));
    }

    /// <summary>
    /// Edits label, dates and next-resumption date (spec 6.3.10). Chronology against the session and
    /// sibling terms is the caller's job — it needs repository lookups this entity cannot perform.
    /// Reachable regardless of <see cref="State"/>: spec 6.3.6 calls out only
    /// <see cref="TimesSchoolOpened"/> as immutable once closed (see <see cref="SetTimesSchoolOpened"/>).
    /// </summary>
    public Result UpdateSchedule(string name, DateOnly startDate, DateOnly endDate, DateOnly? nextResumptionDate)
    {
        if (!TryNormalizeName(name, out var trimmedName, out var nameError))
        {
            return Result.Failure(nameError);
        }

        if (!TryValidateDates(trimmedName, startDate, endDate, out var datesError))
        {
            return Result.Failure(datesError);
        }

        Name = trimmedName;
        StartDate = startDate;
        EndDate = endDate;
        NextResumptionDate = nextResumptionDate;
        return Result.Success();
    }

    /// <summary>
    /// Sets <see cref="TimesSchoolOpened"/> (spec 6.3.4, 6.3.9). Rejected once
    /// <see cref="TermState.Closed"/> — "the term's times_school_opened becomes immutable, because it
    /// is printed on results already in parents' hands" (spec 6.3.6).
    /// </summary>
    public Result SetTimesSchoolOpened(int timesSchoolOpened)
    {
        if (State == TermState.Closed)
        {
            return Result.Failure(Error.Conflict(
                "term.times_school_opened_immutable",
                $"{Name} is closed. Times school opened is printed on results already issued and " +
                "cannot be changed."));
        }

        if (timesSchoolOpened is < MinTimesSchoolOpened or > MaxTimesSchoolOpened)
        {
            return Result.Failure(Error.Validation(
                "term.times_school_opened_out_of_range",
                $"Times school opened must be between {MinTimesSchoolOpened} and {MaxTimesSchoolOpened}."));
        }

        TimesSchoolOpened = timesSchoolOpened;
        return Result.Success();
    }

    /// <summary>
    /// Moves <see cref="TermState.Upcoming"/> to <see cref="TermState.Active"/>. Cross-term
    /// preconditions (spec 6.3.6: the previous term closed, at least one arm exists) are the caller's
    /// job — see <c>TermTransitionGuard.CanOpen</c>.
    /// </summary>
    public Result Open()
    {
        if (State != TermState.Upcoming)
        {
            return Result.Failure(Error.Conflict(
                "term.open_invalid_state",
                $"{Name} is {State} and cannot be opened."));
        }

        State = TermState.Active;
        return Result.Success();
    }

    /// <summary>
    /// Moves <see cref="TermState.Active"/> to <see cref="TermState.Closed"/>, writing
    /// <see cref="ClosedAtUtc"/>/<see cref="ClosedBy"/> (spec 6.3.4, 6.3.9). The result-set precondition
    /// (spec 6.3.6: blocked by Draft/Awaiting Approval/Approved sets) is NOT checked here — result
    /// sets are spec 09 §6.7 and do not exist in this codebase yet; see the calling handler's remarks.
    /// </summary>
    public Result Close(DateTimeOffset closedAtUtc, string? closedBy)
    {
        if (State != TermState.Active)
        {
            return Result.Failure(Error.Conflict(
                "term.close_invalid_state",
                $"{Name} is {State} and cannot be closed."));
        }

        if (TimesSchoolOpened is null)
        {
            return Result.Failure(Error.Validation(
                "term.times_school_opened_required_to_close",
                $"Enter the number of times school opened for {Name} before closing it. This number " +
                "is printed on every result sheet."));
        }

        State = TermState.Closed;
        ClosedAtUtc = closedAtUtc;
        ClosedBy = closedBy;
        return Result.Success();
    }

    /// <summary>
    /// Moves <see cref="TermState.Closed"/> back to <see cref="TermState.Active"/> and clears the
    /// close bookkeeping (spec 6.3.6). Super-admin gating, the reason and the "following term already
    /// opened" refusal are the caller's job — see <c>TermTransitionGuard.CanReopen</c>.
    /// </summary>
    public Result Reopen()
    {
        if (State != TermState.Closed)
        {
            return Result.Failure(Error.Conflict(
                "term.reopen_invalid_state",
                $"Only a closed term can be reopened. {Name} is {State}."));
        }

        State = TermState.Active;
        ClosedAtUtc = null;
        ClosedBy = null;
        return Result.Success();
    }

    /// <summary>
    /// Guard for score-entry-shaped writes (spec 6.3.6: "score entry endpoints return 409 for a
    /// closed term... the trait, attendance and remark endpoints do the same"). No such endpoint
    /// exists in this codebase yet — spec 06/09's marks, traits, attendance and remarks modules have
    /// no task card yet — so nothing calls this today. It is the seam those future handlers must call
    /// before writing, proven directly by <c>TermTests</c> in the meantime.
    /// </summary>
    public Result EnsureAcceptsEntry()
    {
        if (State == TermState.Closed)
        {
            return Result.Failure(Error.Conflict(
                "term.closed_for_entry",
                $"{Name} is closed. Marks, traits, attendance and remarks cannot be entered or edited."));
        }

        return Result.Success();
    }

    private static bool TryNormalizeName(string name, out string normalized, out Error error)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            normalized = string.Empty;
            error = Error.Validation("term.name_required", "Name must not be empty.");
            return false;
        }

        var trimmed = name.Trim();

        if (trimmed.Length > NameMaxLength)
        {
            normalized = string.Empty;
            error = Error.Validation("term.name_too_long", $"Name must be at most {NameMaxLength} characters.");
            return false;
        }

        normalized = trimmed;
        error = Error.None;
        return true;
    }

    private static bool TryValidateDates(string name, DateOnly startDate, DateOnly endDate, out Error error)
    {
        if (startDate >= endDate)
        {
            error = Error.Validation(
                "term.dates_out_of_order",
                $"{name}'s start date must be earlier than its end date.");
            return false;
        }

        error = Error.None;
        return true;
    }
}
