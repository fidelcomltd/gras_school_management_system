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
/// <c>POST /api/v1/pupils/{pupilId}/photo</c> (spec 6.5.4, 9.6). The file's raw bytes, read out of the multipart body by the
/// endpoint; <see cref="ISchoolImageProcessor.ProcessPupilPhoto"/> does the real validation.
/// </summary>
/// <param name="PupilId">From the route.</param>
/// <param name="FileBytes">The uploaded file.</param>
public sealed record UploadPupilPhotoCommand(Guid PupilId, ReadOnlyMemory<byte> FileBytes) : ICommand<Result<PupilPhotoDto>>;

/// <summary>Guards only against an empty body; the processor owns every real rule.</summary>
internal sealed class UploadPupilPhotoCommandValidator : AbstractValidator<UploadPupilPhotoCommand>
{
    public UploadPupilPhotoCommandValidator() => RuleFor(command => command.FileBytes.Length).GreaterThan(0);
}

/// <summary><c>DELETE /api/v1/pupils/{pupilId}/photo</c>: removal is allowed and audited (human ruling 2026-09-25).</summary>
/// <param name="PupilId">From the route.</param>
public sealed record RemovePupilPhotoCommand(Guid PupilId) : ICommand<Result>;

/// <summary>Nothing structural to check beyond the route.</summary>
internal sealed class RemovePupilPhotoCommandValidator : AbstractValidator<RemovePupilPhotoCommand>;

/// <summary><c>GET /api/v1/pupils/{pupilId}/photo[/thumbnail]</c>: the bytes, through the pupil record's own privilege.</summary>
/// <param name="PupilId">From the route.</param>
/// <param name="Thumbnail">The 96 pixel rendition instead of the 400 pixel one.</param>
public sealed record GetPupilPhotoQuery(Guid PupilId, bool Thumbnail) : IQuery<Result<SchoolImageContent>>;

/// <summary>Nothing structural to check beyond the route.</summary>
internal sealed class GetPupilPhotoQueryValidator : AbstractValidator<GetPupilPhotoQuery>;

/// <summary><c>POST /api/v1/pupils/{pupilId}/documents/{documentType}/file</c> (spec 6.5.8): attach or replace a row's scan.</summary>
/// <param name="PupilId">From the route.</param>
/// <param name="DocumentType">From the route.</param>
/// <param name="FileBytes">The uploaded file.</param>
public sealed record UploadPupilDocumentFileCommand(Guid PupilId, PupilDocumentType DocumentType, ReadOnlyMemory<byte> FileBytes)
    : ICommand<Result<PupilDocumentListDto>>;

/// <summary>Enum member and a non-empty body; the processor owns the rest.</summary>
internal sealed class UploadPupilDocumentFileCommandValidator : AbstractValidator<UploadPupilDocumentFileCommand>
{
    public UploadPupilDocumentFileCommandValidator()
    {
        RuleFor(command => command.DocumentType).IsInEnum();
        RuleFor(command => command.FileBytes.Length).GreaterThan(0);
    }
}

/// <summary><c>DELETE /api/v1/pupils/{pupilId}/documents/{documentType}/file</c>: the scan goes, the tick stays.</summary>
/// <param name="PupilId">From the route.</param>
/// <param name="DocumentType">From the route.</param>
public sealed record RemovePupilDocumentFileCommand(Guid PupilId, PupilDocumentType DocumentType) : ICommand<Result<PupilDocumentListDto>>;

/// <summary>Enum member only.</summary>
internal sealed class RemovePupilDocumentFileCommandValidator : AbstractValidator<RemovePupilDocumentFileCommand>
{
    public RemovePupilDocumentFileCommandValidator() => RuleFor(command => command.DocumentType).IsInEnum();
}

/// <summary><c>GET /api/v1/pupils/{pupilId}/documents/{documentType}/file</c>: the scan, through the pupil record's privilege.</summary>
/// <param name="PupilId">From the route.</param>
/// <param name="DocumentType">From the route.</param>
public sealed record GetPupilDocumentFileQuery(Guid PupilId, PupilDocumentType DocumentType) : IQuery<Result<SchoolImageContent>>;

/// <summary>Enum member only.</summary>
internal sealed class GetPupilDocumentFileQueryValidator : AbstractValidator<GetPupilDocumentFileQuery>
{
    public GetPupilDocumentFileQueryValidator() => RuleFor(query => query.DocumentType).IsInEnum();
}

/// <summary>Handles <see cref="UploadPupilPhotoCommand"/>. The privilege is checked before any decoding, so a refused caller costs no CPU.</summary>
internal sealed class UploadPupilPhotoHandler(
    PupilRecordAccess access,
    IPupilRepository pupils,
    ISchoolImageProcessor processor,
    ISchoolImageStore store,
    ICurrentUser currentUser,
    ISystemAuditSink auditSink,
    TimeProvider timeProvider)
    : IRequestHandler<UploadPupilPhotoCommand, Result<PupilPhotoDto>>
{
    /// <inheritdoc />
    public async Task<Result<PupilPhotoDto>> HandleAsync(UploadPupilPhotoCommand request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var allowed = await access.CheckAsync(request.PupilId, Privileges.Pupil.PhotoUpdate, cancellationToken).ConfigureAwait(false);
        if (allowed.IsFailure)
        {
            return Result.Failure<PupilPhotoDto>(allowed.Error);
        }

        var processed = processor.ProcessPupilPhoto(request.FileBytes.ToArray());
        if (processed.IsFailure)
        {
            return Result.Failure<PupilPhotoDto>(processed.Error);
        }

        var pupil = await pupils.FindTrackedByIdAsync(request.PupilId, cancellationToken).ConfigureAwait(false);
        if (pupil is null)
        {
            return Result.Failure<PupilPhotoDto>(Error.NotFound("pupil.not_found", "No pupil was found with that id."));
        }

        var before = pupil.PhotoAssetId;
        var photo = processed.Value;
        var standardId = await store.PutAsync(photo.Standard.Bytes, photo.Standard.ContentType, cancellationToken).ConfigureAwait(false);
        var thumbnailId = await store.PutAsync(photo.Thumbnail.Bytes, photo.Thumbnail.ContentType, cancellationToken).ConfigureAwait(false);
        var now = PupilFiles.StoredInstant(timeProvider.GetUtcNow());
        pupil.SetPhoto(standardId, thumbnailId, now);

        // Store ids only: opaque, and never a name.
        await auditSink.RecordAsync(
            Privileges.Pupil.PhotoUpdate, PupilFiles.PupilEntityType, PupilContactsMapper.Id(pupil.Id),
            PupilFiles.AssetMetadata(standardId), currentUser.UserId, cancellationToken,
            beforeMetadata: before is null ? null : PupilFiles.AssetMetadata(before))
            .ConfigureAwait(false);

        return Result.Success(PupilFiles.PhotoDto(pupil.Id, now));
    }
}

/// <summary>Handles <see cref="RemovePupilPhotoCommand"/>. Needs <c>pupil.photo.update</c>, the same privilege as an upload.</summary>
internal sealed class RemovePupilPhotoHandler(
    PupilRecordAccess access, IPupilRepository pupils, ICurrentUser currentUser, ISystemAuditSink auditSink)
    : IRequestHandler<RemovePupilPhotoCommand, Result>
{
    /// <summary>The audit action; its own name, so a removal is findable apart from an upload.</summary>
    public const string AuditAction = "pupil.photo.remove";

    /// <inheritdoc />
    public async Task<Result> HandleAsync(RemovePupilPhotoCommand request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var allowed = await access.CheckAsync(request.PupilId, Privileges.Pupil.PhotoUpdate, cancellationToken).ConfigureAwait(false);
        if (allowed.IsFailure)
        {
            return Result.Failure(allowed.Error);
        }

        var pupil = await pupils.FindTrackedByIdAsync(request.PupilId, cancellationToken).ConfigureAwait(false);
        if (pupil is null)
        {
            return Result.Failure(Error.NotFound("pupil.not_found", "No pupil was found with that id."));
        }

        var before = pupil.PhotoAssetId;
        var removed = pupil.RemovePhoto();
        if (removed.IsFailure)
        {
            return removed;
        }

        await auditSink.RecordAsync(
            AuditAction, PupilFiles.PupilEntityType, PupilContactsMapper.Id(pupil.Id), metadata: null, currentUser.UserId, cancellationToken,
            beforeMetadata: PupilFiles.AssetMetadata(before!))
            .ConfigureAwait(false);

        return Result.Success();
    }
}

/// <summary>Handles <see cref="GetPupilPhotoQuery"/>. Needs <c>pupil.view</c> over the pupil.</summary>
internal sealed class GetPupilPhotoHandler(PupilRecordAccess access, ISchoolImageStore store)
    : IRequestHandler<GetPupilPhotoQuery, Result<SchoolImageContent>>
{
    /// <inheritdoc />
    public async Task<Result<SchoolImageContent>> HandleAsync(GetPupilPhotoQuery request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var allowed = await access.CheckAsync(request.PupilId, Privileges.Pupil.View, cancellationToken).ConfigureAwait(false);
        if (allowed.IsFailure)
        {
            return Result.Failure<SchoolImageContent>(allowed.Error);
        }

        var assetId = request.Thumbnail ? allowed.Value.PhotoThumbnailAssetId : allowed.Value.PhotoAssetId;
        if (assetId is null)
        {
            return Result.Failure<SchoolImageContent>(Error.NotFound("pupil.photo_not_found", "This pupil has no photograph."));
        }

        var content = await store.OpenAsync(assetId, cancellationToken).ConfigureAwait(false);
        return Result.Success(new SchoolImageContent(content, "image/jpeg", request.Thumbnail ? "photo-96.jpg" : "photo.jpg"));
    }
}

/// <summary>Handles <see cref="UploadPupilDocumentFileCommand"/>. Needs <c>pupil.document.manage</c> over the pupil.</summary>
internal sealed class UploadPupilDocumentFileHandler(
    PupilRecordAccess access,
    IPupilRecordRepository records,
    ISchoolImageProcessor processor,
    ISchoolImageStore store,
    ICurrentUser currentUser,
    ISystemAuditSink auditSink,
    TimeProvider timeProvider)
    : IRequestHandler<UploadPupilDocumentFileCommand, Result<PupilDocumentListDto>>
{
    /// <summary>The audit action for an attached or replaced scan.</summary>
    public const string AuditAction = "pupil.document.file_attached";

    /// <inheritdoc />
    public async Task<Result<PupilDocumentListDto>> HandleAsync(UploadPupilDocumentFileCommand request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var allowed = await access.CheckAsync(request.PupilId, Privileges.Pupil.DocumentManage, cancellationToken).ConfigureAwait(false);
        if (allowed.IsFailure)
        {
            return Result.Failure<PupilDocumentListDto>(allowed.Error);
        }

        var processed = processor.ProcessDocumentScan(request.FileBytes.ToArray());
        if (processed.IsFailure)
        {
            return Result.Failure<PupilDocumentListDto>(processed.Error);
        }

        var documents = (await records.ListDocumentsAsync(request.PupilId, track: true, cancellationToken).ConfigureAwait(false)).ToList();
        var document = documents.FirstOrDefault(candidate => candidate.DocumentType == request.DocumentType);
        var isNew = document is null;
        document ??= PupilDocument.Create(Guid.CreateVersion7(), request.PupilId, request.DocumentType);

        // Refused before the store sees any bytes, so a refusal leaves nothing behind.
        var attachable = document.CheckAttach();
        if (attachable.IsFailure)
        {
            return Result.Failure<PupilDocumentListDto>(attachable.Error);
        }

        var before = document.FileAssetId;
        var scan = processed.Value;
        var assetId = await store.PutAsync(scan.Bytes, scan.ContentType, cancellationToken).ConfigureAwait(false);
        var now = PupilFiles.StoredInstant(timeProvider.GetUtcNow());
        var attached = document.AttachFile(
            assetId, scan.ContentType, scan.Bytes.Length, now, Weekly.WeeklyProjection.LagosToday(now), currentUser.UserId);
        if (attached.IsFailure)
        {
            return Result.Failure<PupilDocumentListDto>(attached.Error);
        }

        if (isNew)
        {
            await records.AddAsync(document, cancellationToken).ConfigureAwait(false);
            documents.Add(document);
        }

        await auditSink.RecordAsync(
            AuditAction, PupilFiles.DocumentEntityType, PupilContactsMapper.Id(request.PupilId),
            new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["documentType"] = request.DocumentType.ToString(),
                ["contentType"] = scan.ContentType,
                ["sizeBytes"] = scan.Bytes.Length,
                ["assetId"] = assetId,
            },
            currentUser.UserId, cancellationToken,
            beforeMetadata: before is null ? null : PupilFiles.AssetMetadata(before))
            .ConfigureAwait(false);

        return Result.Success(DocumentMapper.ToDto(request.PupilId, documents));
    }
}

/// <summary>Handles <see cref="RemovePupilDocumentFileCommand"/>. Needs <c>pupil.document.manage</c> over the pupil.</summary>
internal sealed class RemovePupilDocumentFileHandler(
    PupilRecordAccess access, IPupilRecordRepository records, ICurrentUser currentUser, ISystemAuditSink auditSink)
    : IRequestHandler<RemovePupilDocumentFileCommand, Result<PupilDocumentListDto>>
{
    /// <summary>The audit action for a removed scan.</summary>
    public const string AuditAction = "pupil.document.file_removed";

    /// <inheritdoc />
    public async Task<Result<PupilDocumentListDto>> HandleAsync(RemovePupilDocumentFileCommand request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var allowed = await access.CheckAsync(request.PupilId, Privileges.Pupil.DocumentManage, cancellationToken).ConfigureAwait(false);
        if (allowed.IsFailure)
        {
            return Result.Failure<PupilDocumentListDto>(allowed.Error);
        }

        var documents = await records.ListDocumentsAsync(request.PupilId, track: true, cancellationToken).ConfigureAwait(false);
        var document = documents.FirstOrDefault(candidate => candidate.DocumentType == request.DocumentType);
        var before = document?.FileAssetId;
        var removed = document?.RemoveFile() ?? PupilFiles.NoFile();
        if (removed.IsFailure)
        {
            return Result.Failure<PupilDocumentListDto>(removed.Error);
        }

        await auditSink.RecordAsync(
            AuditAction, PupilFiles.DocumentEntityType, PupilContactsMapper.Id(request.PupilId),
            new Dictionary<string, object?>(StringComparer.Ordinal) { ["documentType"] = request.DocumentType.ToString() },
            currentUser.UserId, cancellationToken,
            beforeMetadata: PupilFiles.AssetMetadata(before!))
            .ConfigureAwait(false);

        return Result.Success(DocumentMapper.ToDto(request.PupilId, documents));
    }
}

/// <summary>Handles <see cref="GetPupilDocumentFileQuery"/>. Needs <c>pupil.view</c> over the pupil, as the record does.</summary>
internal sealed class GetPupilDocumentFileHandler(PupilRecordAccess access, IPupilRecordRepository records, ISchoolImageStore store)
    : IRequestHandler<GetPupilDocumentFileQuery, Result<SchoolImageContent>>
{
    /// <inheritdoc />
    public async Task<Result<SchoolImageContent>> HandleAsync(GetPupilDocumentFileQuery request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var allowed = await access.CheckAsync(request.PupilId, Privileges.Pupil.View, cancellationToken).ConfigureAwait(false);
        if (allowed.IsFailure)
        {
            return Result.Failure<SchoolImageContent>(allowed.Error);
        }

        var documents = await records.ListDocumentsAsync(request.PupilId, track: false, cancellationToken).ConfigureAwait(false);
        var document = documents.FirstOrDefault(candidate => candidate.DocumentType == request.DocumentType);
        if (document is not { FileAssetId: { } assetId, FileContentType: { } contentType })
        {
            return Result.Failure<SchoolImageContent>(PupilFiles.NoFile().Error);
        }

        var content = await store.OpenAsync(assetId, cancellationToken).ConfigureAwait(false);
        return Result.Success(new SchoolImageContent(content, contentType, PupilFiles.DocumentFileName(request.DocumentType, contentType)));
    }
}

/// <summary>Shared by the photograph and document-scan handlers.</summary>
internal static class PupilFiles
{
    public const string PupilEntityType = "pupil";

    public const string DocumentEntityType = "pupil_document";

    /// <summary>
    /// Truncated to the microsecond PostgreSQL stores, so the time an upload returns equals the time a later read returns
    /// (<c>PupilDto.photoUpdatedAtUtc</c> is a client's cache key for the photograph).
    /// </summary>
    public static DateTimeOffset StoredInstant(DateTimeOffset instant) => instant.AddTicks(-(instant.Ticks % 10));

    public static Dictionary<string, object?> AssetMetadata(string assetId) => new(StringComparer.Ordinal) { ["assetId"] = assetId };

    public static Result NoFile() => Result.Failure(Error.NotFound("document.file_not_found", "This document has no attached file."));

    public static PupilPhotoDto PhotoDto(Guid pupilId, DateTimeOffset updatedAtUtc)
    {
        var id = PupilContactsMapper.Id(pupilId);
        return new PupilPhotoDto(id, updatedAtUtc, $"/api/v1/pupils/{id}/photo", $"/api/v1/pupils/{id}/photo/thumbnail");
    }

    /// <summary>A download name from the document type, never from anything the uploader sent.</summary>
    public static string DocumentFileName(PupilDocumentType type, string contentType)
    {
        var stem = type switch
        {
            PupilDocumentType.BirthCertificate => "birth-certificate",
            PupilDocumentType.PassportPhotograph => "passport-photograph",
            PupilDocumentType.PreviousSchoolResult => "previous-school-result",
            PupilDocumentType.TransferLetter => "transfer-letter",
            _ => "other-document",
        };
        var extension = contentType switch
        {
            "application/pdf" => ".pdf",
            "image/png" => ".png",
            _ => ".jpg",
        };
        return stem + extension;
    }
}
