using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using SchoolManagement.Api.Security;
using SchoolManagement.Application.Abstractions.Messaging;
using SchoolManagement.Application.Portal;

namespace SchoolManagement.Api.Portal;

/// <summary>
/// The public parent portal (spec 6.9; human ruling 2026-09-22: server-rendered, same process, own subdomain). These
/// routes are anonymous, live outside <c>/api</c>, and are excluded from the OpenAPI contract, because they are pages
/// rather than an API. They never reveal whether a registration number exists before a pin validates.
/// </summary>
internal static class PortalEndpoints
{
    /// <summary>The device's session cookie: up to five random tokens, dot-separated.</summary>
    public const string CookieName = "gras_portal";

    /// <summary>Spec 6.9.3: every lookup response takes at least this long.</summary>
    private static readonly TimeSpan MinimumLookupDuration = TimeSpan.FromMilliseconds(400);

    public static void MapPortal(this IEndpointRouteBuilder endpoints)
    {
        var portal = endpoints.MapGroup("/portal").AllowAnonymous().ExcludeFromDescription().DisableAntiforgery();
        portal.AddEndpointFilter(async (context, next) =>
        {
            context.HttpContext.Response.Headers["X-Robots-Tag"] = "noindex, nofollow";
            context.HttpContext.Response.Headers.CacheControl = "no-store";
            return await next(context).ConfigureAwait(false);
        });

        portal.MapGet(string.Empty, LandingAsync);
        portal.MapPost("/lookup", LookupAsync)
            .ExemptFromCsrfRequirement("Public portal: no signed-in session exists to ride, and a lookup needs a valid pin the attacker would already hold.");
        portal.MapGet("/terms", TermsAsync);
        portal.MapGet("/result/{termId:guid}", ResultAsync);
        portal.MapPost("/end", EndAsync)
            .ExemptFromCsrfRequirement("The viewing cookie is SameSite=Strict, so a cross-site post arrives without it and ends nothing.");
        portal.MapGet("/logo", LogoAsync);

        endpoints.MapGet("/robots.txt", () => Results.Text("User-agent: *\nDisallow: /portal\nDisallow: /verify\n", "text/plain"))
            .AllowAnonymous().ExcludeFromDescription();
    }

    private static async Task<IResult> LandingAsync(HttpContext http, ISender sender, CancellationToken cancellationToken)
    {
        var branding = await BrandingAsync(sender, cancellationToken).ConfigureAwait(false);
        var sessions = await OpenSessionsAsync(http, sender, cancellationToken).ConfigureAwait(false);
        var message = http.Request.Query.ContainsKey("another") && sessions.Count > 0 ? PortalCopy.CheckAnother(sessions[^1].UsesLeft) : null;
        return Html(PortalHtml.Landing(branding, message, http.Request.Query.ContainsKey("another") ? [] : sessions));
    }

    private static async Task<IResult> LookupAsync(HttpContext http, ISender sender, CancellationToken cancellationToken)
    {
        var stopwatch = Stopwatch.StartNew();
        var form = await http.Request.ReadFormAsync(cancellationToken).ConfigureAwait(false);
        var command = new PortalLookupCommand(
            form["registrationNumber"].ToString(),
            form["pin"].ToString(),
            TruncatedAddress(http.Connection.RemoteIpAddress),
            http.Request.Headers.UserAgent.ToString(),
            ReadTokens(http));

        var result = await sender.SendAsync(command, cancellationToken).ConfigureAwait(false);
        var branding = await BrandingAsync(sender, cancellationToken).ConfigureAwait(false);

        var remaining = MinimumLookupDuration - stopwatch.Elapsed;
        if (remaining > TimeSpan.Zero)
        {
            await Task.Delay(remaining, cancellationToken).ConfigureAwait(false);
        }

        if (result.IsFailure)
        {
            return Html(PortalHtml.Landing(branding, PortalCopy.NotFound, []));
        }

        if (result.Value is { Status: PortalLookupStatus.Opened or PortalLookupStatus.Resumed, Token: { } token })
        {
            var tokens = ReadTokens(http).Where(existing => existing != token).Append(token).TakeLast(PortalSessionRules.MaxSessionsPerDevice).ToList();
            WriteTokens(http, tokens);
            return Results.Redirect("/portal/terms", permanent: false, preserveMethod: false);
        }

        // Spec 6.9.9: every failure is a uniform 200 with one of the 6.9.4 copies.
        return Html(PortalHtml.Landing(branding, PortalCopy.For(result.Value), []));
    }

    private static async Task<IResult> TermsAsync(HttpContext http, ISender sender, CancellationToken cancellationToken)
    {
        var branding = await BrandingAsync(sender, cancellationToken).ConfigureAwait(false);
        var sessions = await OpenSessionsAsync(http, sender, cancellationToken).ConfigureAwait(false);
        if (sessions.Count == 0)
        {
            return Html(PortalHtml.Message(branding, PortalCopy.SessionEnded));
        }

        var selected = Guid.TryParse(http.Request.Query["u"], out var useId)
            ? sessions.FirstOrDefault(session => session.UseId == useId) ?? sessions[^1]
            : sessions[^1];
        return Html(PortalHtml.Terms(branding, selected, sessions.Where(session => session.UseId != selected.UseId).ToList()));
    }

    private static async Task<IResult> ResultAsync(Guid termId, HttpContext http, ISender sender, CancellationToken cancellationToken)
    {
        var branding = await BrandingAsync(sender, cancellationToken).ConfigureAwait(false);
        Guid? useId = Guid.TryParse(http.Request.Query["u"], out var parsed) ? parsed : null;
        var result = await sender.SendAsync(new GetPortalResultQuery(ReadTokens(http), useId, termId), cancellationToken).ConfigureAwait(false);
        if (result.IsFailure)
        {
            return Html(PortalHtml.Message(branding, PortalCopy.ServerFault));
        }

        var view = result.Value;
        return view.Status switch
        {
            PortalResultStatus.Shown => Html(PortalHtml.Result(branding, view.Sheet!, view.UseId!.Value)),
            PortalResultStatus.NotReleased => Html(PortalHtml.Message(branding, PortalCopy.NotReleased(view.TermName ?? "This term's"))),
            PortalResultStatus.BeingCorrected => Html(PortalHtml.Message(branding, PortalCopy.BeingCorrected)),
            PortalResultStatus.NoResult => Html(PortalHtml.Message(branding, PortalCopy.NoResult)),
            _ => Html(PortalHtml.Message(branding, PortalCopy.SessionEnded)),
        };
    }

    private static async Task<IResult> EndAsync(HttpContext http, ISender sender, CancellationToken cancellationToken)
    {
        await sender.SendAsync(new EndPortalSessionsCommand(ReadTokens(http)), cancellationToken).ConfigureAwait(false);
        http.Response.Cookies.Delete(CookieName, new CookieOptions { Path = "/portal", Secure = true, HttpOnly = true, SameSite = SameSiteMode.Strict });
        return Results.Redirect("/portal");
    }

    private static async Task<IResult> LogoAsync(ISender sender, CancellationToken cancellationToken)
    {
        var result = await sender.SendAsync(new GetPortalLogoQuery(), cancellationToken).ConfigureAwait(false);
        return result.IsSuccess ? Results.Stream(result.Value.Content, result.Value.ContentType) : Results.NotFound();
    }

    private static async Task<PortalBranding> BrandingAsync(ISender sender, CancellationToken cancellationToken)
    {
        var result = await sender.SendAsync(new GetPortalBrandingQuery(), cancellationToken).ConfigureAwait(false);
        return result.IsSuccess ? result.Value : new PortalBranding("School results", HasLogo: false);
    }

    private static async Task<IReadOnlyList<PortalViewingSession>> OpenSessionsAsync(HttpContext http, ISender sender, CancellationToken cancellationToken)
    {
        var tokens = ReadTokens(http);
        if (tokens.Count == 0)
        {
            return [];
        }

        var result = await sender.SendAsync(new GetPortalSessionsQuery(tokens), cancellationToken).ConfigureAwait(false);
        return result.IsSuccess ? result.Value : [];
    }

    private static List<string> ReadTokens(HttpContext http) =>
        http.Request.Cookies.TryGetValue(CookieName, out var value) && !string.IsNullOrEmpty(value)
            ? value.Split('.', StringSplitOptions.RemoveEmptyEntries).TakeLast(PortalSessionRules.MaxSessionsPerDevice).ToList()
            : [];

    private static void WriteTokens(HttpContext http, IReadOnlyList<string> tokens) =>
        http.Response.Cookies.Append(CookieName, string.Join('.', tokens), new CookieOptions
        {
            Path = "/portal",
            HttpOnly = true,
            Secure = true,
            SameSite = SameSiteMode.Strict,
            MaxAge = TimeSpan.FromMinutes(30),
            IsEssential = true,
        });

    /// <summary>Spec 6.8.5: an IPv4 address to its /24, an IPv6 address to its /48.</summary>
    internal static string? TruncatedAddress(IPAddress? address)
    {
        if (address is null)
        {
            return null;
        }

        if (address.IsIPv4MappedToIPv6)
        {
            address = address.MapToIPv4();
        }

        var bytes = address.GetAddressBytes();
        if (address.AddressFamily == AddressFamily.InterNetwork)
        {
            bytes[3] = 0;
            return new IPAddress(bytes).ToString() + "/24";
        }

        for (var index = 6; index < bytes.Length; index++)
        {
            bytes[index] = 0;
        }

        return new IPAddress(bytes).ToString() + "/48";
    }

    private static IResult Html(string html) => Results.Content(html, "text/html; charset=utf-8");
}
