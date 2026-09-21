using FluentValidation;
using SchoolManagement.Application.Abstractions.Messaging;
using SchoolManagement.Domain.Common;

namespace SchoolManagement.Application.Results;

/// <summary>
/// <c>PUT /api/v1/arms/{armId}/class-teacher-remarks</c> (TASK-0086 stage A) — partial-save sheet
/// write, one transaction. The first remark of either kind for an arm/term creates the result set
/// (Draft), same convention as <c>SaveTraitRatingsCommand</c>.
/// </summary>
/// <param name="ArmId">The arm this sheet belongs to, from the route.</param>
/// <param name="TermId">The term this sheet is for.</param>
/// <param name="Version">
/// The sheet's version as last read, or <see langword="null"/> for a sheet with no class-teacher
/// remarks yet. A mismatch against the server's current version is a 409
/// <c>class_teacher_remarks.stale_version</c>.
/// </param>
/// <param name="Rows">Every row being saved. A pupil not present here is left entirely untouched.</param>
public sealed record SaveClassTeacherRemarksCommand(
    string ArmId, string TermId, string? Version, IReadOnlyList<SaveRemarkRowInput> Rows)
    : ICommand<Result<RemarkSheetDto>>;

/// <summary>Structural checks only — every data-dependent rule is the handler's job.</summary>
internal sealed class SaveClassTeacherRemarksCommandValidator : AbstractValidator<SaveClassTeacherRemarksCommand>
{
    public SaveClassTeacherRemarksCommandValidator()
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
