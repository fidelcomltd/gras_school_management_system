using System.Globalization;
using SchoolManagement.Application.Abstractions.Audit;
using SchoolManagement.Application.Abstractions.Identity;
using SchoolManagement.Application.Abstractions.Messaging;
using SchoolManagement.Application.Abstractions.Subjects;
using SchoolManagement.Domain.Common;
using SchoolManagement.Domain.Security;

namespace SchoolManagement.Application.Subjects;

/// <summary>
/// Handles <see cref="DeleteSubjectCommand"/>.
/// </summary>
/// <remarks>
/// Spec 6.6.8: "Subject deleted | Permitted only where it has never been mapped and never scored.
/// Otherwise deactivate, per the single rule in 6.4.2" — the same "delete only if never referenced"
/// rule <c>DeleteLevelHandler</c> follows. <c>subject_score</c> does not exist anywhere in this
/// codebase yet (TASK-0071), so "never scored" cannot be false today; the checkable half is "never
/// mapped," backstopped by a <c>RESTRICT</c> foreign key on both <c>subject_mapping.subject_id</c> and
/// <c>subject_mapping_exception.subject_id</c> (<c>SubjectMappingConfiguration</c>,
/// <c>SubjectMappingExceptionConfiguration</c>).
/// </remarks>
internal sealed class DeleteSubjectHandler(
    ISubjectRepository subjects,
    ISubjectMappingRepository mappings,
    ICurrentUser currentUser,
    ISystemAuditSink auditSink)
    : IRequestHandler<DeleteSubjectCommand, Result>
{
    private const string EntityType = "subject";

    /// <inheritdoc />
    public async Task<Result> HandleAsync(DeleteSubjectCommand request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var subject = await subjects.FindTrackedByIdAsync(request.Id, cancellationToken).ConfigureAwait(false);

        if (subject is null)
        {
            return Result.Failure(Error.NotFound("subject.not_found", "No subject was found with that id."));
        }

        if (await mappings.AnyEverForSubjectAsync(subject.Id, cancellationToken).ConfigureAwait(false))
        {
            return Result.Failure(Error.Conflict(
                "subject.referenced",
                $"{subject.Name} cannot be deleted because it has been mapped. Deactivate it instead."));
        }

        await subjects.RemoveAsync(subject, cancellationToken).ConfigureAwait(false);

        await auditSink.RecordAsync(
            Privileges.Subject.Delete,
            EntityType,
            subject.Id.ToString("D", CultureInfo.InvariantCulture),
            metadata: null,
            actorAdminId: currentUser.UserId,
            cancellationToken).ConfigureAwait(false);

        return Result.Success();
    }
}
