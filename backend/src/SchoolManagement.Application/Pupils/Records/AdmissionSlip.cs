using FluentValidation;
using SchoolManagement.Application.Abstractions.Admissions;
using SchoolManagement.Application.Abstractions.Auth;
using SchoolManagement.Application.Abstractions.Authorization;
using SchoolManagement.Application.Abstractions.Classes;
using SchoolManagement.Application.Abstractions.Identity;
using SchoolManagement.Application.Abstractions.Messaging;
using SchoolManagement.Application.Abstractions.Pupils;
using SchoolManagement.Application.Abstractions.Sessions;
using SchoolManagement.Application.Settings;
using SchoolManagement.Application.Weekly;
using SchoolManagement.Domain.Classes;
using SchoolManagement.Domain.Common;
using SchoolManagement.Domain.Security;

namespace SchoolManagement.Application.Pupils.Records;

/// <summary>
/// <c>GET /api/v1/pupils/{pupilId}/admission-slip</c> (spec 6.5.11: "Print admission slip" on the confirmation screen): the
/// slip with the issued registration number, as a PDF. Needs <c>pupil.view</c> over the pupil.
/// </summary>
/// <param name="PupilId">From the route.</param>
public sealed record GetAdmissionSlipQuery(Guid PupilId) : IQuery<Result<SchoolImageContent>>;

/// <summary>Nothing structural to check beyond the route.</summary>
internal sealed class GetAdmissionSlipQueryValidator : AbstractValidator<GetAdmissionSlipQuery>;

/// <summary>Handles <see cref="GetAdmissionSlipQuery"/>.</summary>
internal sealed class GetAdmissionSlipHandler(
    PupilRecordAccess access,
    IAdmissionRecordRepository admissions,
    IPupilArmOfRecordLookup armOfRecord,
    IArmRepository arms,
    IClassLevelRepository classLevels,
    IAcademicSessionRepository sessions,
    ISchoolProfileRepository schoolProfiles,
    IAdminAccountRepository accounts,
    ICurrentUser currentUser,
    IAdmissionSlipRenderer renderer,
    TimeProvider timeProvider)
    : IRequestHandler<GetAdmissionSlipQuery, Result<SchoolImageContent>>
{
    /// <inheritdoc />
    public async Task<Result<SchoolImageContent>> HandleAsync(GetAdmissionSlipQuery request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var allowed = await access.CheckAsync(request.PupilId, Privileges.Pupil.View, cancellationToken).ConfigureAwait(false);
        if (allowed.IsFailure)
        {
            return Result.Failure<SchoolImageContent>(allowed.Error);
        }

        var pupil = allowed.Value;
        var record = await admissions.FindReadOnlyByPupilIdAsync(pupil.Id, cancellationToken).ConfigureAwait(false);
        if (pupil.RegistrationNumber is null || record is null)
        {
            return Result.Failure<SchoolImageContent>(Error.Conflict(
                "pupil.not_admitted", "This pupil has no registration number yet. Approve the admission first."));
        }

        var className = await ClassNameAsync(pupil.Id, cancellationToken).ConfigureAwait(false);
        var session = await sessions.FindReadOnlyByIdAsync(record.SessionId, cancellationToken).ConfigureAwait(false);
        var profile = await schoolProfiles.GetReadOnlySingletonAsync(cancellationToken).ConfigureAwait(false);
        var printedBy = Guid.TryParse(currentUser.UserId, out var accountId)
            ? (await accounts.FindReadOnlyByIdAsync(accountId, cancellationToken).ConfigureAwait(false))?.StaffName
            : null;

        var name = string.Join(' ', new[] { pupil.Surname.ToUpperInvariant(), pupil.FirstName, pupil.MiddleName }.Where(part => !string.IsNullOrWhiteSpace(part)));
        var slip = new AdmissionSlipDocument(
            profile.SchoolName,
            profile.Address,
            name,
            pupil.RegistrationNumber,
            pupil.Sex.ToString(),
            pupil.DateOfBirth,
            className ?? "Not yet placed",
            session?.Name ?? string.Empty,
            record.DateAdmitted,
            timeProvider.GetUtcNow().ToOffset(WeeklyProjection.LagosOffset).DateTime,
            printedBy ?? "Unknown account");
        var bytes = renderer.Render(slip);
        return Result.Success(new SchoolImageContent(new MemoryStream(bytes, writable: false), "application/pdf", "admission-slip.pdf"));
    }

    /// <summary>The arm of the pupil's open enrolment, composed as the school writes it.</summary>
    private async Task<string?> ClassNameAsync(Guid pupilId, CancellationToken cancellationToken)
    {
        if (await armOfRecord.GetArmIdAsync(pupilId, cancellationToken).ConfigureAwait(false) is not { } armId
            || await arms.FindReadOnlyByIdAsync(armId, cancellationToken).ConfigureAwait(false) is not { } arm)
        {
            return null;
        }

        var level = (await classLevels.ListAllReadOnlyAsync(cancellationToken).ConfigureAwait(false)).FirstOrDefault(candidate => candidate.Id == arm.ClassLevelId);
        return level is null ? arm.Label : ArmDisplayName.Compose(level.Name, arm.Label);
    }
}
