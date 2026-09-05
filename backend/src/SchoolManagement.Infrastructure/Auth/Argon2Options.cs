using Microsoft.Extensions.Options;

namespace SchoolManagement.Infrastructure.Auth;

/// <summary>
/// Argon2id cost parameters (spec 9.1: "memory cost tuned so that a single hash takes between 150
/// and 300 milliseconds on the production instance"), bound from the <c>Argon2</c> section.
/// </summary>
/// <remarks>
/// Defaults are OWASP's current baseline Argon2id recommendation (19 MiB, 2 iterations, 1 degree of
/// parallelism), which the production instance should be benchmarked against and tuned via
/// configuration — not a code change — to land inside the 150-300ms window. Every hash embeds the
/// parameters that produced it (<c>Argon2idPasswordHasher</c>), so raising these later never
/// invalidates an existing password (spec 9.1).
/// </remarks>
public sealed class Argon2Options
{
    /// <summary>The configuration section this binds to.</summary>
    public const string SectionName = "Argon2";

    /// <summary>Memory cost in kibibytes. Default 19456 (19 MiB) — OWASP's current baseline.</summary>
    public int MemorySizeKiB { get; set; } = 19_456;

    /// <summary>Time cost (number of passes). Default 2.</summary>
    public int Iterations { get; set; } = 2;

    /// <summary>Degree of parallelism (lanes). Default 1.</summary>
    public int DegreeOfParallelism { get; set; } = 1;

    /// <summary>Salt length in bytes. Default 16.</summary>
    public int SaltSizeBytes { get; set; } = 16;

    /// <summary>Output hash length in bytes. Default 32.</summary>
    public int HashSizeBytes { get; set; } = 32;
}

/// <summary>Validates <see cref="Argon2Options"/> at startup.</summary>
internal sealed class Argon2OptionsValidator : IValidateOptions<Argon2Options>
{
    /// <inheritdoc />
    public ValidateOptionsResult Validate(string? name, Argon2Options options)
    {
        ArgumentNullException.ThrowIfNull(options);

        var failures = new List<string>();

        Check(nameof(Argon2Options.MemorySizeKiB), options.MemorySizeKiB, 8_192, 1_048_576);
        Check(nameof(Argon2Options.Iterations), options.Iterations, 1, 100);
        Check(nameof(Argon2Options.DegreeOfParallelism), options.DegreeOfParallelism, 1, 16);
        Check(nameof(Argon2Options.SaltSizeBytes), options.SaltSizeBytes, 8, 64);
        Check(nameof(Argon2Options.HashSizeBytes), options.HashSizeBytes, 16, 64);

        return failures.Count > 0
            ? ValidateOptionsResult.Fail(failures)
            : ValidateOptionsResult.Success;

        void Check(string property, int value, int minimum, int maximum)
        {
            if (value < minimum || value > maximum)
            {
                failures.Add(
                    $"'{Argon2Options.SectionName}:{property}' must be between {minimum} and " +
                    $"{maximum}, but was {value}.");
            }
        }
    }
}
