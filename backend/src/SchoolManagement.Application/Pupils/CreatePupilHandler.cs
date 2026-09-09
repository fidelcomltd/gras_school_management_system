using System.Globalization;
using SchoolManagement.Application.Abstractions.Audit;
using SchoolManagement.Application.Abstractions.Identity;
using SchoolManagement.Application.Abstractions.Messaging;
using SchoolManagement.Application.Abstractions.Pupils;
using SchoolManagement.Domain.Common;
using SchoolManagement.Domain.Pupils;
using SchoolManagement.Domain.Security;

namespace SchoolManagement.Application.Pupils;

/// <summary>Handles <see cref="CreatePupilCommand"/>.</summary>
internal sealed class CreatePupilHandler(
    IPupilRepository pupils,
    ICurrentUser currentUser,
    ISystemAuditSink auditSink,
    TimeProvider timeProvider)
    : IRequestHandler<CreatePupilCommand, Result<PupilDto>>
{
    private const string EntityType = "pupil";

    /// <inheritdoc />
    public async Task<Result<PupilDto>> HandleAsync(CreatePupilCommand request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var today = DateOnly.FromDateTime(timeProvider.GetUtcNow().UtcDateTime);

        var creation = Pupil.Create(
            Guid.CreateVersion7(),
            request.Surname,
            request.FirstName,
            request.MiddleName,
            request.Sex,
            request.DateOfBirth,
            today,
            request.Nationality,
            request.StateOfOrigin,
            request.Lga,
            request.HomeAddress,
            request.PreviousSchool,
            request.PreviousClass,
            request.OtherInformation);

        if (creation.IsFailure)
        {
            return Result.Failure<PupilDto>(creation.Error);
        }

        var newPupil = creation.Value;
        await pupils.AddAsync(newPupil, cancellationToken).ConfigureAwait(false);

        await auditSink.RecordAsync(
            Privileges.Pupil.Create,
            EntityType,
            newPupil.Id.ToString("D", CultureInfo.InvariantCulture),
            metadata: null,
            actorAdminId: currentUser.UserId,
            cancellationToken).ConfigureAwait(false);

        return Result.Success(PupilMapper.ToDto(newPupil, today));
    }
}
