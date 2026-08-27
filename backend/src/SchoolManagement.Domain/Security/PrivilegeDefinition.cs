namespace SchoolManagement.Domain.Security;

/// <summary>
/// One row of the privilege register (spec 4.4): a privilege's stable code and whether it may be
/// granted over a list of arms rather than school-wide.
/// </summary>
/// <param name="Code">
/// The immutable, dot-separated privilege string, for example <c>result.score.enter</c>. This is
/// API surface — clients and stored role/assignment data reference it by value.
/// </param>
/// <param name="Scopable">
/// Set by the code, never by an admin (spec 4.2). When <see langword="false"/>, the privilege may
/// only ever be granted school-wide.
/// </param>
public sealed record PrivilegeDefinition(string Code, bool Scopable);
