using System.Globalization;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using SchoolManagement.Application.Abstractions.Results;
using SchoolManagement.Infrastructure.Pins;

namespace SchoolManagement.Infrastructure.Results;

/// <summary>
/// Result PDFs on local disk (spec 6.9.6), one file per (result set, pupil, revision). A republication changes the
/// revision, so an old file is simply never asked for again. Writes go to a temporary name and are renamed into place,
/// so a reader never sees half a file. Any IO failure degrades to "not cached".
/// </summary>
internal sealed partial class DiskResultPdfCache(IOptions<PortalOptions> options, ILogger<DiskResultPdfCache> logger) : IResultPdfCache
{
    private readonly string _directory = string.IsNullOrWhiteSpace(options.Value.PdfCacheDirectory)
        ? Path.Combine(Path.GetTempPath(), "gras-result-pdf")
        : options.Value.PdfCacheDirectory;

    public async Task<byte[]?> GetAsync(ResultPdfKey key, CancellationToken cancellationToken)
    {
        var path = PathFor(key);
        try
        {
            return File.Exists(path) ? await File.ReadAllBytesAsync(path, cancellationToken).ConfigureAwait(false) : null;
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            LogCacheFailed(logger, path, exception);
            return null;
        }
    }

    public async Task SetAsync(ResultPdfKey key, byte[] pdf, CancellationToken cancellationToken)
    {
        var path = PathFor(key);
        var temporary = $"{path}.{Guid.NewGuid():N}.tmp";
        try
        {
            Directory.CreateDirectory(_directory);
            await File.WriteAllBytesAsync(temporary, pdf, cancellationToken).ConfigureAwait(false);
            File.Move(temporary, path, overwrite: true);
        }
        catch (Exception exception) when (exception is IOException or UnauthorizedAccessException)
        {
            LogCacheFailed(logger, path, exception);
            File.Delete(temporary);
        }
    }

    private string PathFor(ResultPdfKey key) =>
        Path.Combine(_directory, string.Create(CultureInfo.InvariantCulture, $"{key.ResultSetId:N}_{key.PupilId:N}_r{key.RevisionNumber}.pdf"));

    [LoggerMessage(Level = LogLevel.Warning, Message = "Result PDF cache unavailable at {Path}; serving uncached.")]
    private static partial void LogCacheFailed(ILogger logger, string path, Exception exception);
}
