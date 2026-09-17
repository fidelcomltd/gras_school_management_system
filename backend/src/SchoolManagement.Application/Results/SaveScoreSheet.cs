using FluentValidation;
using SchoolManagement.Application.Abstractions.Messaging;
using SchoolManagement.Domain.Common;

namespace SchoolManagement.Application.Results;

/// <summary>One submitted row of <see cref="SaveScoreSheetCommand"/> (spec 6.7.4).</summary>
/// <param name="PupilId">Must be on the arm's active roster.</param>
/// <param name="ComponentMarks">
/// Every non-examination component id in the current assessment structure must be a key. A present
/// key with a <see langword="null"/> value is a blank cell — never an implicit zero. A missing key is
/// rejected (<c>component_missing</c>).
/// </param>
/// <param name="ExamMark"><see langword="null"/> for a blank cell, or when <paramref name="ExamAbsent"/> is true.</param>
/// <param name="ExamAbsent">True means the pupil did not sit the examination.</param>
public sealed record SaveScoreSheetRowInput(
    string PupilId, IReadOnlyDictionary<string, int?>? ComponentMarks, int? ExamMark, bool ExamAbsent);

/// <summary>
/// <c>PUT /api/v1/arms/{armId}/score-sheets</c> (spec 6.7.4; TASK-0076's approved contract delta) —
/// whole-sheet save, one transaction. The first save for an arm/term creates the result set (Draft).
/// </summary>
/// <param name="ArmId">The arm this sheet belongs to, from the route.</param>
/// <param name="SubjectId">The subject this sheet is for.</param>
/// <param name="TermId">The term this sheet is for.</param>
/// <param name="Version">
/// The sheet's version as last read, or <see langword="null"/> for a sheet with no rows yet. A
/// mismatch against the server's current version is a 409 <c>score_sheet.stale_version</c>.
/// </param>
/// <param name="Rows">Every row being saved. A row not present here is left untouched.</param>
public sealed record SaveScoreSheetCommand(
    string ArmId, string SubjectId, string TermId, string? Version, IReadOnlyList<SaveScoreSheetRowInput> Rows)
    : ICommand<Result<ScoreSheetDto>>;

/// <summary>
/// Structural checks only — every per-cell business rule (spec 6.7.4: component membership, ranges,
/// roster membership) is data-dependent (assessment structure and roster both come from the database)
/// and is therefore the handler's job, the same split <c>CreateSubjectExceptionHandler</c> uses.
/// </summary>
internal sealed class SaveScoreSheetCommandValidator : AbstractValidator<SaveScoreSheetCommand>
{
    public SaveScoreSheetCommandValidator()
    {
        RuleFor(command => command.ArmId).NotEmpty().Must(value => Guid.TryParse(value, out _))
            .WithMessage("ArmId must be a valid identifier.");
        RuleFor(command => command.SubjectId).NotEmpty().Must(value => Guid.TryParse(value, out _))
            .WithMessage("SubjectId must be a valid identifier.");
        RuleFor(command => command.TermId).NotEmpty().Must(value => Guid.TryParse(value, out _))
            .WithMessage("TermId must be a valid identifier.");
        RuleFor(command => command.Rows).NotNull();

        RuleForEach(command => command.Rows).ChildRules(row =>
        {
            row.RuleFor(entry => entry.PupilId).NotEmpty().Must(value => Guid.TryParse(value, out _))
                .WithMessage("PupilId must be a valid identifier.");
            row.RuleFor(entry => entry.ComponentMarks).NotNull()
                .WithMessage("ComponentMarks must be supplied, even if every value is blank.");
        });
    }
}
