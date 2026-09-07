using System.Globalization;
using SchoolManagement.Domain.Sessions;

namespace SchoolManagement.Application.Sessions;

/// <summary>Maps <see cref="AcademicSession"/>/<see cref="Term"/> to their wire shapes.</summary>
internal static class SessionMapper
{
    /// <summary>Projects a term to its wire shape.</summary>
    public static TermDto ToTermDto(Term term)
    {
        ArgumentNullException.ThrowIfNull(term);

        return new TermDto(
            term.Id.ToString("D", CultureInfo.InvariantCulture),
            term.SessionId.ToString("D", CultureInfo.InvariantCulture),
            term.Ordinal,
            term.Name,
            term.StartDate,
            term.EndDate,
            term.TimesSchoolOpened,
            term.NextResumptionDate,
            term.State,
            term.ClosedAtUtc,
            term.ClosedBy);
    }

    /// <summary>Projects a session (list item shape) to its wire shape.</summary>
    public static SessionDto ToDto(AcademicSession session)
    {
        ArgumentNullException.ThrowIfNull(session);

        return new SessionDto(
            session.Id.ToString("D", CultureInfo.InvariantCulture),
            session.Name,
            session.StartDate,
            session.EndDate,
            session.State);
    }

    /// <summary>Projects a session and its terms to the detail wire shape.</summary>
    public static SessionDetailDto ToDetailDto(AcademicSession session, IReadOnlyList<Term> terms)
    {
        ArgumentNullException.ThrowIfNull(session);
        ArgumentNullException.ThrowIfNull(terms);

        return new SessionDetailDto(
            session.Id.ToString("D", CultureInfo.InvariantCulture),
            session.Name,
            session.StartDate,
            session.EndDate,
            session.State,
            terms.OrderBy(term => term.Ordinal).Select(ToTermDto).ToArray());
    }
}
