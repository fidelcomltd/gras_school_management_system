using FluentValidation;
using SchoolManagement.Application.Abstractions.Messaging;
using SchoolManagement.Domain.Common;

namespace SchoolManagement.Application.Security.PrivilegeRegister;

/// <summary>
/// Reads the privilege register grouped by module (spec 4.4), for use when building or reviewing a
/// role. Approved contract delta: <c>GET /api/v1/privileges</c>
/// (<c>.agent/decisions/2026-Q3-contract-deltas.md</c>, entry <c>TASK-0028</c>, section 1).
/// </summary>
/// <remarks>
/// Authenticated only — no privilege is required (spec 6.1.14 states this explicitly: every
/// signed-in admin, whatever role they hold, needs to see the full menu of privileges to
/// understand what a role can be built from). NOT PAGED:
/// <see cref="Domain.Security.PrivilegeRegistry.All"/> is a fixed, compile-time, 93-row constant,
/// not a growing list, so spec 9.5's cursor-pagination rule for collection endpoints does not
/// apply here — do not "fix" this into a cursor page.
/// </remarks>
public sealed record GetPrivilegeRegisterQuery : IQuery<Result<PrivilegeRegisterResponse>>;

/// <summary>Validates <see cref="GetPrivilegeRegisterQuery"/>. Empty — the query carries no input.</summary>
internal sealed class GetPrivilegeRegisterQueryValidator : AbstractValidator<GetPrivilegeRegisterQuery>;

/// <summary>The privilege register, grouped 4.4.1 through 4.4.6 in spec table order.</summary>
/// <param name="Groups">One entry per module group, in spec section order.</param>
public sealed record PrivilegeRegisterResponse(IReadOnlyList<PrivilegeGroupDto> Groups);

/// <summary>One module group of the privilege register (one of spec 4.4.1 through 4.4.6).</summary>
/// <param name="Key">
/// The group's stable key: <c>administration</c>, <c>settings</c>, <c>academic_structure</c>,
/// <c>pupils_and_subjects</c>, <c>results</c> or <c>pins_and_reports</c>. Crosses the wire as a
/// plain string (root CLAUDE.md §8) — a client must tolerate a key it does not recognise, since a
/// seventh group is an additive change.
/// </param>
/// <param name="Title">Verbatim spec 4.4.x section heading, for example "Academic structure".</param>
/// <param name="Privileges">The group's privileges, in spec table order.</param>
public sealed record PrivilegeGroupDto(string Key, string Title, IReadOnlyList<PrivilegeDescriptorDto> Privileges);

/// <summary>One row of the privilege register (spec 4.4).</summary>
/// <param name="Code">
/// The canonical privilege code, for example <c>result.score.enter</c>. Never a legacy
/// <c>guardian.*</c> alias — the register is canonical codes only.
/// </param>
/// <param name="Permits">Verbatim spec 4.4 "Permits" cell for this row.</param>
/// <param name="Scopable">Whether the privilege may be granted over a list of arms rather than school-wide.</param>
public sealed record PrivilegeDescriptorDto(string Code, string Permits, bool Scopable);
