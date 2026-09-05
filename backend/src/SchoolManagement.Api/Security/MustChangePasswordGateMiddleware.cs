using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using SchoolManagement.Api.Http;

namespace SchoolManagement.Api.Security;

/// <summary>
/// Marks an endpoint exempt from <see cref="MustChangePasswordGateMiddleware"/>. Applied to
/// <c>POST /auth/password</c> and <c>GET /auth/me</c> — spec 6.1.6's own two named exceptions
/// (password change, logout) plus the fix in approved contract delta §2a (<c>me</c> must stay
/// reachable so the frontend can learn <c>mustChangePassword</c> on a hard reload). <c>sign-out</c>
/// needs no separate marker: it is already <c>.AllowAnonymous()</c>, which the middleware treats as
/// out of its scope entirely.
/// </summary>
internal sealed class PasswordChangeGateExemptMarker;

/// <summary>Attaches <see cref="PasswordChangeGateExemptMarker"/> to an endpoint.</summary>
internal static class PasswordChangeGateExemptionExtensions
{
    /// <summary>Exempts the endpoint from the must-change-password gate.</summary>
    public static TBuilder ExemptFromPasswordChangeGate<TBuilder>(this TBuilder builder)
        where TBuilder : IEndpointConventionBuilder
    {
        builder.Add(endpointBuilder => endpointBuilder.Metadata.Add(new PasswordChangeGateExemptMarker()));
        return builder;
    }
}

/// <summary>
/// Spec 6.1.6: "server middleware rejects every request except the password change endpoint and
/// logout while <c>must_change_password</c> is true." Approved contract delta §2a narrows this to
/// exactly three exemptions for this card: <c>POST /auth/password</c>, <c>POST /auth/sign-out</c>
/// (via its own <c>.AllowAnonymous()</c>), and <c>GET /auth/me</c>. Every OTHER protected endpoint —
/// <c>POST /auth/refresh</c> in this card, and every product endpoint TASK-0005 onward — gets
/// <c>403 auth.password_change_required</c> while the flag is true.
/// </summary>
/// <remarks>
/// Runs AFTER <c>UseAuthentication()</c> (needs the claim it reads) and BEFORE
/// <c>UseAuthorization()</c> (this is account state, not a privilege decision, so it does not belong
/// inside <c>PrivilegeAuthorizationHandler</c>). An anonymous request, or one whose endpoint declares
/// <see cref="IAllowAnonymous"/>, is entirely outside this gate's scope — anonymous endpoints have no
/// session to gate in the first place.
/// </remarks>
internal sealed class MustChangePasswordGateMiddleware(RequestDelegate next)
{
    private const string ErrorCode = "auth.password_change_required";

    /// <summary>Processes the request.</summary>
    public async Task InvokeAsync(HttpContext context)
    {
        ArgumentNullException.ThrowIfNull(context);

        var endpoint = context.GetEndpoint();

        if (endpoint is null ||
            endpoint.Metadata.GetMetadata<IAllowAnonymous>() is not null ||
            endpoint.Metadata.GetMetadata<PasswordChangeGateExemptMarker>() is not null)
        {
            await next(context).ConfigureAwait(false);
            return;
        }

        if (context.User.Identity?.IsAuthenticated == true)
        {
            var mustChangePassword = context.User.FindFirstValue(SessionClaimTypes.MustChangePassword);

            if (string.Equals(mustChangePassword, bool.TrueString, StringComparison.OrdinalIgnoreCase))
            {
                context.Response.StatusCode = StatusCodes.Status403Forbidden;

                var problemDetailsService = context.RequestServices.GetRequiredService<IProblemDetailsService>();

                await problemDetailsService.TryWriteAsync(new ProblemDetailsContext
                {
                    HttpContext = context,
                    ProblemDetails =
                    {
                        Status = StatusCodes.Status403Forbidden,
                        Title = "Access denied",
                        Detail = "Change your password before continuing.",
                        Type = ApiProblem.ToTypeUrn(ErrorCode),
                        Extensions = { ["errorCode"] = ErrorCode },
                    },
                }).ConfigureAwait(false);

                return;
            }
        }

        await next(context).ConfigureAwait(false);
    }
}
