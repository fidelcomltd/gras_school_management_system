using SchoolManagement.Application.Abstractions.Classes;
using SchoolManagement.Application.Abstractions.Enrolments;
using SchoolManagement.Application.Abstractions.Messaging;
using SchoolManagement.Application.Abstractions.Results;
using SchoolManagement.Application.Abstractions.Sessions;
using SchoolManagement.Domain.Common;

namespace SchoolManagement.Application.Results;

/// <summary>Handles <see cref="GetAttendanceQuery"/>.</summary>
internal sealed class GetAttendanceHandler(
    IArmRepository arms,
    ITermRepository terms,
    IEnrolmentRepository enrolments,
    IResultSetRepository resultSets,
    IAttendanceEntryRepository attendanceEntries)
    : IRequestHandler<GetAttendanceQuery, Result<AttendanceSheetDto>>
{
    /// <inheritdoc />
    public async Task<Result<AttendanceSheetDto>> HandleAsync(GetAttendanceQuery request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var armId = Guid.Parse(request.ArmId);
        var termId = Guid.Parse(request.TermId);

        var arm = await arms.FindReadOnlyByIdAsync(armId, cancellationToken).ConfigureAwait(false);
        if (arm is null)
        {
            return Result.Failure<AttendanceSheetDto>(Error.NotFound("arm.not_found", "No arm was found with that id."));
        }

        var term = await terms.FindReadOnlyByIdAsync(termId, cancellationToken).ConfigureAwait(false);
        if (term is null)
        {
            return Result.Failure<AttendanceSheetDto>(Error.NotFound("term.not_found", "No term was found with that id."));
        }

        var roster = await enrolments.ListActiveRosterByArmAsync(armId, cancellationToken).ConfigureAwait(false);
        var resultSet = await resultSets.FindReadOnlyByArmTermAsync(armId, termId, cancellationToken).ConfigureAwait(false);

        IReadOnlyList<AttendanceEntrySnapshot> entries = resultSet is null
            ? []
            : await attendanceEntries.ListReadOnlyAsync(resultSet.Id, cancellationToken).ConfigureAwait(false);

        var dto = AttendanceProjection.Build(armId, termId, term.TimesSchoolOpened, resultSet, roster, entries);

        return Result.Success(dto);
    }
}
