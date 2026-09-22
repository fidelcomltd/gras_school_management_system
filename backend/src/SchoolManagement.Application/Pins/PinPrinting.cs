using FluentValidation;
using SchoolManagement.Application.Abstractions.Audit;
using SchoolManagement.Application.Abstractions.Identity;
using SchoolManagement.Application.Abstractions.Messaging;
using SchoolManagement.Application.Abstractions.Pins;
using SchoolManagement.Application.Abstractions.Sessions;
using SchoolManagement.Application.Abstractions.Settings;
using SchoolManagement.Application.Settings;
using SchoolManagement.Domain.Common;
using SchoolManagement.Domain.Pins;
using SchoolManagement.Domain.Security;
using DomainSizeVariant = SchoolManagement.Domain.Settings.SchoolImageSizeVariant;

namespace SchoolManagement.Application.Pins;

/// <summary>A rendered PDF.</summary>
/// <param name="FileName">For <c>Content-Disposition</c>.</param>
/// <param name="Content">The PDF bytes.</param>
public sealed record PdfFile(string FileName, ReadOnlyMemory<byte> Content);

/// <summary>
/// Renders a batch's slips (spec 6.8.9 step 6, 6.8.13). This is the only place a pin value is revealed, and only as a
/// print artefact. It moves the batch to Printed and is audited. After the purge date it is 410.
/// </summary>
/// <param name="BatchId">From the route.</param>
public sealed record PrintPinBatchCommand(Guid BatchId) : ICommand<Result<PdfFile>>;

/// <summary>No input beyond the route.</summary>
internal sealed class PrintPinBatchCommandValidator : AbstractValidator<PrintPinBatchCommand>;

/// <summary>The numbered hand-over sheet: prefixes only (spec 6.8.9 step 7).</summary>
/// <param name="BatchId">From the route.</param>
public sealed record GetPinDistributionListQuery(Guid BatchId) : IQuery<Result<PdfFile>>;

/// <summary>No input beyond the route.</summary>
internal sealed class GetPinDistributionListQueryValidator : AbstractValidator<GetPinDistributionListQuery>;

/// <summary>Handles <see cref="PrintPinBatchCommand"/>.</summary>
internal sealed class PrintPinBatchHandler(
    IPinBatchRepository batches,
    IPinSecrets secrets,
    IPinSlipRenderer renderer,
    IAcademicSessionRepository sessions,
    ISchoolProfileRepository schoolProfile,
    ISchoolImageRepository schoolImages,
    ISchoolImageStore imageStore,
    ICurrentUser currentUser,
    ISystemAuditSink auditSink,
    TimeProvider timeProvider)
    : IRequestHandler<PrintPinBatchCommand, Result<PdfFile>>
{
    /// <inheritdoc />
    public async Task<Result<PdfFile>> HandleAsync(PrintPinBatchCommand request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var batch = await batches.FindTrackedAsync(request.BatchId, cancellationToken).ConfigureAwait(false);
        if (batch is null)
        {
            return Result.Failure<PdfFile>(Error.NotFound("pin_batch.not_found", "No pin batch was found with that id."));
        }

        if (batch.State == PinBatchState.Revoked)
        {
            return Result.Failure<PdfFile>(Error.Conflict("pin_batch.revoked", "This batch is revoked. Its pins no longer work, so they are not printed."));
        }

        var pins = await batches.ListPinsAsync(batch.Id, tracked: false, cancellationToken).ConfigureAwait(false);
        if (timeProvider.GetUtcNow() >= batch.PlaintextPurgeAtUtc || pins.Any(pin => pin.Ciphertext is null))
        {
            return Result.Failure<PdfFile>(Error.Gone(
                "pin_batch.plaintext_purged", "Pin values are no longer stored. Generate a new batch to replace any lost slips."));
        }

        var (shortName, sessionName) = await HeadingAsync(batch, cancellationToken).ConfigureAwait(false);
        var logo = await LogoAsync(cancellationToken).ConfigureAwait(false);
        var slips = pins
            .Where(pin => pin.State != PinState.Revoked)
            .Select(pin => new PinSlip(PinValue.Format(secrets.Reveal(pin.Ciphertext!)), pin.MaxUses))
            .ToList();
        var pdf = renderer.RenderSlips(new PinSlipSheet(shortName, logo, sessionName, batch.Name, slips));

        var before = batch.State;
        batch.MarkPrinted();
        await auditSink.RecordAsync(
            Privileges.Pin.Print,
            "pin_batch",
            PinMapper.Id(batch.Id),
            metadata: new Dictionary<string, object?>(StringComparer.Ordinal) { ["state"] = batch.State.ToString(), ["slipsPrinted"] = slips.Count },
            actorAdminId: currentUser.UserId,
            cancellationToken,
            beforeMetadata: new Dictionary<string, object?>(StringComparer.Ordinal) { ["state"] = before.ToString() }).ConfigureAwait(false);

        return Result.Success(new PdfFile(FileNameFor(batch.Name, "pins"), pdf));
    }

    private async Task<(string ShortName, string SessionName)> HeadingAsync(PinBatch batch, CancellationToken cancellationToken)
    {
        var profile = await schoolProfile.GetReadOnlySingletonAsync(cancellationToken).ConfigureAwait(false);
        var session = await sessions.FindReadOnlyByIdAsync(batch.SessionId, cancellationToken).ConfigureAwait(false);
        return (string.IsNullOrWhiteSpace(profile.ShortName) ? profile.SchoolName : profile.ShortName, session?.Name ?? string.Empty);
    }

    private async Task<ReadOnlyMemory<byte>> LogoAsync(CancellationToken cancellationToken)
    {
        var profile = await schoolProfile.GetReadOnlySingletonAsync(cancellationToken).ConfigureAwait(false);
        if (profile.CurrentLogoGroupId is not { } groupId)
        {
            return ReadOnlyMemory<byte>.Empty;
        }

        var rendition = await schoolImages.FindRenditionAsync(groupId, DomainSizeVariant.Size200, cancellationToken).ConfigureAwait(false);
        if (rendition is null)
        {
            return ReadOnlyMemory<byte>.Empty;
        }

        await using var stream = await imageStore.OpenAsync(rendition.AssetId, cancellationToken).ConfigureAwait(false);
        using var buffer = new MemoryStream();
        await stream.CopyToAsync(buffer, cancellationToken).ConfigureAwait(false);
        return buffer.ToArray();
    }

    internal static string FileNameFor(string batchName, string suffix)
    {
        var safe = System.Text.RegularExpressions.Regex.Replace(batchName, "[^A-Za-z0-9]+", "-").Trim('-');
        return $"{safe}-{suffix}.pdf";
    }
}

/// <summary>Handles <see cref="GetPinDistributionListQuery"/>. Prefixes only, so it works after the purge too.</summary>
internal sealed class GetPinDistributionListHandler(
    IPinBatchRepository batches,
    IPinSlipRenderer renderer,
    IAcademicSessionRepository sessions,
    ISchoolProfileRepository schoolProfile)
    : IRequestHandler<GetPinDistributionListQuery, Result<PdfFile>>
{
    /// <inheritdoc />
    public async Task<Result<PdfFile>> HandleAsync(GetPinDistributionListQuery request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var batch = await batches.FindReadOnlyAsync(request.BatchId, cancellationToken).ConfigureAwait(false);
        if (batch is null)
        {
            return Result.Failure<PdfFile>(Error.NotFound("pin_batch.not_found", "No pin batch was found with that id."));
        }

        var pins = await batches.ListPinsAsync(batch.Id, tracked: false, cancellationToken).ConfigureAwait(false);
        var profile = await schoolProfile.GetReadOnlySingletonAsync(cancellationToken).ConfigureAwait(false);
        var session = await sessions.FindReadOnlyByIdAsync(batch.SessionId, cancellationToken).ConfigureAwait(false);
        var shortName = string.IsNullOrWhiteSpace(profile.ShortName) ? profile.SchoolName : profile.ShortName;

        var pdf = renderer.RenderDistributionList(new DistributionSheet(
            shortName, session?.Name ?? string.Empty, batch.Name, pins.Where(pin => pin.State != PinState.Revoked).Select(pin => pin.Prefix).ToList()));
        return Result.Success(new PdfFile(PrintPinBatchHandler.FileNameFor(batch.Name, "distribution-list"), pdf));
    }
}
