using SchoolManagement.Domain.Common;

namespace SchoolManagement.Domain.Pupils;

/// <summary>Section H's checklist rows (spec 6.5.8), seeded from the form.</summary>
public enum PupilDocumentType
{
    /// <summary>Birth certificate.</summary>
    BirthCertificate,

    /// <summary>Passport photograph.</summary>
    PassportPhotograph,

    /// <summary>Previous school result.</summary>
    PreviousSchoolResult,

    /// <summary>Transfer letter.</summary>
    TransferLetter,

    /// <summary>The form's "Other Required Document" row, with its own label.</summary>
    Other,
}

/// <summary>
/// One row of the document checklist (spec 6.5.8). Created on first use; a type with no row reads as not received.
/// Ticking received without a file is normal — the school keeps paper. Approval never waits on a document.
/// </summary>
public sealed class PupilDocument : Entity<Guid>, IAuditableEntity
{
    /// <summary>Spec 6.5.8: String 80.</summary>
    public const int OtherLabelMaxLength = 80;

    /// <summary>Spec 6.5.8: String 200.</summary>
    public const int RemarksMaxLength = 200;

    private PupilDocument(Guid id, Guid pupilId, PupilDocumentType documentType)
        : base(id)
    {
        PupilId = pupilId;
        DocumentType = documentType;
    }

    // EF Core materialisation constructor.
    private PupilDocument()
        : base()
    {
    }

    /// <summary>The pupil.</summary>
    public Guid PupilId { get; private set; }

    /// <summary>Which checklist row.</summary>
    public PupilDocumentType DocumentType { get; private set; }

    /// <summary>Required for <see cref="PupilDocumentType.Other"/>.</summary>
    public string? OtherLabel { get; private set; }

    /// <summary>The form's checkbox.</summary>
    public bool Received { get; private set; }

    /// <summary>Defaults to today when ticked.</summary>
    public DateOnly? ReceivedDate { get; private set; }

    /// <summary>The Remarks column.</summary>
    public string? Remarks { get; private set; }

    /// <summary>The account that ticked it.</summary>
    public string? ReceivedBy { get; private set; }

    /// <summary>The attached scan's store id, or <see langword="null"/>. A convenience, never a requirement.</summary>
    public string? FileAssetId { get; private set; }

    /// <summary><c>application/pdf</c>, <c>image/jpeg</c> or <c>image/png</c>, decided by magic bytes.</summary>
    public string? FileContentType { get; private set; }

    /// <summary>The stored scan's size in bytes.</summary>
    public int? FileSizeBytes { get; private set; }

    /// <summary>When the scan was attached.</summary>
    public DateTimeOffset? FileUploadedAtUtc { get; private set; }

    /// <summary>The account that attached it.</summary>
    public string? FileUploadedBy { get; private set; }

    /// <inheritdoc />
    public DateTimeOffset CreatedAtUtc { get; set; }

    /// <inheritdoc />
    public string? CreatedBy { get; set; }

    /// <inheritdoc />
    public DateTimeOffset? ModifiedAtUtc { get; set; }

    /// <inheritdoc />
    public string? ModifiedBy { get; set; }

    /// <summary>A new, unticked row.</summary>
    public static PupilDocument Create(Guid id, Guid pupilId, PupilDocumentType documentType) => new(id, pupilId, documentType);

    /// <summary>Records the row's state. Unticking clears the date and the receiver.</summary>
    public Result Apply(bool received, DateOnly? receivedDate, string? remarks, string? otherLabel, DateOnly today, string? actor)
    {
        var label = DocumentType == PupilDocumentType.Other ? PersonFields.Clean(otherLabel) : null;
        if (DocumentType == PupilDocumentType.Other && received && label is null)
        {
            return Result.Failure(Error.Validation("document.other_label_required", "Name the other document."));
        }

        if (label is { Length: > OtherLabelMaxLength })
        {
            return Result.Failure(Error.Validation("document.other_label_too_long", "The document name must be at most 80 characters."));
        }

        var cleanRemarks = PersonFields.Clean(remarks);
        if (cleanRemarks is { Length: > RemarksMaxLength })
        {
            return Result.Failure(Error.Validation("document.remarks_too_long", "Remarks must be at most 200 characters."));
        }

        if (receivedDate is { } date && date > today)
        {
            return Result.Failure(Error.Validation("document.received_in_future", "The received date cannot be in the future."));
        }

        if (received && !Received)
        {
            ReceivedBy = actor;
        }

        Received = received;
        ReceivedDate = received ? receivedDate ?? ReceivedDate ?? today : null;
        ReceivedBy = received ? ReceivedBy : null;
        Remarks = cleanRemarks;
        OtherLabel = label;
        return Result.Success();
    }

    /// <summary>
    /// Attaches (or replaces) the row's one scan. An unticked row is ticked as received today by the uploader (human ruling
    /// 2026-09-26: a scan proves the school saw the document), so the Other row needs its label first. A replaced scan stays
    /// in the store, which never deletes.
    /// </summary>
    public Result AttachFile(string assetId, string contentType, int sizeBytes, DateTimeOffset uploadedAtUtc, DateOnly today, string? actor)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(assetId);
        ArgumentException.ThrowIfNullOrWhiteSpace(contentType);
        var allowed = CheckAttach();
        if (allowed.IsFailure)
        {
            return allowed;
        }

        if (!Received)
        {
            Received = true;
            ReceivedDate = today;
            ReceivedBy = actor;
        }

        FileAssetId = assetId;
        FileContentType = contentType;
        FileSizeBytes = sizeBytes;
        FileUploadedAtUtc = uploadedAtUtc;
        FileUploadedBy = actor;
        return Result.Success();
    }

    /// <summary>Whether <see cref="AttachFile"/> would succeed, so a caller can refuse before storing any bytes.</summary>
    public Result CheckAttach() =>
        DocumentType == PupilDocumentType.Other && OtherLabel is null
            ? Result.Failure(Error.Validation("document.other_label_required", "Name the other document before attaching a file."))
            : Result.Success();

    /// <summary>Removes the scan and leaves the tick alone: the paper still exists. Fails when there is none.</summary>
    public Result RemoveFile()
    {
        if (FileAssetId is null)
        {
            return Result.Failure(Error.NotFound("document.file_not_found", "This document has no attached file."));
        }

        FileAssetId = null;
        FileContentType = null;
        FileSizeBytes = null;
        FileUploadedAtUtc = null;
        FileUploadedBy = null;
        return Result.Success();
    }
}
