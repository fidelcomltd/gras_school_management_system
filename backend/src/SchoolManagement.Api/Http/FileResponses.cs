using Microsoft.AspNetCore.Http.HttpResults;
using SchoolManagement.Application.Settings;

namespace SchoolManagement.Api.Http;

/// <summary>Uploads and stored files (spec 9.6), shared by every endpoint that takes or serves one.</summary>
internal static class FileResponses
{
    /// <summary>Bytes over a file's own cap that a multipart envelope's boundaries and headers may add.</summary>
    public const long MultipartOverheadBytes = 64 * 1024;

    /// <summary>
    /// Streams a stored file with spec 9.6's headers: <c>Content-Disposition</c> under a server-chosen name and
    /// <c>Cache-Control: private</c> (<c>nosniff</c> is added to every response by the security headers middleware).
    /// </summary>
    /// <param name="httpContext">The current request.</param>
    /// <param name="content">The opened file.</param>
    /// <param name="disposition"><c>inline</c> for an image shown on a page, <c>attachment</c> for a download.</param>
    /// <param name="cacheControl"><c>private</c>, or <c>no-store</c> for health data that must never reach a disk cache.</param>
    public static FileStreamHttpResult Serve(
        HttpContext httpContext, SchoolImageContent content, string disposition = "inline", string cacheControl = "private")
    {
        ArgumentNullException.ThrowIfNull(httpContext);
        ArgumentNullException.ThrowIfNull(content);
        httpContext.Response.Headers.CacheControl = cacheControl;
        httpContext.Response.Headers.ContentDisposition = $"{disposition}; filename={content.FileName}";
        return TypedResults.Stream(content.Content, content.ContentType);
    }
}
