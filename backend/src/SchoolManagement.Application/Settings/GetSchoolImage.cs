using FluentValidation;
using SchoolManagement.Application.Abstractions.Messaging;
using SchoolManagement.Application.Abstractions.Settings;
using SchoolManagement.Domain.Common;
using SchoolManagement.Domain.Settings;
using DomainSizeVariant = SchoolManagement.Domain.Settings.SchoolImageSizeVariant;

namespace SchoolManagement.Application.Settings;

/// <summary>The stored asset behind one rendition (TASK-0005b stage C).</summary>
/// <param name="AssetId">The store's opaque id.</param>
/// <param name="ContentType"><c>image/png</c> or <c>image/jpeg</c>.</param>
public sealed record SchoolImageRenditionRef(string AssetId, string ContentType);

/// <summary>An image ready to stream: the bytes, their type and a download name.</summary>
/// <param name="Content">Opened from <see cref="ISchoolImageStore"/>; the endpoint disposes it.</param>
/// <param name="ContentType"><c>image/png</c> or <c>image/jpeg</c>.</param>
/// <param name="FileName">For <c>Content-Disposition</c>, e.g. <c>logo-200.png</c>.</param>
public sealed record SchoolImageContent(Stream Content, string ContentType, string FileName);

/// <summary>Reads the current logo or signature rendition (spec 9.6: served through a privilege-checked endpoint).</summary>
/// <param name="Kind">Logo or signature.</param>
/// <param name="SizeVariant">Always <see cref="DomainSizeVariant.Original"/> for a signature.</param>
public sealed record GetSchoolImageQuery(SchoolImageKind Kind, DomainSizeVariant SizeVariant) : IQuery<Result<SchoolImageContent>>;

/// <summary>A signature has only its original rendition.</summary>
internal sealed class GetSchoolImageQueryValidator : AbstractValidator<GetSchoolImageQuery>
{
    public GetSchoolImageQueryValidator() =>
        RuleFor(query => query.SizeVariant)
            .Equal(DomainSizeVariant.Original)
            .When(query => query.Kind == SchoolImageKind.Signature)
            .WithMessage("A signature is stored at its original size only.");
}

/// <summary>Handles <see cref="GetSchoolImageQuery"/>.</summary>
internal sealed class GetSchoolImageQueryHandler(
    ISchoolProfileRepository schoolProfileRepository,
    ISchoolImageRepository schoolImages,
    ISchoolImageStore store)
    : IRequestHandler<GetSchoolImageQuery, Result<SchoolImageContent>>
{
    /// <summary>Returned when nothing has been uploaded yet for this kind (or this size).</summary>
    public const string NotUploadedErrorCode = "school_image.not_uploaded";

    /// <inheritdoc />
    public async Task<Result<SchoolImageContent>> HandleAsync(GetSchoolImageQuery request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var profile = await schoolProfileRepository.GetReadOnlySingletonAsync(cancellationToken).ConfigureAwait(false);
        var groupId = request.Kind == SchoolImageKind.Logo ? profile.CurrentLogoGroupId : profile.CurrentSignatureGroupId;

        var rendition = groupId is { } id
            ? await schoolImages.FindRenditionAsync(id, request.SizeVariant, cancellationToken).ConfigureAwait(false)
            : null;

        if (rendition is null)
        {
            var what = request.Kind == SchoolImageKind.Logo ? "school logo" : "head teacher's signature";
            return Result.Failure<SchoolImageContent>(Error.NotFound(NotUploadedErrorCode, $"No {what} has been uploaded."));
        }

        var content = await store.OpenAsync(rendition.AssetId, cancellationToken).ConfigureAwait(false);
        return Result.Success(new SchoolImageContent(content, rendition.ContentType, FileNameFor(request, rendition.ContentType)));
    }

    private static string FileNameFor(GetSchoolImageQuery request, string contentType)
    {
        var stem = request.Kind == SchoolImageKind.Logo ? "logo" : "signature";
        var size = request.SizeVariant switch
        {
            DomainSizeVariant.Size200 => "-200",
            DomainSizeVariant.Size64 => "-64",
            _ => string.Empty,
        };
        var extension = contentType == "image/png" ? ".png" : ".jpg";
        return stem + size + extension;
    }
}
