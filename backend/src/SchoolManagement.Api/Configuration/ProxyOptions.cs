using System.Globalization;
using System.Net;
using Microsoft.AspNetCore.HttpOverrides;
using Microsoft.Extensions.Options;

namespace SchoolManagement.Api.Configuration;

/// <summary>
/// Whether this process sits behind a reverse proxy, and which proxies it believes, bound from the
/// <c>Proxy</c> section.
/// </summary>
/// <remarks>
/// <para>
/// WHY THIS IS NOT ALWAYS-ON. <c>X-Forwarded-For</c> and <c>X-Forwarded-Proto</c> are request
/// headers: anyone who can reach Kestrel directly can send them. Honouring them unconditionally
/// would let a caller name its own client address — which is the key the rate limiter partitions on
/// (<c>Program.cs</c>'s <c>ResolveRateLimitPartitionKey</c>), the address the portal's per-address
/// pin limits count (spec 6.9), and the <c>source_ip</c> every audit row records
/// (<c>AuditEventFactory</c>). So the forwarding is opt-in per deployment AND scoped to named
/// proxies.
/// </para>
/// <para>
/// WHY IT MATTERS THAT IT IS ON IN PRODUCTION. Behind Nginx every request arrives from loopback
/// over plain HTTP. Without this, all three of the above collapse onto <c>127.0.0.1</c> — one
/// shared rate-limit bucket for the whole internet, portal address limits that lock out every
/// parent at once, and an audit trail that records the proxy instead of the caller — and
/// <c>UseHttpsRedirection</c> sees <c>http</c> and redirects a request that already arrived over
/// TLS.
/// </para>
/// <para>
/// Two shapes are supported. On a single VPS, Nginx is on loopback and
/// <see cref="KnownProxies"/> names it. On a PaaS (staging, Render) the edge address is neither
/// stable nor published, so <see cref="TrustAllProxies"/> accepts the nearest hop instead — sound
/// only because nothing but that platform's edge can route to the container.
/// </para>
/// </remarks>
public sealed class ProxyOptions
{
    /// <summary>The configuration section this binds to.</summary>
    public const string SectionName = "Proxy";

    /// <summary>
    /// Whether to honour forwarding headers at all. Default <c>false</c>, so a deployment that
    /// forgets to set it gets the safe behaviour (the proxy's own address) rather than a spoofable one.
    /// </summary>
    public bool Enabled { get; set; }

    /// <summary>
    /// Addresses of the proxies to believe — for example <c>127.0.0.1</c> and <c>::1</c> for an
    /// Nginx on the same host.
    /// </summary>
    /// <remarks>
    /// A get-only list for the same reason as <see cref="CorsOptions.AllowedOrigins"/>: the
    /// configuration binder adds into the existing instance (CA1819).
    /// </remarks>
    public IList<string> KnownProxies { get; } = [];

    /// <summary>Proxy networks to believe, in CIDR form — for example <c>10.0.0.0/8</c>.</summary>
    public IList<string> KnownNetworks { get; } = [];

    /// <summary>
    /// Believe whichever proxy the request came from. For a PaaS whose edge address is not
    /// published; never on the VPS, where <see cref="KnownProxies"/> is exact.
    /// </summary>
    public bool TrustAllProxies { get; set; }

    /// <summary>
    /// How many forwarding hops to unwind, counting back from this process. Default 1 — one proxy
    /// (Nginx, or the PaaS edge) sits in front, so only the last entry is ours to trust and
    /// anything a client appended earlier stays ignored.
    /// </summary>
    public int ForwardLimit { get; set; } = 1;

    /// <summary>
    /// Translates these options onto ASP.NET Core's own <see cref="ForwardedHeadersOptions"/>.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Only <c>X-Forwarded-For</c> and <c>X-Forwarded-Proto</c> are read. <c>X-Forwarded-Host</c> is
    /// deliberately NOT, because the host reaches this process as the ordinary <c>Host</c> header
    /// (Nginx's <c>proxy_set_header Host</c>) and is checked against <c>AllowedHosts</c>; honouring
    /// a forwarded host as well would add a second, header-controlled way to change the value
    /// absolute redirects and links are built from.
    /// </para>
    /// <para>
    /// The framework's defaults for <c>KnownProxies</c> and <c>KnownNetworks</c> (loopback) are
    /// cleared first, so what is trusted is exactly what configuration named and does not silently
    /// widen on a framework default change.
    /// </para>
    /// </remarks>
    /// <param name="forwardedHeaders">The framework options to populate.</param>
    public void ApplyTo(ForwardedHeadersOptions forwardedHeaders)
    {
        ArgumentNullException.ThrowIfNull(forwardedHeaders);

        forwardedHeaders.ForwardedHeaders = ForwardedHeaders.XForwardedFor | ForwardedHeaders.XForwardedProto;
        forwardedHeaders.ForwardLimit = ForwardLimit;

        forwardedHeaders.KnownProxies.Clear();
        forwardedHeaders.KnownIPNetworks.Clear();

        if (TrustAllProxies)
        {
            // An empty allow-list plus a null ForwardLimit is the framework's documented way to say
            // "accept the headers whatever the immediate peer is". ForwardLimit stays set, so a
            // client-appended chain is still not walked past the edge's own entry.
            return;
        }

        foreach (var proxy in KnownProxies)
        {
            forwardedHeaders.KnownProxies.Add(IPAddress.Parse(proxy));
        }

        foreach (var network in KnownNetworks)
        {
            forwardedHeaders.KnownIPNetworks.Add(System.Net.IPNetwork.Parse(network));
        }
    }
}

/// <summary>Validates <see cref="ProxyOptions"/> at startup.</summary>
/// <remarks>
/// Every failure here is one that would otherwise surface as a security property quietly not
/// holding — headers honoured from nowhere in particular, or an unparsable address silently
/// dropped from the trust list — rather than as an error. Hence fail-fast at boot.
/// </remarks>
internal sealed class ProxyOptionsValidator : IValidateOptions<ProxyOptions>
{
    /// <inheritdoc />
    public ValidateOptionsResult Validate(string? name, ProxyOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        if (!options.Enabled)
        {
            // Nothing else in the section means anything while forwarding is off, so an unparsable
            // leftover address is not worth failing a boot over.
            return ValidateOptionsResult.Success;
        }

        var failures = new List<string>();

        if (options.ForwardLimit < 1)
        {
            failures.Add(string.Create(
                CultureInfo.InvariantCulture,
                $"Proxy:ForwardLimit must be at least 1 when forwarding is enabled; found {options.ForwardLimit}."));
        }

        foreach (var proxy in options.KnownProxies)
        {
            if (!IPAddress.TryParse(proxy, out _))
            {
                failures.Add($"Proxy:KnownProxies contains '{proxy}', which is not an IP address.");
            }
        }

        foreach (var network in options.KnownNetworks)
        {
            if (!System.Net.IPNetwork.TryParse(network, out _))
            {
                failures.Add($"Proxy:KnownNetworks contains '{network}', which is not a CIDR network such as '10.0.0.0/8'.");
            }
        }

        if (options.TrustAllProxies)
        {
            if (options.KnownProxies.Count > 0 || options.KnownNetworks.Count > 0)
            {
                // Not merely redundant: someone who listed addresses believes those are the limit,
                // and TrustAllProxies means they are not. Refuse rather than pick one silently.
                failures.Add(
                    "Proxy:TrustAllProxies is set together with KnownProxies/KnownNetworks, which would be " +
                    "ignored. Use TrustAllProxies alone (a PaaS edge), or the lists alone (a known proxy).");
            }
        }
        else if (options.KnownProxies.Count == 0 && options.KnownNetworks.Count == 0)
        {
            failures.Add(
                "Proxy:Enabled is true but no proxy is named. Set KnownProxies (for example 127.0.0.1 and ::1 " +
                "for an Nginx on the same host), KnownNetworks, or TrustAllProxies.");
        }

        return failures.Count == 0
            ? ValidateOptionsResult.Success
            : ValidateOptionsResult.Fail(failures);
    }
}
