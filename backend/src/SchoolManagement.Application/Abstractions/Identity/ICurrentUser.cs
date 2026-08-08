namespace SchoolManagement.Application.Abstractions.Identity;

/// <summary>
/// The caller on whose behalf the current request is executing. Implemented in the Api layer over
/// <c>HttpContext.User</c>.
/// </summary>
/// <remarks>
/// <para>
/// This abstraction exists so the Application and Infrastructure layers can record "who did this"
/// without taking a dependency on ASP.NET Core, and so tests can act as any caller by substituting
/// a stub rather than constructing an <c>HttpContext</c>.
/// </para>
/// <para>
/// IT IS FOR AUDIT AND ATTRIBUTION, NOT FOR AUTHORISATION. Never write
/// <c>if (currentUser.UserId == ...)</c> to decide whether an operation is permitted — permission
/// checks belong in an authorisation policy, where they are declarative and testable. Using this
/// for access control scatters security decisions through business logic, which is how they end up
/// inconsistent.
/// </para>
/// </remarks>
public interface ICurrentUser
{
    /// <summary>
    /// Stable identifier for the caller, or <c>null</c> for an anonymous or system-initiated action
    /// (a background job, a migration). Audit columns record <c>null</c> in that case rather than a
    /// fabricated "system" string, so "we do not know who" and "the system did it" stay
    /// distinguishable.
    /// </summary>
    string? UserId { get; }

    /// <summary>Whether the request carries an authenticated identity.</summary>
    bool IsAuthenticated { get; }
}
