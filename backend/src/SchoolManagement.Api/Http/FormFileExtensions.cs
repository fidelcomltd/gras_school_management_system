namespace SchoolManagement.Api.Http;

/// <summary>Reading an uploaded multipart file, shared by every upload endpoint.</summary>
internal static class FormFileExtensions
{
    /// <summary>The whole file. The route's <c>RequestSizeLimitAttribute</c> has already bounded it.</summary>
    public static async Task<byte[]> ReadAllBytesAsync(this IFormFile file, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(file);

        await using var stream = file.OpenReadStream();
        using var buffer = new MemoryStream();
        await stream.CopyToAsync(buffer, cancellationToken).ConfigureAwait(false);
        return buffer.ToArray();
    }
}
