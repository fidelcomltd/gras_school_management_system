using FluentValidation;
using SchoolManagement.Application.Abstractions.Admissions;
using SchoolManagement.Application.Abstractions.Auth;
using SchoolManagement.Application.Abstractions.Classes;
using SchoolManagement.Application.Abstractions.Enrolments;
using SchoolManagement.Application.Abstractions.Identity;
using SchoolManagement.Application.Abstractions.Messaging;
using SchoolManagement.Application.Abstractions.Pupils;
using SchoolManagement.Application.Abstractions.Sessions;
using SchoolManagement.Application.Abstractions.Settings;
using SchoolManagement.Application.Portal;
using SchoolManagement.Application.Settings;
using SchoolManagement.Application.Weekly;
using SchoolManagement.Domain.Classes;
using SchoolManagement.Domain.Common;
using SchoolManagement.Domain.Security;
using DomainSizeVariant = SchoolManagement.Domain.Settings.SchoolImageSizeVariant;

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
    IEnrolmentRepository enrolments,
    IArmRepository arms,
    IClassLevelRepository classLevels,
    IAcademicSessionRepository sessions,
    ISchoolProfileRepository schoolProfiles,
    ISchoolImageRepository schoolImages,
    ISchoolImageStore imageStore,
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
        if (pupil.RegistrationNumber is null)
        {
            return Result.Failure<SchoolImageContent>(Error.Conflict(
                "pupil.not_admitted", "This pupil has no registration number yet. Approve the admission first."));
        }

        // The slip records the admission, so it prints the arm the pupil was admitted into (the first enrolment), never the
        // current one: a reprint years later must still read as the day it happened. The admission record supplies the date
        // and session where it exists; older or imported records fall back to that first enrolment.
        var record = await admissions.FindReadOnlyByPupilIdAsync(pupil.Id, cancellationToken).ConfigureAwait(false);
        var first = (await enrolments.ListByPupilReadOnlyAsync(pupil.Id, cancellationToken).ConfigureAwait(false))
            .OrderBy(enrolment => enrolment.EffectiveFrom)
            .FirstOrDefault();
        var arm = first is null ? null : await arms.FindReadOnlyByIdAsync(first.ArmId, cancellationToken).ConfigureAwait(false);
        var dateAdmitted = record?.DateAdmitted ?? first?.EffectiveFrom;
        var sessionId = record?.SessionId ?? arm?.SessionId;
        if (dateAdmitted is null || sessionId is null)
        {
            return Result.Failure<SchoolImageContent>(Error.Conflict(
                "pupil.no_admission_details", "There is no admission record or enrolment to print this pupil's slip from."));
        }

        var className = arm is null ? null : await ClassNameAsync(arm, cancellationToken).ConfigureAwait(false);
        var session = await sessions.FindReadOnlyByIdAsync(sessionId.Value, cancellationToken).ConfigureAwait(false);
        var profile = await schoolProfiles.GetReadOnlySingletonAsync(cancellationToken).ConfigureAwait(false);
        var printedBy = Guid.TryParse(currentUser.UserId, out var accountId)
            ? (await accounts.FindReadOnlyByIdAsync(accountId, cancellationToken).ConfigureAwait(false))?.StaffName
            : null;

        // The slip carries the school's current branding (project lead, 2026-09-30): a reprint shows today's logo and head
        // teacher, like any other document printed today, unlike a published result, which keeps its snapshot.
        var logo = await SnapshotImages.ReadAsync(schoolImages, imageStore, profile.CurrentLogoGroupId, DomainSizeVariant.Size200, cancellationToken)
            .ConfigureAwait(false);
        var signature = await SnapshotImages.ReadAsync(schoolImages, imageStore, profile.CurrentSignatureGroupId, DomainSizeVariant.Original, cancellationToken)
            .ConfigureAwait(false);
        var contact = string.Join("  ·  ", new[] { profile.Phone, profile.Email }.Where(part => !string.IsNullOrWhiteSpace(part)));

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
            dateAdmitted.Value,
            timeProvider.GetUtcNow().ToOffset(WeeklyProjection.LagosOffset).DateTime,
            printedBy ?? "Unknown account",
            profile.Motto,
            contact,
            profile.HeadTeacherName,
            logo,
            signature);
        var bytes = renderer.Render(slip);
        return Result.Success(new SchoolImageContent(new MemoryStream(bytes, writable: false), "application/pdf", "admission-slip.pdf"));
    }

    /// <summary>The admission arm, composed as the school writes it.</summary>
    private async Task<string> ClassNameAsync(Arm arm, CancellationToken cancellationToken)
    {
        var level = (await classLevels.ListAllReadOnlyAsync(cancellationToken).ConfigureAwait(false)).FirstOrDefault(candidate => candidate.Id == arm.ClassLevelId);
        return level is null ? arm.Label : ArmDisplayName.Compose(level.Name, arm.Label);
    }
}
