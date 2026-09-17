using System.Globalization;
using SchoolManagement.Application.Abstractions.Audit;
using SchoolManagement.Application.Abstractions.Identity;
using SchoolManagement.Application.Abstractions.Messaging;
using SchoolManagement.Application.Abstractions.Subjects;
using SchoolManagement.Domain.Common;
using SchoolManagement.Domain.Security;
using SchoolManagement.Domain.Subjects;

namespace SchoolManagement.Application.Subjects;

/// <summary>Handles <see cref="CreateSubjectCommand"/>.</summary>
internal sealed class CreateSubjectHandler(
    ISubjectRepository subjects,
    ICurrentUser currentUser,
    ISystemAuditSink auditSink)
    : IRequestHandler<CreateSubjectCommand, Result<SubjectDto>>
{
    private const string EntityType = "subject";

    /// <inheritdoc />
    public async Task<Result<SubjectDto>> HandleAsync(CreateSubjectCommand request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var nameKey = request.Name.Trim().ToLowerInvariant();

        if (await subjects.NameExistsAsync(nameKey, excludingId: null, cancellationToken).ConfigureAwait(false))
        {
            return Result.Failure<SubjectDto>(Error.Conflict(
                "subject.name_duplicate", $"A subject named {request.Name.Trim()} already exists."));
        }

        if (!string.IsNullOrEmpty(request.Code))
        {
            var codeKey = request.Code.Trim().ToLowerInvariant();
            var conflicting = await subjects.FindByCodeKeyAsync(codeKey, excludingId: null, cancellationToken).ConfigureAwait(false);

            if (conflicting is not null)
            {
                return Result.Failure<SubjectDto>(Error.Conflict(
                    "subject.code_duplicate", $"The code {request.Code.Trim()} is already used by {conflicting.Name}."));
            }
        }

        var creation = Subject.Create(Guid.CreateVersion7(), request.Name, request.Code, request.Description);

        if (creation.IsFailure)
        {
            return Result.Failure<SubjectDto>(creation.Error);
        }

        var subject = creation.Value;
        await subjects.AddAsync(subject, cancellationToken).ConfigureAwait(false);

        await auditSink.RecordAsync(
            Privileges.Subject.Create,
            EntityType,
            subject.Id.ToString("D", CultureInfo.InvariantCulture),
            metadata: new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["name"] = subject.Name,
                ["code"] = subject.Code,
            },
            actorAdminId: currentUser.UserId,
            cancellationToken).ConfigureAwait(false);

        return Result.Success(SubjectMapper.ToDto(subject, mappedLevelCount: null, armExceptionCount: null, pupilsTakingCount: null));
    }
}
