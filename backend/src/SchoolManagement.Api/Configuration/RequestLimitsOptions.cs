using Microsoft.Extensions.Options;

namespace SchoolManagement.Api.Configuration;

/// <summary>
/// Inbound request limits, bound from the <c>RequestLimits</c> section.
/// </summary>
/// <remarks>
/// These are availability controls. Without a body-size cap, one client streaming a large upload can
/// consume the server's memory; without a bound on collection sizes, a single request can ask the
/// database to materialise everything. Both are trivially cheap for an attacker and expensive for us.
/// </remarks>
public sealed class RequestLimitsOptions
{
    /// <summary>The configuration section this binds to.</summary>
    public const string SectionName = "RequestLimits";

    /// <summary>
    /// Largest accepted request body, in bytes. Default 1 MiB.
    /// </summary>
    /// <remarks>
    /// Sized for JSON, which is all this API accepts today. Raise it for a specific upload endpoint
    /// using that endpoint's own metadata rather than lifting the global ceiling for everyone.
    /// </remarks>
    public long MaxRequestBodyBytes { get; set; } = 1024 * 1024;

    /// <summary>
    /// Maximum nesting depth accepted when deserialising a JSON body. Default 32.
    /// </summary>
    /// <remarks>
    /// Applied to <c>JsonSerializerOptions.MaxDepth</c>. A deeply nested payload is a few bytes to
    /// send and expensive to parse, so an unbounded depth is a cheap denial-of-service primitive.
    /// 32 is far beyond any legitimate request shape in this API.
    /// <para>
    /// Note there is no model-binding collection limit here: that setting belongs to MVC, and this
    /// service uses Minimal APIs, where a JSON body is bound by System.Text.Json. Configuring
    /// <c>MvcOptions</c> would look like a control and do nothing — the effective limits are this
    /// depth bound and <see cref="MaxRequestBodyBytes"/>.
    /// </para>
    /// </remarks>
    public int MaxJsonDepth { get; set; } = 32;
}

/// <summary>Validates <see cref="RequestLimitsOptions"/> at startup.</summary>
internal sealed class RequestLimitsOptionsValidator : IValidateOptions<RequestLimitsOptions>
{
    private const long MaxAllowedBodyBytes = 128L * 1024 * 1024;

    /// <inheritdoc />
    public ValidateOptionsResult Validate(string? name, RequestLimitsOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        var failures = new List<string>();

        if (options.MaxRequestBodyBytes is < 1024 or > MaxAllowedBodyBytes)
        {
            failures.Add(
                $"'{RequestLimitsOptions.SectionName}:" +
                $"{nameof(RequestLimitsOptions.MaxRequestBodyBytes)}' must be between 1024 and " +
                $"{MaxAllowedBodyBytes}, but was {options.MaxRequestBodyBytes}. A limit above that is " +
                "almost certainly the wrong tool — stream large uploads to object storage instead of " +
                "buffering them through the API.");
        }

        if (options.MaxJsonDepth is < 1 or > 256)
        {
            failures.Add(
                $"'{RequestLimitsOptions.SectionName}:{nameof(RequestLimitsOptions.MaxJsonDepth)}' " +
                $"must be between 1 and 256, but was {options.MaxJsonDepth}.");
        }

        return failures.Count > 0
            ? ValidateOptionsResult.Fail(failures)
            : ValidateOptionsResult.Success;
    }
}
