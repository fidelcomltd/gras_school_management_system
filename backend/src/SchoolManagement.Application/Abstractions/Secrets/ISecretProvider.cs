namespace SchoolManagement.Application.Abstractions.Secrets;

/// <summary>
/// Retrieves a secret by logical name. The seam that keeps the choice of secret STORE out of the
/// application.
/// </summary>
/// <remarks>
/// <para>
/// The shipped implementation reads from <c>IConfiguration</c>, which already layers user-secrets
/// (local), environment variables, and any external configuration provider — so pointing the service
/// at Azure Key Vault, AWS Secrets Manager or HashiCorp Vault is a one-line change in the composition
/// root and NO change here. See <c>docs/adr/0009-secret-store.md</c>.
/// </para>
/// <para>
/// This is for secrets fetched at RUNTIME by logical name. Configuration that happens to be
/// sensitive — a connection string, an API key bound into an options class — should keep using the
/// options pattern with <c>ValidateOnStart</c>, so a missing value fails at boot instead of on first
/// use. Do not reach for this interface to avoid writing an options class.
/// </para>
/// <para>
/// IMPLEMENTATION RULE: never log a secret's VALUE, and never include it in an exception message.
/// Naming the missing key is helpful; echoing its contents defeats the point of the store.
/// </para>
/// </remarks>
public interface ISecretProvider
{
    /// <summary>
    /// Returns the secret, or <c>null</c> when it is not configured.
    /// </summary>
    /// <param name="name">
    /// Logical secret name, using configuration key syntax (for example
    /// <c>ExternalApi:ApiKey</c>). A provider maps this to its own naming scheme.
    /// </param>
    /// <param name="cancellationToken">The request's cancellation token.</param>
    ValueTask<string?> TryGetSecretAsync(string name, CancellationToken cancellationToken);

    /// <summary>
    /// Returns the secret, throwing when it is absent.
    /// </summary>
    /// <param name="name">Logical secret name.</param>
    /// <param name="cancellationToken">The request's cancellation token.</param>
    /// <returns>The secret value, never null or whitespace.</returns>
    /// <exception cref="InvalidOperationException">
    /// The secret is not configured. The message names the key but NEVER any value.
    /// </exception>
    ValueTask<string> GetRequiredSecretAsync(string name, CancellationToken cancellationToken);
}
