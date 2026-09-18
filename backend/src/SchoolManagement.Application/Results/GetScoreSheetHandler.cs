using SchoolManagement.Application.Abstractions.Enrolments;
using SchoolManagement.Application.Abstractions.Messaging;
using SchoolManagement.Application.Abstractions.Results;
using SchoolManagement.Application.Abstractions.Sessions;
using SchoolManagement.Application.Abstractions.Subjects;
using SchoolManagement.Application.Settings;
using SchoolManagement.Application.Subjects;
using SchoolManagement.Domain.Common;

namespace SchoolManagement.Application.Results;

/// <summary>Handles <see cref="GetScoreSheetQuery"/>.</summary>
internal sealed class GetScoreSheetHandler(
    ITermRepository terms,
    ISubjectRepository subjects,
    SubjectsInEffectResolver subjectsInEffect,
    IAssessmentComponentRepository components,
    IEnrolmentRepository enrolments,
    IResultSetRepository resultSets,
    ISubjectScoreRepository scores)
    : IRequestHandler<GetScoreSheetQuery, Result<ScoreSheetDto>>
{
    /// <inheritdoc />
    public async Task<Result<ScoreSheetDto>> HandleAsync(GetScoreSheetQuery request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var armId = Guid.Parse(request.ArmId);
        var subjectId = Guid.Parse(request.SubjectId);
        var termId = Guid.Parse(request.TermId);

        var term = await terms.FindReadOnlyByIdAsync(termId, cancellationToken).ConfigureAwait(false);
        if (term is null)
        {
            return Result.Failure<ScoreSheetDto>(Error.NotFound("term.not_found", "No term was found with that id."));
        }

        var subject = await subjects.FindReadOnlyByIdAsync(subjectId, cancellationToken).ConfigureAwait(false);
        if (subject is null)
        {
            return Result.Failure<ScoreSheetDto>(Error.NotFound("subject.not_found", "No subject was found with that id."));
        }

        var resolution = await subjectsInEffect.ResolveAsync(armId, termId, cancellationToken).ConfigureAwait(false);
        if (resolution.IsFailure)
        {
            return Result.Failure<ScoreSheetDto>(resolution.Error);
        }

        if (!resolution.Value.Any(resolved => resolved.SubjectId == subjectId))
        {
            return Result.Failure<ScoreSheetDto>(Error.Validation(
                "score_sheet.subject_not_in_effect", $"{subject.Name} is not in effect for this arm this term."));
        }

        var roster = await enrolments.ListActiveRosterByArmAsync(armId, cancellationToken).ConfigureAwait(false);
        var structure = await components.ListReadOnlyOrderedAsync(cancellationToken).ConfigureAwait(false);
        var resultSet = await resultSets.FindReadOnlyByArmTermAsync(armId, termId, cancellationToken).ConfigureAwait(false);

        var scoresByPupil = resultSet is null
            ? new Dictionary<Guid, ScoreSheetVersionRow>()
            : (await scores.ListActiveReadOnlyAsync(resultSet.Id, subjectId, cancellationToken).ConfigureAwait(false))
                .ToDictionary(
                    row => row.PupilId,
                    row => new ScoreSheetVersionRow(row.PupilId, row.ComponentMarksJson, row.ExamMark, row.ExamAbsent));

        var dto = ScoreSheetProjection.Build(armId, subjectId, termId, resultSet, roster, structure, scoresByPupil);

        return Result.Success(dto);
    }
}
