using Microsoft.Extensions.Options;

namespace SchoolManagement.Api.Configuration;

/// <summary>
/// Helper for the one way this codebase registers configuration.
/// </summary>
public static class OptionsRegistration
{
    /// <summary>
    /// Binds <typeparamref name="TOptions"/> to a configuration section, attaches
    /// <typeparamref name="TValidator"/>, and validates at STARTUP.
    /// </summary>
    /// <remarks>
    /// <para>
    /// Every options type in this application goes through this method. The point is
    /// <c>ValidateOnStart()</c>: without it, options validation runs lazily on first resolution, so a
    /// typo in a deployed configuration file produces a 500 on whichever request happens to touch that
    /// setting first — possibly hours after deployment, and probably not the request you were watching.
    /// With it, the process refuses to start and the orchestrator's rollout halts on a message naming
    /// the bad key.
    /// </para>
    /// <para>
    /// Validators are hand-written <see cref="IValidateOptions{TOptions}"/> implementations rather than
    /// data annotations, because they can express cross-field rules ("credentials require an explicit
    /// origin list") and produce messages that say what to do about it.
    /// </para>
    /// </remarks>
    /// <typeparam name="TOptions">The options type to bind.</typeparam>
    /// <typeparam name="TValidator">Its validator.</typeparam>
    /// <param name="services">The service collection.</param>
    /// <param name="configuration">Configuration root.</param>
    /// <param name="sectionName">The section to bind from.</param>
    /// <param name="validateOnStart">
    /// Whether to validate at host start. Pass <c>false</c> ONLY in contract-generation mode, where the
    /// process starts purely to emit the OpenAPI document and no infrastructure is configured — see
    /// <see cref="HostMode"/>. The validator is still registered either way, so anything that resolves
    /// the options gets a validated instance; only the eager check at start is skipped.
    /// </param>
    /// <returns>The same collection, for chaining.</returns>
    public static IServiceCollection AddValidatedOptions<TOptions, TValidator>(
        this IServiceCollection services,
        IConfiguration configuration,
        string sectionName,
        bool validateOnStart = true)
        where TOptions : class
        where TValidator : class, IValidateOptions<TOptions>
    {
        ArgumentNullException.ThrowIfNull(services);
        ArgumentNullException.ThrowIfNull(configuration);
        ArgumentException.ThrowIfNullOrWhiteSpace(sectionName);

        services.AddSingleton<IValidateOptions<TOptions>, TValidator>();

        var builder = services
            .AddOptions<TOptions>()
            .Bind(configuration.GetSection(sectionName));

        if (validateOnStart)
        {
            builder.ValidateOnStart();
        }

        return services;
    }
}
