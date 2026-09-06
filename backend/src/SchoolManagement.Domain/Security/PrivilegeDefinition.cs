namespace SchoolManagement.Domain.Security;

/// <summary>
/// One row of the privilege register (spec 4.4): a privilege's stable code, which of the
/// register's six groups it belongs to, its verbatim "Permits" sentence, and whether it may be
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
/// <param name="Module">Which of spec 4.4.1 through 4.4.6 this privilege belongs to.</param>
/// <param name="Permits">
/// Spec 4.4's "Permits" cell for this row, transcribed verbatim — not paraphrased, not
/// "improved". This is what <c>GET /api/v1/privileges</c> (TASK-0028) returns for the field of the
/// same name.
/// </param>
public sealed record PrivilegeDefinition(string Code, bool Scopable, PrivilegeModule Module, string Permits);
