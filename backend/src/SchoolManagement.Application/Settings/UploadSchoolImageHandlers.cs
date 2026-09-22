using System.Globalization;
using SchoolManagement.Application.Abstractions.Audit;
using SchoolManagement.Application.Abstractions.Auth;
using SchoolManagement.Application.Abstractions.Identity;
using SchoolManagement.Application.Abstractions.Messaging;
using SchoolManagement.Application.Abstractions.Settings;
using SchoolManagement.Domain.Common;
using SchoolManagement.Domain.Settings;
// SchoolImageSizeVariant exists once per layer, deliberately (Domain must not depend on Application —
// see the Domain enum's own remarks), so both are aliased here to keep every reference to either
// unambiguous rather than fully qualifying it inline throughout this file.
using AppRendition = SchoolManagement.Application.Abstractions.Settings.SchoolImageSizeVariant;
using DomainSizeVariant = SchoolManagement.Domain.Settings.SchoolImageSizeVariant;

namespace SchoolManagement.Application.Settings;

/// <summary>Handles <see cref="UploadSchoolLogoCommand"/> (TASK-0005b stage B2).</summary>
internal sealed class UploadSchoolLogoCommandHandler(
    ISchoolImageProcessor processor,
    ISchoolImageStore store,
    ISchoolImageRepository schoolImages,
    ISchoolProfileRepository schoolProfileRepository,
    IAdminAccountRepository accounts,
    ICurrentUser currentUser,
    ISystemAuditSink auditSink,
    TimeProvider timeProvider)
    : IRequestHandler<UploadSchoolLogoCommand, Result<SchoolImageDto>>
{
    /// <inheritdoc />
    public Task<Result<SchoolImageDto>> HandleAsync(UploadSchoolLogoCommand request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        return SchoolImageUploadSupport.UploadAsync(
            processor.ProcessLogo(request.FileBytes.ToArray()),
            SchoolImageKind.Logo,
            "settings.identity.logo_uploaded",
            static profile => profile.CurrentLogoGroupId,
            static (profile, groupId) => profile.SetCurrentLogo(groupId),
            store, schoolImages, schoolProfileRepository, accounts, currentUser, auditSink, timeProvider,
            cancellationToken);
    }
}

/// <summary>Handles <see cref="UploadSchoolSignatureCommand"/> (TASK-0005b stage B2).</summary>
internal sealed class UploadSchoolSignatureCommandHandler(
    ISchoolImageProcessor processor,
    ISchoolImageStore store,
    ISchoolImageRepository schoolImages,
    ISchoolProfileRepository schoolProfileRepository,
    IAdminAccountRepository accounts,
    ICurrentUser currentUser,
    ISystemAuditSink auditSink,
    TimeProvider timeProvider)
    : IRequestHandler<UploadSchoolSignatureCommand, Result<SchoolImageDto>>
{
    /// <inheritdoc />
    public Task<Result<SchoolImageDto>> HandleAsync(UploadSchoolSignatureCommand request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        return SchoolImageUploadSupport.UploadAsync(
            processor.ProcessSignature(request.FileBytes.ToArray()),
            SchoolImageKind.Signature,
            "settings.identity.signature_uploaded",
            static profile => profile.CurrentSignatureGroupId,
            static (profile, groupId) => profile.SetCurrentSignature(groupId),
            store, schoolImages, schoolProfileRepository, accounts, currentUser, auditSink, timeProvider,
            cancellationToken);
    }
}

/// <summary>
/// Shared upload/repoint/audit sequence for both logo and signature uploads (TASK-0005b stage B2).
/// Never deletes the previous asset (orchestrator design, 2026-09-21: assets are immutable) — the
/// audit before/after metadata carries only the upload GROUP id, never bytes.
/// </summary>
internal static class SchoolImageUploadSupport
{
    private const string SchoolProfileEntityType = "school_profile";

    public static async Task<Result<SchoolImageDto>> UploadAsync(
        Result<ProcessedSchoolImage> processed,
        SchoolImageKind kind,
        string auditAction,
        Func<SchoolProfile, Guid?> getCurrentGroupId,
        Action<SchoolProfile, Guid> setCurrentGroupId,
        ISchoolImageStore store,
        ISchoolImageRepository schoolImages,
        ISchoolProfileRepository schoolProfileRepository,
        IAdminAccountRepository accounts,
        ICurrentUser currentUser,
        ISystemAuditSink auditSink,
        TimeProvider timeProvider,
        CancellationToken cancellationToken)
    {
        if (processed.IsFailure)
        {
            return Result.Failure<SchoolImageDto>(processed.Error);
        }

        var profile = await schoolProfileRepository.GetTrackedSingletonAsync(cancellationToken).ConfigureAwait(false);
        var beforeGroupId = getCurrentGroupId(profile);
        var uploadGroupId = Guid.CreateVersion7();

        var images = new List<SchoolImage>(processed.Value.Renditions.Count);
        foreach (var rendition in processed.Value.Renditions)
        {
            var assetId = await store.PutAsync(rendition.Bytes, rendition.ContentType, cancellationToken).ConfigureAwait(false);
            images.Add(SchoolImage.Create(
                Guid.CreateVersion7(), kind, ToDomainSizeVariant(rendition.SizeVariant), uploadGroupId, assetId,
                rendition.WidthPixels, rendition.HeightPixels, rendition.ContentType));
        }

        await schoolImages.AddRangeAsync(images, cancellationToken).ConfigureAwait(false);
        setCurrentGroupId(profile, uploadGroupId);

        var now = timeProvider.GetUtcNow();
        var actorName = await ResolveActorNameAsync(currentUser, accounts, cancellationToken).ConfigureAwait(false);

        await auditSink.RecordAsync(
            auditAction,
            SchoolProfileEntityType,
            profile.Id.ToString("D", CultureInfo.InvariantCulture),
            metadata: new Dictionary<string, object?>(StringComparer.Ordinal) { ["uploadGroupId"] = uploadGroupId },
            actorAdminId: currentUser.UserId,
            cancellationToken,
            beforeMetadata: beforeGroupId is null
                ? null
                : new Dictionary<string, object?>(StringComparer.Ordinal) { ["uploadGroupId"] = beforeGroupId })
            .ConfigureAwait(false);

        var original = processed.Value.Original;
        return Result.Success(new SchoolImageDto(original.WidthPixels, original.HeightPixels, now, actorName));
    }

    /// <summary>Maps the processor's own size-variant enum to the entity's (see the Domain enum's remarks).</summary>
    private static DomainSizeVariant ToDomainSizeVariant(AppRendition variant) => variant switch
    {
        AppRendition.Original => DomainSizeVariant.Original,
        AppRendition.Size200 => DomainSizeVariant.Size200,
        AppRendition.Size64 => DomainSizeVariant.Size64,
        _ => throw new ArgumentOutOfRangeException(nameof(variant), variant, "Unknown school-image size variant."),
    };

    private static async Task<string?> ResolveActorNameAsync(
        ICurrentUser currentUser, IAdminAccountRepository accounts, CancellationToken cancellationToken)
    {
        if (currentUser.UserId is not { } actorIdText || !Guid.TryParse(actorIdText, out var actorId))
        {
            return null;
        }

        var account = await accounts.FindReadOnlyByIdAsync(actorId, cancellationToken).ConfigureAwait(false);
        return account?.StaffName;
    }
}
