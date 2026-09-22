using Microsoft.AspNetCore.Http.Features;
using Microsoft.AspNetCore.Http.Metadata;

namespace SchoolManagement.Api.Http;

/// <summary>
/// Applies an endpoint's own <see cref="IRequestSizeLimitMetadata"/> — attached with the ready-made
/// <c>Microsoft.AspNetCore.Mvc.RequestSizeLimitAttribute</c>, which already implements the interface,
/// so no bespoke marker type is needed here — to Kestrel's own body-size enforcement, BEFORE the
/// endpoint's model binding reads the request body.
/// </summary>
/// <remarks>
/// TASK-0005b: the upload routes need a tighter per-route cap (the processor's own file cap plus
/// 64 KB of multipart overhead) than the server-wide default. A minimal-API endpoint FILTER cannot
/// enforce this — filters run AFTER argument binding has already buffered the whole body (including
/// an <c>IFormFile</c>), so by the time one runs the oversized body was already read. Only real
/// middleware, executed before the endpoint's own delegate, can set the limit in time. Placed after
/// <c>UseAuthorization()</c> in <c>Program.cs</c>; <c>context.GetEndpoint()</c> already resolves
/// there because routing runs implicitly before the first middleware (no explicit
/// <c>UseRouting()</c> call in this pipeline).
/// </remarks>
internal sealed class EndpointRequestSizeLimitMiddleware(RequestDelegate next)
{
    /// <summary>Processes the request.</summary>
    public async Task InvokeAsync(HttpContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var limit = context.GetEndpoint()?.Metadata.GetMetadata<IRequestSizeLimitMetadata>()?.MaxRequestBodySize;

        if (limit is { } maxBytes)
        {
            var feature = context.Features.Get<IHttpMaxRequestBodySizeFeature>();

            if (feature is { IsReadOnly: false })
            {
                feature.MaxRequestBodySize = maxBytes;
            }
        }

        await next(context).ConfigureAwait(false);
    }
}
