using FluentValidation;
using SchoolManagement.Application.Abstractions.Classes;
using SchoolManagement.Application.Abstractions.Messaging;
using SchoolManagement.Application.Abstractions.Results;
using SchoolManagement.Application.Abstractions.Sessions;
using SchoolManagement.Application.Abstractions.Settings;
using SchoolManagement.Application.Pins;
using SchoolManagement.Application.Portal;
using SchoolManagement.Domain.Common;
using SchoolManagement.Domain.Results;
using DomainSizeVariant = SchoolManagement.Domain.Settings.SchoolImageSizeVariant;

namespace SchoolManagement.Application.Results.Sheets;

/// <summary>
/// <c>GET /arms/{armId}/result-sheets/pdf?termId=&amp;pupilId=</c>: staff printing of a published arm's result sheets
/// (<c>result.print</c>). Without <paramref name="PupilId"/> the whole arm prints as one PDF, pupils in name order. Staff
/// sheets always show the outstanding-fee line (6.2.13). Not audited: it reproduces what is already published.
/// </summary>
/// <param name="ArmId">The arm, from the route.</param>
/// <param name="TermId">The term.</param>
/// <param name="PupilId">One pupil, or null or empty for the whole arm.</param>
public sealed record PrintResultSheetsQuery(Guid ArmId, string TermId, string? PupilId) : IQuery<Result<PdfFile>>;

/// <summary>Structural checks only.</summary>
internal sealed class PrintResultSheetsQueryValidator : AbstractValidator<PrintResultSheetsQuery>
{
    public PrintResultSheetsQueryValidator()
    {
        RuleFor(query => query.TermId).NotEmpty().Must(value => Guid.TryParse(value, out _))
            .WithMessage("TermId must be a valid identifier.");
        RuleFor(query => query.PupilId).Must(value => Guid.TryParse(value, out _))
            .When(query => !string.IsNullOrEmpty(query.PupilId))
            .WithMessage("PupilId must be a valid identifier.");
    }
}

/// <summary>
/// Handles <see cref="PrintResultSheetsQuery"/>. Reads through <see cref="IResultSheetReader.ReadSetAsync"/>, bound to this arm's
/// own result set, so a pupil who moved arm mid-term still prints from the arm that published their lines. Never touches
/// <see cref="IResultPdfCache"/>, which holds parent-view PDFs.
/// </summary>
internal sealed class PrintResultSheetsHandler(
    IArmRepository arms,
    ITermRepository terms,
    IResultSetRepository resultSets,
    IResultSheetReader reader,
    IResultSheetPdfRenderer renderer,
    IResultVerificationReader verifications,
    ISchoolImageRepository schoolImages,
    ISchoolImageStore imageStore,
    TimeProvider timeProvider)
    : IRequestHandler<PrintResultSheetsQuery, Result<PdfFile>>
{
    /// <inheritdoc />
    public async Task<Result<PdfFile>> HandleAsync(PrintResultSheetsQuery request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var termId = Guid.Parse(request.TermId);
        Guid? onlyPupil = string.IsNullOrEmpty(request.PupilId) ? null : Guid.Parse(request.PupilId);

        var arm = await arms.FindReadOnlyByIdAsync(request.ArmId, cancellationToken).ConfigureAwait(false);
        if (arm is null)
        {
            return Result.Failure<PdfFile>(Error.NotFound("arm.not_found", "No arm was found with that id."));
        }

        // A term from another session can never have a result set for this arm: not found, not "not yet published".
        var term = await terms.FindReadOnlyByIdAsync(termId, cancellationToken).ConfigureAwait(false);
        if (term is null || term.SessionId != arm.SessionId)
        {
            return Result.Failure<PdfFile>(Error.NotFound("term.not_found", "No term was found with that id in this arm's session."));
        }

        var resultSet = await resultSets.FindReadOnlyByArmTermAsync(request.ArmId, termId, cancellationToken).ConfigureAwait(false);
        if (resultSet is not { State: ResultSetState.Published })
        {
            return Result.Failure<PdfFile>(Error.Conflict(
                "result_set.not_published", "Result sheets can be printed only once the arm's results for this term are published."));
        }

        var sheets = (await reader.ReadSetAsync(resultSet.Id, onlyPupil, cancellationToken).ConfigureAwait(false))
            .Where(entry => entry.Value.Lines.Count > 0)
            .Select(entry => (PupilId: entry.Key, Sheet: ResultSheetBuilder.Build(entry.Value, forParent: false)))
            .Where(entry => entry.Sheet is not null)
            .Select(entry => (entry.PupilId, Sheet: entry.Sheet!))
            .OrderBy(entry => entry.Sheet.PupilName, StringComparer.OrdinalIgnoreCase)
            .ThenBy(entry => entry.Sheet.RegistrationNumber, StringComparer.Ordinal)
            .ToList();
        if (sheets.Count == 0)
        {
            return Result.Failure<PdfFile>(onlyPupil is null
                ? Error.NotFound("result_sheet.none", "No pupil in this arm has a result for this term.")
                : Error.NotFound("result_sheet.not_found", "This pupil has no result in this arm for this term."));
        }

        // Every sheet in one set shares the snapshot and the revision: the images and tokens are read once for all of them.
        var first = sheets[0].Sheet;
        var logo = await SnapshotImages.ReadAsync(schoolImages, imageStore, first.LogoUploadGroupId, DomainSizeVariant.Size200, cancellationToken)
            .ConfigureAwait(false);
        var signature = await SnapshotImages.ReadAsync(schoolImages, imageStore, first.SignatureUploadGroupId, DomainSizeVariant.Original, cancellationToken)
            .ConfigureAwait(false);
        var tokens = await verifications.FindTokensAsync(resultSet.Id, resultSet.RevisionNumber, cancellationToken).ConfigureAwait(false);
        var printedAt = timeProvider.GetUtcNow();

        var pages = sheets.ConvertAll(entry =>
        {
            var token = tokens.GetValueOrDefault(entry.PupilId);
            return (entry.Sheet, new ResultSheetPdfExtras(logo, signature, token, token is null ? null : renderer.VerificationUrl(token), printedAt));
        });

        var fileName = onlyPupil is null
            ? GetPortalResultPdfHandler.FileName(first.ClassName, first.TermName, first.AcademicYear)
            : GetPortalResultPdfHandler.FileName(first.RegistrationNumber, first.TermName, first.AcademicYear);
        return Result.Success(new PdfFile(fileName, renderer.RenderMany(pages)));
    }
}
