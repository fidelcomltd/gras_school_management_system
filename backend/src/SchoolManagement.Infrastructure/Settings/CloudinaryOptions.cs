using Microsoft.Extensions.Options;

namespace SchoolManagement.Infrastructure.Settings;

/// <summary>
/// Cloudinary credentials and asset placement (TASK-0005b stage D), bound from the
/// <c>Cloudinary</c> section.
/// </summary>
/// <remarks>
/// <para>
/// THE SECRETS NEVER LIVE IN A COMMITTED FILE. On the VPS and on the staging host they arrive as
/// environment variables (<c>Cloudinary__ApiKey</c> and so on); locally, through
/// <c>dotnet user-secrets</c>. <c>appsettings*.json</c> carries at most the folder prefix.
/// </para>
/// <para>
/// <see cref="FolderPrefix"/> keeps environments from writing over each other inside one Cloudinary
/// account — production under <c>gras/prod</c>, staging under <c>gras/staging</c>. It is a
/// convenience for a human reading the media library, not a security boundary: one API key can
/// reach the whole account either way.
/// </para>
/// </remarks>
public sealed class CloudinaryOptions
{
    /// <summary>The configuration section this binds to.</summary>
    public const string SectionName = "Cloudinary";

    /// <summary>The Cloudinary account's cloud name.</summary>
    public string? CloudName { get; set; }

    /// <summary>The API key.</summary>
    public string? ApiKey { get; set; }

    /// <summary>The API secret. A secret in the strict sense: environment or user-secrets only.</summary>
    public string? ApiSecret { get; set; }

    /// <summary>
    /// Folder every asset of this environment is written under, without leading or trailing
    /// slashes — for example <c>gras/prod</c>.
    /// </summary>
    public string FolderPrefix { get; set; } = "gras";

    /// <summary>
    /// Development and tests only: use the in-process <see cref="InMemorySchoolImageStore"/> when
    /// no credentials are configured, instead of refusing to start.
    /// </summary>
    /// <remarks>
    /// The same shape as <c>Pins:AllowDevelopmentKeys</c>, and for the same reason: a deployed
    /// environment that is missing its credentials must fail loudly at boot rather than come up
    /// with a store that loses the school's logo on the next restart.
    /// </remarks>
    public bool AllowInMemoryStore { get; set; }

    /// <summary>Whether a complete set of credentials is present.</summary>
    internal bool IsConfigured =>
        !string.IsNullOrWhiteSpace(CloudName)
        && !string.IsNullOrWhiteSpace(ApiKey)
        && !string.IsNullOrWhiteSpace(ApiSecret);
}

/// <summary>Validates <see cref="CloudinaryOptions"/> at startup.</summary>
internal sealed class CloudinaryOptionsValidator : IValidateOptions<CloudinaryOptions>
{
    /// <inheritdoc />
    public ValidateOptionsResult Validate(string? name, CloudinaryOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        var failures = new List<string>();

        if (!options.IsConfigured)
        {
            if (!options.AllowInMemoryStore)
            {
                failures.Add(
                    "Cloudinary:CloudName, Cloudinary:ApiKey and Cloudinary:ApiSecret are all required. " +
                    "Set them as environment variables (Cloudinary__CloudName and so on) on this host, or " +
                    "set Cloudinary:AllowInMemoryStore in Development to store images in process memory.");
            }
            else if (!string.IsNullOrWhiteSpace(options.CloudName)
                || !string.IsNullOrWhiteSpace(options.ApiKey)
                || !string.IsNullOrWhiteSpace(options.ApiSecret))
            {
                // Half-configured is the dangerous state: it reads as "Cloudinary is set up" while
                // silently falling back to a store that forgets everything on restart.
                failures.Add(
                    "Cloudinary is partially configured: some of CloudName/ApiKey/ApiSecret are set and " +
                    "some are not. Set all three, or none of them.");
            }
        }

        var prefix = options.FolderPrefix;

        if (string.IsNullOrWhiteSpace(prefix))
        {
            failures.Add("Cloudinary:FolderPrefix must not be empty.");
        }
        else if (prefix.StartsWith('/') || prefix.EndsWith('/'))
        {
            failures.Add($"Cloudinary:FolderPrefix must not start or end with '/', found '{prefix}'.");
        }

        return failures.Count == 0
            ? ValidateOptionsResult.Success
            : ValidateOptionsResult.Fail(failures);
    }
}
