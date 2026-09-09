using System.Net;

namespace SchoolManagement.Domain.Audit;

/// <summary>
/// Spec 6.1.12 / spec 9.3's PII rule for <c>audit_event.source_ip</c> and
/// <c>audit_event.user_agent</c>: truncate before storage, never store the full value.
/// </summary>
/// <remarks>
/// Pure and persistence-free so it is unit-testable directly (root <c>CLAUDE.md</c> §6: "Unit
/// tests cover domain rules"). Called by the writer in <c>Infrastructure/Audit</c> BEFORE the raw
/// value reaches <see cref="AuditEvent.Create"/> — see that type's own remarks.
/// </remarks>
public static class AuditFieldTruncation
{
    /// <summary>The longest <c>user_agent</c> value spec 6.1.12 allows.</summary>
    public const int UserAgentMaxLength = 300;

    /// <summary>How many leading bytes of an IPv4 address are kept (a /24 network).</summary>
    private const int Ipv4PrefixBytes = 3;

    /// <summary>How many leading bytes of an IPv6 address are kept (a /48 network).</summary>
    private const int Ipv6PrefixBytes = 6;

    /// <summary>
    /// Truncates <paramref name="rawSourceIp"/> to its /24 network (IPv4) or /48 network (IPv6),
    /// per spec 6.1.12: "Truncated to /24 for IPv4 and /48 for IPv6 before storage."
    /// </summary>
    /// <param name="rawSourceIp">The caller's raw address, or <see langword="null"/>.</param>
    /// <returns>
    /// The truncated network address, or <see langword="null"/> when <paramref name="rawSourceIp"/>
    /// is <see langword="null"/>, empty, or not a parseable IP address — a malformed value is
    /// dropped rather than stored half-truncated.
    /// </returns>
    public static string? TruncateSourceIp(string? rawSourceIp)
    {
        if (string.IsNullOrWhiteSpace(rawSourceIp) || !IPAddress.TryParse(rawSourceIp, out var address))
        {
            return null;
        }

        // An IPv4-mapped IPv6 address (::ffff:a.b.c.d, as .NET's Socket layer commonly reports for a
        // dual-stack listener) is, for this rule's purpose, an IPv4 address — spec 6.1.12 names no
        // third case, and truncating the WRAPPER'S 128 bits to /48 would keep far more of the real
        // address than the /24 rule intends.
        if (address.IsIPv4MappedToIPv6)
        {
            address = address.MapToIPv4();
        }

        var bytes = address.GetAddressBytes();
        var prefixBytes = address.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork
            ? Ipv4PrefixBytes
            : Ipv6PrefixBytes;

        Array.Clear(bytes, prefixBytes, bytes.Length - prefixBytes);

        return new IPAddress(bytes).ToString();
    }

    /// <summary>
    /// Truncates <paramref name="rawUserAgent"/> to <see cref="UserAgentMaxLength"/> characters.
    /// Never throws, regardless of input length.
    /// </summary>
    public static string? TruncateUserAgent(string? rawUserAgent) =>
        rawUserAgent is null
            ? null
            : rawUserAgent.Length <= UserAgentMaxLength
                ? rawUserAgent
                : rawUserAgent[..UserAgentMaxLength];
}
