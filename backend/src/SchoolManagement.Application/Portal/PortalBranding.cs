using FluentValidation;
using SchoolManagement.Application.Abstractions.Messaging;
using SchoolManagement.Application.Abstractions.Settings;
using SchoolManagement.Application.Settings;
using SchoolManagement.Domain.Common;
using DomainSizeVariant = SchoolManagement.Domain.Settings.SchoolImageSizeVariant;

namespace SchoolManagement.Application.Portal;

/// <summary>What the portal's landing page shows about the school (spec 6.9.2 step 1). Public information only.</summary>
/// <param name="SchoolName">Full name.</param>
/// <param name="HasLogo">Whether <c>/portal/logo</c> will return an image.</param>
public sealed record PortalBranding(string SchoolName, bool HasLogo);

/// <summary>The school name and whether a logo exists.</summary>
public sealed record GetPortalBrandingQuery : IQuery<Result<PortalBranding>>;

/// <summary>No input.</summary>
internal sealed class GetPortalBrandingQueryValidator : AbstractValidator<GetPortalBrandingQuery>;

/// <summary>The current logo's 200 px rendition, for the public portal page. The logo is public by nature.</summary>
public sealed record GetPortalLogoQuery : IQuery<Result<SchoolImageContent>>;

/// <summary>No input.</summary>
internal sealed class GetPortalLogoQueryValidator : AbstractValidator<GetPortalLogoQuery>;

/// <summary>Handles <see cref="GetPortalBrandingQuery"/>.</summary>
internal sealed class GetPortalBrandingHandler(ISchoolProfileRepository schoolProfile) : IRequestHandler<GetPortalBrandingQuery, Result<PortalBranding>>
{
    /// <inheritdoc />
    public async Task<Result<PortalBranding>> HandleAsync(GetPortalBrandingQuery request, CancellationToken cancellationToken)
    {
        var profile = await schoolProfile.GetReadOnlySingletonAsync(cancellationToken).ConfigureAwait(false);
        var name = string.IsNullOrWhiteSpace(profile.SchoolName) ? "School results" : profile.SchoolName;
        return Result.Success(new PortalBranding(name, profile.CurrentLogoGroupId is not null));
    }
}

/// <summary>Handles <see cref="GetPortalLogoQuery"/>.</summary>
internal sealed class GetPortalLogoHandler(ISchoolProfileRepository schoolProfile, ISchoolImageRepository schoolImages, ISchoolImageStore store)
    : IRequestHandler<GetPortalLogoQuery, Result<SchoolImageContent>>
{
    /// <inheritdoc />
    public async Task<Result<SchoolImageContent>> HandleAsync(GetPortalLogoQuery request, CancellationToken cancellationToken)
    {
        var profile = await schoolProfile.GetReadOnlySingletonAsync(cancellationToken).ConfigureAwait(false);
        var rendition = profile.CurrentLogoGroupId is { } groupId
            ? await schoolImages.FindRenditionAsync(groupId, DomainSizeVariant.Size200, cancellationToken).ConfigureAwait(false)
            : null;
        if (rendition is null)
        {
            return Result.Failure<SchoolImageContent>(Error.NotFound(GetSchoolImageQueryHandler.NotUploadedErrorCode, "No school logo has been uploaded."));
        }

        var content = await store.OpenAsync(rendition.AssetId, cancellationToken).ConfigureAwait(false);
        return Result.Success(new SchoolImageContent(content, rendition.ContentType, "logo.png"));
    }
}
