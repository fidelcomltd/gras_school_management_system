using System.Text.RegularExpressions;
using FluentValidation;
using SchoolManagement.Application.Abstractions.Messaging;
using SchoolManagement.Application.Abstractions.Results;
using SchoolManagement.Application.Abstractions.Settings;
using SchoolManagement.Application.Pins;
using SchoolManagement.Domain.Common;
using DomainSizeVariant = SchoolManagement.Domain.Settings.SchoolImageSizeVariant;

namespace SchoolManagement.Application.Portal;

/// <summary>The download, or the reason there is none (the view's status).</summary>
/// <param name="View">The same outcome the on-screen page would have had.</param>
/// <param name="File">Present when shown.</param>
public sealed record PortalResultPdf(PortalResultView View, PdfFile? File);

/// <summary>Spec 6.9.9 <c>GET /portal/result/{term_id}/pdf</c>. Counts no additional use (6.9.2 step 6).</summary>
/// <param name="Tokens">From the cookie.</param>
/// <param name="UseId">Which open session.</param>
/// <param name="TermId">The term.</param>
public sealed record GetPortalResultPdfQuery(IReadOnlyList<string> Tokens, Guid? UseId, Guid TermId) : IQuery<Result<PortalResultPdf>>;

/// <summary>At most five tokens.</summary>
internal sealed class GetPortalResultPdfQueryValidator : AbstractValidator<GetPortalResultPdfQuery>
{
    public GetPortalResultPdfQueryValidator() => RuleFor(query => query.Tokens.Count).LessThanOrEqualTo(PortalSessionRules.MaxSessionsPerDevice);
}

/// <summary>
/// Handles <see cref="GetPortalResultPdfQuery"/>. The session and publication checks are the on-screen handler's, so the
/// two can never disagree about who may see what. The file is cached per (result set, pupil, revision).
/// </summary>
internal sealed partial class GetPortalResultPdfHandler(
    IRequestHandler<GetPortalResultQuery, Result<PortalResultView>> results,
    IResultPdfCache cache,
    IResultSheetPdfRenderer renderer,
    IResultVerificationReader verifications,
    ISchoolImageRepository schoolImages,
    ISchoolImageStore imageStore,
    TimeProvider timeProvider)
    : IRequestHandler<GetPortalResultPdfQuery, Result<PortalResultPdf>>
{
    /// <inheritdoc />
    public async Task<Result<PortalResultPdf>> HandleAsync(GetPortalResultPdfQuery request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var outcome = await results.HandleAsync(new GetPortalResultQuery(request.Tokens, request.UseId, request.TermId, ForPdf: true), cancellationToken)
            .ConfigureAwait(false);
        if (outcome.IsFailure)
        {
            return Result.Failure<PortalResultPdf>(outcome.Error);
        }

        var view = outcome.Value;
        if (view is not { Status: PortalResultStatus.Shown, Sheet: { } sheet, PdfKey: { } key })
        {
            return Result.Success(new PortalResultPdf(view, null));
        }

        var fileName = FileName(sheet.RegistrationNumber, sheet.TermName, sheet.AcademicYear);
        if (await cache.GetAsync(key, cancellationToken).ConfigureAwait(false) is { } cached)
        {
            return Result.Success(new PortalResultPdf(view, new PdfFile(fileName, cached)));
        }

        var token = await verifications.FindTokenAsync(key.ResultSetId, key.PupilId, key.RevisionNumber, cancellationToken).ConfigureAwait(false);
        var extras = new ResultSheetPdfExtras(
            await ReadImageAsync(sheet.LogoUploadGroupId, DomainSizeVariant.Size200, cancellationToken).ConfigureAwait(false),
            await ReadImageAsync(sheet.SignatureUploadGroupId, DomainSizeVariant.Original, cancellationToken).ConfigureAwait(false),
            token,
            token is null ? null : renderer.VerificationUrl(token),
            timeProvider.GetUtcNow());
        var pdf = renderer.Render(sheet, extras);
        await cache.SetAsync(key, pdf, cancellationToken).ConfigureAwait(false);
        return Result.Success(new PortalResultPdf(view, new PdfFile(fileName, pdf)));
    }

    /// <summary>6.9.6: e.g. <c>GRAS-2026-0041_First-Term_2026-2027.pdf</c>, safe on Android and Windows.</summary>
    internal static string FileName(string registrationNumber, string termName, string sessionName) =>
        $"{Safe(registrationNumber)}_{Safe(termName)}_{Safe(sessionName)}.pdf";

    private static string Safe(string value) => UnsafeRun().Replace(value, "-").Trim('-') is { Length: > 0 } safe ? safe : "result";

    [GeneratedRegex("[^A-Za-z0-9]+")]
    private static partial Regex UnsafeRun();

    // The images named by the snapshot's upload groups, so a sheet reprinted later shows what was published.
    private async Task<ReadOnlyMemory<byte>> ReadImageAsync(Guid? groupId, DomainSizeVariant variant, CancellationToken cancellationToken)
    {
        if (groupId is not { } id
            || await schoolImages.FindRenditionAsync(id, variant, cancellationToken).ConfigureAwait(false) is not { } rendition)
        {
            return ReadOnlyMemory<byte>.Empty;
        }

        var stream = await imageStore.OpenAsync(rendition.AssetId, cancellationToken).ConfigureAwait(false);
        await using (stream.ConfigureAwait(false))
        {
            using var buffer = new MemoryStream();
            await stream.CopyToAsync(buffer, cancellationToken).ConfigureAwait(false);
            return buffer.ToArray();
        }
    }
}
