using FluentValidation;
using SchoolManagement.Application.Abstractions.Messaging;
using SchoolManagement.Application.Abstractions.Portal;
using SchoolManagement.Application.Abstractions.Results;
using SchoolManagement.Application.Abstractions.Settings;
using SchoolManagement.Application.Pins;
using SchoolManagement.Application.Results.Sheets;
using SchoolManagement.Domain.Common;
using DomainSizeVariant = SchoolManagement.Domain.Settings.SchoolImageSizeVariant;

namespace SchoolManagement.Application.Portal;

/// <summary>The annual page's outcome (spec 6.7.10: present but greyed until computed).</summary>
/// <param name="Status">Shown, SessionEnded or NotReleased.</param>
/// <param name="Sheet">Present when shown.</param>
/// <param name="UseId">The viewing session, for the page's links.</param>
/// <param name="File">The PDF, when asked for and shown.</param>
public sealed record PortalAnnualView(PortalResultStatus Status, AnnualSheet? Sheet = null, Guid? UseId = null, PdfFile? File = null);

/// <summary>Spec 6.9.9 <c>GET /portal/annual</c>, as a page or its PDF. Spends no use.</summary>
/// <param name="Tokens">From the cookie.</param>
/// <param name="UseId">Which open session.</param>
/// <param name="SessionId">The academic session.</param>
/// <param name="AsPdf">Render the A4 document.</param>
public sealed record GetPortalAnnualQuery(IReadOnlyList<string> Tokens, Guid? UseId, Guid SessionId, bool AsPdf = false) : IQuery<Result<PortalAnnualView>>;

/// <summary>At most five tokens.</summary>
internal sealed class GetPortalAnnualQueryValidator : AbstractValidator<GetPortalAnnualQuery>
{
    public GetPortalAnnualQueryValidator() => RuleFor(query => query.Tokens.Count).LessThanOrEqualTo(PortalSessionRules.MaxSessionsPerDevice);
}

/// <summary>Handles <see cref="GetPortalAnnualQuery"/>.</summary>
internal sealed class GetPortalAnnualHandler(
    IPortalRepository portal,
    IAnnualSheetReader sheets,
    IResultPdfCache cache,
    IResultSheetPdfRenderer renderer,
    ISchoolImageRepository schoolImages,
    ISchoolImageStore imageStore,
    TimeProvider timeProvider)
    : IRequestHandler<GetPortalAnnualQuery, Result<PortalAnnualView>>
{
    /// <inheritdoc />
    public async Task<Result<PortalAnnualView>> HandleAsync(GetPortalAnnualQuery request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        if (await PortalSessionResolver.ResolveAsync(portal, request.Tokens, request.UseId, timeProvider.GetUtcNow(), cancellationToken).ConfigureAwait(false)
            is not { } session)
        {
            return Result.Success(new PortalAnnualView(PortalResultStatus.SessionEnded));
        }

        var data = await sheets.ReadAsync(session.Use.PupilId, request.SessionId, cancellationToken).ConfigureAwait(false);
        if (data is null || AnnualSheetBuilder.Build(data) is not { } sheet)
        {
            return Result.Success(new PortalAnnualView(PortalResultStatus.NotReleased, UseId: session.Use.Id));
        }

        if (!request.AsPdf)
        {
            return Result.Success(new PortalAnnualView(PortalResultStatus.Shown, sheet, session.Use.Id));
        }

        // A recomputation writes a new row id, which is the cache invalidation.
        var key = new ResultPdfKey(data.Row.Id, data.Row.PupilId, 0);
        var fileName = GetPortalResultPdfHandler.FileName(sheet.RegistrationNumber, "Annual", sheet.AcademicYear);
        if (await cache.GetAsync(key, cancellationToken).ConfigureAwait(false) is not { } pdf)
        {
            pdf = renderer.RenderAnnual(sheet, new ResultSheetPdfExtras(
                await ReadImageAsync(sheet.LogoUploadGroupId, DomainSizeVariant.Size200, cancellationToken).ConfigureAwait(false),
                await ReadImageAsync(sheet.SignatureUploadGroupId, DomainSizeVariant.Original, cancellationToken).ConfigureAwait(false),
                null,
                null,
                timeProvider.GetUtcNow()));
            await cache.SetAsync(key, pdf, cancellationToken).ConfigureAwait(false);
        }

        return Result.Success(new PortalAnnualView(PortalResultStatus.Shown, sheet, session.Use.Id, new PdfFile(fileName, pdf)));
    }

    private Task<ReadOnlyMemory<byte>> ReadImageAsync(Guid? groupId, DomainSizeVariant variant, CancellationToken cancellationToken) =>
        SnapshotImages.ReadAsync(schoolImages, imageStore, groupId, variant, cancellationToken);
}
