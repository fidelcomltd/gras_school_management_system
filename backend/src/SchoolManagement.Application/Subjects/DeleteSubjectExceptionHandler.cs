using System.Globalization;
using SchoolManagement.Application.Abstractions.Audit;
using SchoolManagement.Application.Abstractions.Identity;
using SchoolManagement.Application.Abstractions.Messaging;
using SchoolManagement.Application.Abstractions.Results;
using SchoolManagement.Application.Abstractions.Subjects;
using SchoolManagement.Application.Results;
using SchoolManagement.Domain.Common;
using SchoolManagement.Domain.Results;
using SchoolManagement.Domain.Security;

namespace SchoolManagement.Application.Subjects;

/// <summary>Handles <see cref="DeleteSubjectExceptionCommand"/>.</summary>
internal sealed class DeleteSubjectExceptionHandler(
    ISubjectMappingExceptionRepository exceptions,
    IResultSetRepository resultSets,
    ICurrentUser currentUser,
    ISystemAuditSink auditSink)
    : IRequestHandler<DeleteSubjectExceptionCommand, Result>
{
    private const string EntityType = "subject_mapping_exception";

    /// <inheritdoc />
    public async Task<Result> HandleAsync(DeleteSubjectExceptionCommand request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var exception = await exceptions.FindTrackedByIdAsync(request.Id, cancellationToken).ConfigureAwait(false);

        if (exception is null)
        {
            return Result.Failure(Error.NotFound("subject_exception.not_found", "No exception was found with that id."));
        }

        var armId = exception.ArmId;
        var termId = exception.TermId;

        await exceptions.RemoveAsync(exception, cancellationToken).ConfigureAwait(false);

        await auditSink.RecordAsync(
            Privileges.Subject.MapArm,
            EntityType,
            exception.Id.ToString("D", CultureInfo.InvariantCulture),
            metadata: null,
            actorAdminId: currentUser.UserId,
            cancellationToken).ConfigureAwait(false);

        // TASK-0088 AC A2: removing an exception changes this ONE arm's in-effect subject list back
        // — flags its result set for this term, if one exists and is not Published.
        var lockedResultSet = await resultSets
            .FindTrackedByArmTermForUpdateAsync(armId, termId, cancellationToken)
            .ConfigureAwait(false);

        if (lockedResultSet is { State: not ResultSetState.Published })
        {
            await ResultSetRecomputeFlagger
                .FlagAsync([lockedResultSet], auditSink, cancellationToken)
                .ConfigureAwait(false);
        }

        return Result.Success();
    }
}
