using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Authorization.Policy;
using SchoolManagement.Api.Http;

namespace SchoolManagement.Api.Security;

/// <summary>
/// Turns a forbidden authorization result into an RFC 9457 <c>ProblemDetails</c> body, matching
/// every other error response in the API.
/// </summary>
/// <remarks>
/// <para>
/// The default <see cref="AuthorizationMiddlewareResultHandler"/> sets the status code and nothing
/// else — the response body is empty. Spec 9.2 requires "a body containing nothing but a generic
/// message," which an empty body technically satisfies but which is inconsistent with the error
/// shape every other endpoint returns. This wraps the default handler, replacing its behaviour only
/// for the forbidden case.
/// </para>
/// <para>
/// DELIBERATELY GENERIC: the message never names the missing privilege or the target's existence
/// (spec 9.2: "They do not reveal whether the entity exists"). What privilege was checked and who
/// was denied is recorded by <c>IAuthorizationAuditSink</c> inside
/// <see cref="PrivilegeAuthorizationHandler"/>, not surfaced here.
/// </para>
/// <para>
/// The 401 (challenge) path is untouched — it still goes through the default handler via
/// <see cref="AuthenticationSetup.PlaceholderScheme"/>'s challenge behaviour, unchanged by this card.
/// </para>
/// </remarks>
internal sealed class ProblemDetailsAuthorizationMiddlewareResultHandler : IAuthorizationMiddlewareResultHandler
{
    private const string ErrorCode = "authorization.forbidden";

    private static readonly AuthorizationMiddlewareResultHandler Default = new();

    /// <inheritdoc />
    public async Task HandleAsync(
        RequestDelegate next,
        HttpContext context,
        AuthorizationPolicy policy,
        PolicyAuthorizationResult authorizeResult)
    {
        ArgumentNullException.ThrowIfNull(context);
        ArgumentNullException.ThrowIfNull(authorizeResult);

        if (!authorizeResult.Forbidden)
        {
            await Default.HandleAsync(next, context, policy, authorizeResult).ConfigureAwait(false);
            return;
        }

        context.Response.StatusCode = StatusCodes.Status403Forbidden;

        var problemDetailsService = context.RequestServices.GetRequiredService<IProblemDetailsService>();

        await problemDetailsService.TryWriteAsync(new ProblemDetailsContext
        {
            HttpContext = context,
            ProblemDetails =
            {
                Status = StatusCodes.Status403Forbidden,
                Title = "Access denied",
                Detail = "You do not have permission to perform this action.",
                Type = ApiProblem.ToTypeUrn(ErrorCode),
                Extensions = { ["errorCode"] = ErrorCode },
            },
        }).ConfigureAwait(false);
    }
}
