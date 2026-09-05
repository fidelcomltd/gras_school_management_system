namespace SchoolManagement.Application.Abstractions.Identity;

/// <summary>
/// The session the current request authenticated against, if any. Sibling to <see cref="ICurrentUser"/>
/// — that abstraction identifies WHO is calling; this one identifies WHICH session, which sign-out,
/// refresh and password-change all need in order to act on (or exempt) the right
/// <c>AdminSession</c> row.
/// </summary>
public interface ICurrentSession
{
    /// <summary>
    /// The current session's id, or <c>null</c> when the request carries no valid session (anonymous,
    /// or an invalid/expired/revoked cookie on an endpoint that tolerates that — <c>sign-out</c>).
    /// </summary>
    Guid? SessionId { get; }
}
