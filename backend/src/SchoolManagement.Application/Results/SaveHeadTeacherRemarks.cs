using FluentValidation;
using SchoolManagement.Application.Abstractions.Messaging;
using SchoolManagement.Domain.Common;
using SchoolManagement.Domain.Results;

namespace SchoolManagement.Application.Results;

/// <summary>
/// <c>PUT /api/v1/arms/{armId}/head-teacher-remarks</c> (TASK-0086 stage A) — partial-save sheet
/// write, one transaction, plus ruling H's fill-all action. The first remark of either kind for an
/// arm/term creates the result set (Draft), same convention as <c>SaveTraitRatingsCommand</c>.
/// </summary>
/// <param name="ArmId">The arm this sheet belongs to, from the route.</param>
/// <param name="TermId">The term this sheet is for.</param>
/// <param name="Version">
/// The sheet's version as last read, or <see langword="null"/> for a sheet with no head-teacher
/// remarks yet. A mismatch against the server's current version is a 409
/// <c>head_teacher_remarks.stale_version</c>.
/// </param>
/// <param name="Rows">Every row being saved. A pupil not present here is left entirely untouched. Rows apply BEFORE <paramref name="FillEmpty"/> (delta item 3).</param>
/// <param name="FillEmpty">
/// Optional fill-all text. Sets this text for every roster pupil who STILL has no remark after
/// <paramref name="Rows"/> is applied — never overwrites an existing remark. <see langword="null"/>
/// to skip the fill action. Validated like a remark (1-300 characters after trimming).
/// </param>
public sealed record SaveHeadTeacherRemarksCommand(
    string ArmId, string TermId, string? Version, IReadOnlyList<SaveRemarkRowInput> Rows, string? FillEmpty)
    : ICommand<Result<RemarkSheetDto>>;

/// <summary>Structural checks only — every data-dependent rule is the handler's job.</summary>
internal sealed class SaveHeadTeacherRemarksCommandValidator : AbstractValidator<SaveHeadTeacherRemarksCommand>
{
    public SaveHeadTeacherRemarksCommandValidator()
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

        RuleFor(command => command.FillEmpty)
            .MaximumLength(PupilRemark.TextMaxLength)
            .When(command => !string.IsNullOrWhiteSpace(command.FillEmpty))
            .WithMessage($"Remark must be {PupilRemark.TextMaxLength} characters or fewer.");
    }
}
