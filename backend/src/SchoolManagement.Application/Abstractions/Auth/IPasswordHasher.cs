namespace SchoolManagement.Application.Abstractions.Auth;

/// <summary>
/// Hashes and verifies passwords with Argon2id (spec 9.1): "memory cost tuned so that a single hash
/// takes between 150 and 300 milliseconds on the production instance. Cost parameters stored with the
/// hash so they can be raised later without invalidating existing passwords."
/// </summary>
/// <remarks>
/// The encoded hash returned by <see cref="Hash"/> embeds its own cost parameters and salt (PHC
/// string format), so <see cref="Verify"/> never needs them supplied separately and a future cost
/// increase does not invalidate passwords hashed under the old parameters.
/// </remarks>
public interface IPasswordHasher
{
    /// <summary>Hashes <paramref name="password"/>, returning a self-describing encoded string.</summary>
    /// <param name="password">The plaintext password. Never logged, never persisted.</param>
    string Hash(string password);

    /// <summary>
    /// Verifies <paramref name="password"/> against a previously encoded hash.
    /// </summary>
    /// <param name="password">The plaintext password to check.</param>
    /// <param name="encodedHash">A hash previously returned by <see cref="Hash"/>.</param>
    /// <remarks>
    /// MUST run in constant time relative to whether the password matches — do not short-circuit on a
    /// cheap pre-check. TASK-0003's sign-in flow depends on this running unconditionally before any
    /// lockout branching, or response timing becomes an enumeration oracle.
    /// </remarks>
    bool Verify(string password, string encodedHash);

    /// <summary>
    /// A syntactically valid encoded hash, produced under the CURRENTLY CONFIGURED cost parameters,
    /// that can never match any real password. <c>SignInCommandHandler</c> verifies an unknown
    /// email's submitted password against this instead of skipping the check, so "no such account"
    /// costs the same as "wrong password" (spec 6.1.11's identical-message rule).
    /// </summary>
    /// <remarks>
    /// MUST be derived from the live configuration, never a hardcoded string: a hand-written constant
    /// silently stops tracking reality the moment cost parameters are tuned (which spec 9.1 explicitly
    /// asks operators to do, to hit 150-300ms), reopening the exact timing oracle the 423 ruling
    /// closed. Implementations should compute this once (e.g. via <see cref="Hash"/> against a fixed
    /// plaintext) and cache it — the cost must be paid by <see cref="Verify"/> when this value is
    /// used, not by regenerating it on every call.
    /// </remarks>
    string DummyHashForTimingParity { get; }
}
