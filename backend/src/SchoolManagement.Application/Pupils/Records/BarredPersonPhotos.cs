using FluentValidation;
using SchoolManagement.Application.Abstractions.Audit;
using SchoolManagement.Application.Abstractions.Identity;
using SchoolManagement.Application.Abstractions.Messaging;
using SchoolManagement.Application.Abstractions.Pupils;
using SchoolManagement.Application.Abstractions.Settings;
using SchoolManagement.Application.Settings;
using SchoolManagement.Domain.Common;
using SchoolManagement.Domain.Pupils;
using SchoolManagement.Domain.Security;

namespace SchoolManagement.Application.Pupils.Records;

/// <summary>
/// <c>POST /api/v1/pupils/{pupilId}/barred-persons/photos</c> (project lead, 2026-09-30): a photograph of someone barred from
/// collecting the child, so the gate can recognise them. Stored straight away and attached when the barred list is saved
/// with the returned id.
/// </summary>
/// <param name="PupilId">From the route.</param>
/// <param name="FileBytes">The uploaded file.</param>
public sealed record UploadBarredPersonPhotoCommand(Guid PupilId, ReadOnlyMemory<byte> FileBytes) : ICommand<Result<BarredPersonPhotoDto>>;

/// <summary>Guards only against an empty body; the processor owns every real rule.</summary>
internal sealed class UploadBarredPersonPhotoCommandValidator : AbstractValidator<UploadBarredPersonPhotoCommand>
{
    public UploadBarredPersonPhotoCommandValidator() => RuleFor(command => command.FileBytes.Length).GreaterThan(0);
}

/// <summary>
/// <c>GET /api/v1/pupils/{pupilId}/barred-persons/photos/{photoId}</c>. A command, like the barred list's read, because every
/// view writes an audit event.
/// </summary>
/// <param name="PupilId">From the route.</param>
/// <param name="PhotoId">From the route.</param>
public sealed record ReadBarredPersonPhotoCommand(Guid PupilId, Guid PhotoId) : ICommand<Result<SchoolImageContent>>;

/// <summary>Nothing structural to check beyond the route.</summary>
internal sealed class ReadBarredPersonPhotoCommandValidator : AbstractValidator<ReadBarredPersonPhotoCommand>;

/// <summary>Handles <see cref="UploadBarredPersonPhotoCommand"/>. Needs <c>pupil.safeguarding.update</c> over the pupil.</summary>
internal sealed class UploadBarredPersonPhotoHandler(
    PupilRecordAccess access,
    IPupilRecordRepository records,
    ISchoolImageProcessor processor,
    ISchoolImageStore store,
    ICurrentUser currentUser,
    ISystemAuditSink auditSink)
    : IRequestHandler<UploadBarredPersonPhotoCommand, Result<BarredPersonPhotoDto>>
{
    /// <summary>The audit action; the photograph's store id only, never a name.</summary>
    public const string AuditAction = "pupil.safeguarding.photo_uploaded";

    /// <inheritdoc />
    public async Task<Result<BarredPersonPhotoDto>> HandleAsync(UploadBarredPersonPhotoCommand request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var allowed = await access.CheckAsync(request.PupilId, Privileges.Pupil.SafeguardingUpdate, cancellationToken).ConfigureAwait(false);
        if (allowed.IsFailure)
        {
            return Result.Failure<BarredPersonPhotoDto>(allowed.Error);
        }

        var processed = processor.ProcessPersonPhoto(request.FileBytes.ToArray());
        if (processed.IsFailure)
        {
            return Result.Failure<BarredPersonPhotoDto>(processed.Error);
        }

        var assetId = await store.PutAsync(processed.Value.Bytes, processed.Value.ContentType, cancellationToken).ConfigureAwait(false);
        var photo = BarredPersonPhoto.Create(Guid.CreateVersion7(), request.PupilId, assetId);
        await records.AddAsync(photo, cancellationToken).ConfigureAwait(false);
        await auditSink.RecordAsync(
            AuditAction, "barred_person_photo", PupilContactsMapper.Id(request.PupilId), PupilFiles.AssetMetadata(assetId), currentUser.UserId, cancellationToken)
            .ConfigureAwait(false);

        return Result.Success(new BarredPersonPhotoDto(PupilContactsMapper.Id(photo.Id)));
    }
}

/// <summary>
/// Handles <see cref="ReadBarredPersonPhotoCommand"/>. Needs <c>pupil.safeguarding.view</c> over the pupil, and serves only a
/// photograph a current barred person of that pupil carries: once a person is removed from the list, so is their picture.
/// </summary>
internal sealed class ReadBarredPersonPhotoHandler(
    PupilRecordAccess access, IPupilRecordRepository records, ISchoolImageStore store, ICurrentUser currentUser, ISystemAuditSink auditSink)
    : IRequestHandler<ReadBarredPersonPhotoCommand, Result<SchoolImageContent>>
{
    /// <inheritdoc />
    public async Task<Result<SchoolImageContent>> HandleAsync(ReadBarredPersonPhotoCommand request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var allowed = await access.CheckAsync(request.PupilId, Privileges.Pupil.SafeguardingView, cancellationToken).ConfigureAwait(false);
        if (allowed.IsFailure)
        {
            return Result.Failure<SchoolImageContent>(allowed.Error);
        }

        var persons = await records.ListBarredPersonsAsync(request.PupilId, track: false, cancellationToken).ConfigureAwait(false);
        var photo = persons.Any(person => person.PhotoId == request.PhotoId)
            ? await records.FindBarredPhotoAsync(request.PhotoId, cancellationToken).ConfigureAwait(false)
            : null;
        if (photo is null || photo.PupilId != request.PupilId)
        {
            return Result.Failure<SchoolImageContent>(Error.NotFound("barred_person.photo_not_found", "There is no such photograph on this pupil's list."));
        }

        await auditSink.RecordAsync(
            ReadBarredPersonsHandler.ReadAction, "barred_person_photo", PupilContactsMapper.Id(request.PupilId), PupilFiles.AssetMetadata(photo.AssetId),
            currentUser.UserId, cancellationToken)
            .ConfigureAwait(false);
        var content = await store.OpenAsync(photo.AssetId, cancellationToken).ConfigureAwait(false);
        return Result.Success(new SchoolImageContent(content, "image/jpeg", "barred-person.jpg"));
    }
}
