using FluentValidation;
using SchoolManagement.Application.Abstractions.Messaging;
using SchoolManagement.Domain.Common;

namespace SchoolManagement.Application.Results;

/// <summary>
/// One submitted row of <see cref="SaveAttendanceCommand"/> (TASK-0086 stage A). A pupil OMITTED
/// from <see cref="SaveAttendanceCommand.Rows"/> leaves that pupil's existing entry UNTOUCHED
/// (Q1-A's convention, generalised to a single-value field). A pupil present with an explicit
/// <see langword="null"/> <see cref="TimesPresent"/> CLEARS (deletes) the entry. A pupil present
/// with a number sets or replaces it.
/// </summary>
/// <param name="PupilId">Must be on the arm's active roster.</param>
/// <param name="TimesPresent"><see langword="null"/> to clear.</param>
public sealed record SaveAttendanceRowInput(string PupilId, int? TimesPresent);

/// <summary>
/// <c>PUT /api/v1/arms/{armId}/attendance</c> (TASK-0086 stage A) — partial-save sheet write, one
/// transaction. The first save for an arm/term creates the result set (Draft), same convention as
/// <c>SaveTraitRatingsCommand</c>.
/// </summary>
/// <param name="ArmId">The arm this sheet belongs to, from the route.</param>
/// <param name="TermId">The term this sheet is for.</param>
/// <param name="Version">
/// The sheet's version as last read, or <see langword="null"/> for a sheet with no entries yet. A
/// mismatch against the server's current version is a 409 <c>attendance.stale_version</c>.
/// </param>
/// <param name="Rows">Every row being saved. A pupil not present here is left entirely untouched.</param>
public sealed record SaveAttendanceCommand(
    string ArmId, string TermId, string? Version, IReadOnlyList<SaveAttendanceRowInput> Rows)
    : ICommand<Result<AttendanceSheetDto>>;

/// <summary>
/// Structural checks only — every data-dependent rule (spec §6.7.7: roster membership, the
/// times-school-opened upper bound) is the handler's job, same split <c>SaveTraitRatingsCommandValidator</c> uses.
/// </summary>
internal sealed class SaveAttendanceCommandValidator : AbstractValidator<SaveAttendanceCommand>
{
    public SaveAttendanceCommandValidator()
    {
        RuleFor(command => command.ArmId).NotEmpty().Must(value => Guid.TryParse(value, out _))
            .WithMessage("ArmId must be a valid identifier.");
        RuleFor(command => command.TermId).NotEmpty().Must(value => Guid.TryParse(value, out _))
            .WithMessage("TermId must be a valid identifier.");
        RuleFor(command => command.Rows).NotNull();

        RuleForEach(command => command.Rows).ChildRules(row =>
        {
            row.RuleFor(entry => entry.PupilId).NotEmpty().Must(value => Guid.TryParse(value, out _))
                .WithMessage("PupilId must be a valid identifier.");
        });
    }
}
