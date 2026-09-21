using SchoolManagement.Application.Abstractions.Classes;
using SchoolManagement.Application.Abstractions.Enrolments;
using SchoolManagement.Application.Abstractions.Messaging;
using SchoolManagement.Application.Abstractions.Results;
using SchoolManagement.Application.Abstractions.Sessions;
using SchoolManagement.Domain.Common;
using SchoolManagement.Domain.Results;

namespace SchoolManagement.Application.Results;

/// <summary>Handles <see cref="GetHeadTeacherRemarksQuery"/>.</summary>
internal sealed class GetHeadTeacherRemarksHandler(
    IArmRepository arms,
    ITermRepository terms,
    IEnrolmentRepository enrolments,
    IResultSetRepository resultSets,
    IPupilRemarkRepository pupilRemarks)
    : IRequestHandler<GetHeadTeacherRemarksQuery, Result<RemarkSheetDto>>
{
    /// <inheritdoc />
    public async Task<Result<RemarkSheetDto>> HandleAsync(GetHeadTeacherRemarksQuery request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var armId = Guid.Parse(request.ArmId);
        var termId = Guid.Parse(request.TermId);

        var arm = await arms.FindReadOnlyByIdAsync(armId, cancellationToken).ConfigureAwait(false);
        if (arm is null)
        {
            return Result.Failure<RemarkSheetDto>(Error.NotFound("arm.not_found", "No arm was found with that id."));
        }

        var term = await terms.FindReadOnlyByIdAsync(termId, cancellationToken).ConfigureAwait(false);
        if (term is null)
        {
            return Result.Failure<RemarkSheetDto>(Error.NotFound("term.not_found", "No term was found with that id."));
        }

        var roster = await enrolments.ListActiveRosterByArmAsync(armId, cancellationToken).ConfigureAwait(false);
        var resultSet = await resultSets.FindReadOnlyByArmTermAsync(armId, termId, cancellationToken).ConfigureAwait(false);

        IReadOnlyList<PupilRemarkSnapshot> remarks = resultSet is null
            ? []
            : await pupilRemarks.ListReadOnlyAsync(resultSet.Id, RemarkKind.HeadTeacher, cancellationToken).ConfigureAwait(false);

        var dto = RemarkProjection.Build(armId, termId, resultSet, roster, remarks);

        return Result.Success(dto);
    }
}
