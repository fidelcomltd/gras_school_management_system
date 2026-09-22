using SchoolManagement.Application.Results.Sheets;

namespace SchoolManagement.Application.Abstractions.Results;

/// <summary>Everything the A4 sheet prints beyond the <see cref="ResultSheet"/> itself.</summary>
/// <param name="Logo">The logo's 200 px rendition from the snapshot's upload group, or empty.</param>
/// <param name="Signature">The head teacher's signature, or empty for a ruled line (C.6).</param>
/// <param name="VerificationToken">Unformatted, or null when none was issued (the footer then omits the marks).</param>
/// <param name="VerificationUrl">What the QR code encodes, or null with the token.</param>
/// <param name="PrintedAt">The C.7 generated timestamp.</param>
public sealed record ResultSheetPdfExtras(
    ReadOnlyMemory<byte> Logo,
    ReadOnlyMemory<byte> Signature,
    string? VerificationToken,
    Uri? VerificationUrl,
    DateTimeOffset PrintedAt);

/// <summary>Renders the A4 result sheet (spec 6.9.6, Appendices C, E, F).</summary>
public interface IResultSheetPdfRenderer
{
    /// <summary>The verification address for a token, from <c>Portal:PublicUrl</c>; null when unset.</summary>
    Uri? VerificationUrl(string token);

    /// <summary>The PDF bytes.</summary>
    byte[] Render(ResultSheet sheet, ResultSheetPdfExtras extras);
}

/// <summary>Identifies one cached PDF: republication changes the revision, which is the invalidation (6.9.6).</summary>
/// <param name="ResultSetId">The result set.</param>
/// <param name="PupilId">The pupil.</param>
/// <param name="RevisionNumber">The revision.</param>
public sealed record ResultPdfKey(Guid ResultSetId, Guid PupilId, int RevisionNumber);

/// <summary>Generated sheets, so the second download of the same sheet is a file read.</summary>
public interface IResultPdfCache
{
    /// <summary>The cached bytes, or null.</summary>
    Task<byte[]?> GetAsync(ResultPdfKey key, CancellationToken cancellationToken);

    /// <summary>Stores <paramref name="pdf"/>. A failure to cache never fails the download.</summary>
    Task SetAsync(ResultPdfKey key, byte[] pdf, CancellationToken cancellationToken);
}
