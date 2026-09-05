using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Konscious.Security.Cryptography;
using Microsoft.Extensions.Options;
using SchoolManagement.Application.Abstractions.Auth;

namespace SchoolManagement.Infrastructure.Auth;

/// <summary>
/// <see cref="IPasswordHasher"/> over Argon2id (spec 9.1), via <c>Konscious.Security.Cryptography</c>.
/// </summary>
/// <remarks>
/// <para>
/// ENCODING: a PHC-string-INSPIRED but project-specific format —
/// <c>argon2id$v=19$m={memoryKiB},t={iterations},p={parallelism}$saltBase64$hashBase64</c> — chosen
/// over reusing the exact PHC grammar because nothing outside this codebase ever parses it; a simpler
/// hand-written format is easier to review than a general-purpose PHC parser would be. Embedding the
/// parameters is what lets a future cost increase (spec 9.1) apply to new hashes without invalidating
/// old ones: <see cref="Verify"/> always re-derives from what the STORED hash says, never from the
/// currently configured <see cref="Argon2Options"/>.
/// </para>
/// <para>
/// Callers such as <c>SignInCommandHandler</c> rely on <see cref="Verify"/> costing approximately the
/// same whether or not the hash was ever produced by a real account (see
/// <see cref="DummyHashForTimingParity"/>) — this method never short-circuits on a cheap pre-check
/// before running the full KDF.
/// </para>
/// </remarks>
internal sealed class Argon2idPasswordHasher(IOptions<Argon2Options> options) : IPasswordHasher
{
    private const string Prefix = "argon2id";
    private const char Separator = '$';
    private const int ExpectedSegmentCount = 5;

    /// <summary>
    /// Never a real password — exists only so <see cref="DummyHashForTimingParity"/> has something
    /// fixed to hash under whatever cost parameters are CURRENTLY configured.
    /// </summary>
    private const string DummyPlaintext = "schoolmanagement-timing-parity-dummy-never-a-real-password";

    // Computed once, lazily, under the live IOptions<Argon2Options> — see the interface member's
    // remarks for why this must never be a hardcoded string (second-pass review HIGH 2). Lazy so the
    // (expensive) computation happens at most once per process, not on every failed sign-in. Calls the
    // STATIC Encode overload rather than the instance Hash method — a field initializer cannot
    // reference an instance member, only the captured primary-constructor parameter (options).
    private readonly Lazy<string> _dummyHashForTimingParity = new(() => Encode(options.Value, DummyPlaintext));

    /// <inheritdoc />
    public string DummyHashForTimingParity => _dummyHashForTimingParity.Value;

    /// <inheritdoc />
    public string Hash(string password)
    {
        ArgumentException.ThrowIfNullOrEmpty(password);

        return Encode(options.Value, password);
    }

    private static string Encode(Argon2Options configured, string password)
    {
        var salt = RandomNumberGenerator.GetBytes(configured.SaltSizeBytes);

        var hash = ComputeHash(
            password,
            salt,
            configured.MemorySizeKiB,
            configured.Iterations,
            configured.DegreeOfParallelism,
            configured.HashSizeBytes);

        return string.Join(
            Separator,
            Prefix,
            "v=19",
            FormattableString.Invariant(
                $"m={configured.MemorySizeKiB},t={configured.Iterations},p={configured.DegreeOfParallelism}"),
            Convert.ToBase64String(salt),
            Convert.ToBase64String(hash));
    }

    /// <inheritdoc />
    public bool Verify(string password, string encodedHash)
    {
        ArgumentException.ThrowIfNullOrEmpty(password);
        ArgumentException.ThrowIfNullOrEmpty(encodedHash);

        if (!TryParse(encodedHash, out var parsed))
        {
            return false;
        }

        var computed = ComputeHash(
            password,
            parsed.Salt,
            parsed.MemorySizeKiB,
            parsed.Iterations,
            parsed.DegreeOfParallelism,
            parsed.Hash.Length);

        return CryptographicOperations.FixedTimeEquals(computed, parsed.Hash);
    }

    private static byte[] ComputeHash(
        string password,
        byte[] salt,
        int memorySizeKiB,
        int iterations,
        int degreeOfParallelism,
        int hashSizeBytes)
    {
        using var argon2 = new Argon2id(Encoding.UTF8.GetBytes(password))
        {
            Salt = salt,
            MemorySize = memorySizeKiB,
            Iterations = iterations,
            DegreeOfParallelism = degreeOfParallelism,
        };

        return argon2.GetBytes(hashSizeBytes);
    }

    private static bool TryParse(string encodedHash, out ParsedHash parsed)
    {
        parsed = default;

        var segments = encodedHash.Split(Separator);

        if (segments.Length != ExpectedSegmentCount || segments[0] != Prefix)
        {
            return false;
        }

        var parameters = segments[2].Split(',');

        if (parameters.Length != 3)
        {
            return false;
        }

        if (!TryParseNamedInt(parameters[0], "m", out var memoryKiB) ||
            !TryParseNamedInt(parameters[1], "t", out var iterations) ||
            !TryParseNamedInt(parameters[2], "p", out var parallelism))
        {
            return false;
        }

        byte[] salt;
        byte[] hash;

        try
        {
            salt = Convert.FromBase64String(segments[3]);
            hash = Convert.FromBase64String(segments[4]);
        }
        catch (FormatException)
        {
            return false;
        }

        parsed = new ParsedHash(memoryKiB, iterations, parallelism, salt, hash);
        return true;
    }

    private static bool TryParseNamedInt(string segment, string expectedName, out int value)
    {
        value = 0;
        var parts = segment.Split('=');

        return parts.Length == 2 &&
            parts[0] == expectedName &&
            int.TryParse(parts[1], NumberStyles.None, CultureInfo.InvariantCulture, out value);
    }

    private readonly record struct ParsedHash(
        int MemorySizeKiB,
        int Iterations,
        int DegreeOfParallelism,
        byte[] Salt,
        byte[] Hash);
}
