using SchoolManagement.Domain.Common;

namespace SchoolManagement.Domain.Sessions;

/// <summary>
/// Pure date-sequencing rules that span more than one <see cref="Term"/> or its
/// <see cref="AcademicSession"/> (spec 6.3.4) — used both when a session's three terms are created
/// together and when a single term is edited afterwards, so the two flows cannot drift apart.
/// </summary>
public static class TermChronologyGuard
{
    /// <summary>Spec 6.3.4: a term's dates "must fall inside the session's range."</summary>
    public static Result ValidateWithinSession(
        string sessionName,
        DateOnly sessionStart,
        DateOnly sessionEnd,
        string termName,
        DateOnly termStart,
        DateOnly termEnd)
    {
        if (termStart < sessionStart || termEnd > sessionEnd)
        {
            return Result.Failure(Error.Validation(
                "term.outside_session_range",
                $"{termName}'s dates must fall within {sessionName} " +
                $"({sessionStart:dd/MM/yyyy} to {sessionEnd:dd/MM/yyyy})."));
        }

        return Result.Success();
    }

    /// <summary>
    /// Spec 6.3.4: a term's start date must be "later than the previous term's end_date"; spec 6.3.9's
    /// example: "Second Term starts on 05/01/2027, before First Term ends on 18/12/2026 has passed.
    /// Terms cannot overlap."
    /// </summary>
    public static Result ValidateSequential(string previousName, DateOnly previousEnd, string nextName, DateOnly nextStart)
    {
        if (nextStart <= previousEnd)
        {
            return Result.Failure(Error.Validation(
                "term.overlap",
                $"{nextName} starts on {nextStart:dd/MM/yyyy}, before {previousName} ends on " +
                $"{previousEnd:dd/MM/yyyy} has passed. Terms cannot overlap."));
        }

        return Result.Success();
    }
}
