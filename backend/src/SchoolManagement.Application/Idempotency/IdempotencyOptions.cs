using Microsoft.Extensions.Options;

namespace SchoolManagement.Application.Idempotency;

/// <summary>
/// Retention tuning for the idempotency substrate (TASK-0019, §9.9), bound from the
/// <c>Idempotency</c> configuration section. Lives in Application (like <c>PipelineOptions</c>) so
/// both the Api-layer filter and the Infrastructure-layer purge job can depend on it without either
/// depending on the other.
/// </summary>
public sealed class IdempotencyOptions
{
    /// <summary>The configuration section this binds to.</summary>
    public const string SectionName = "Idempotency";

    /// <summary>
    /// How long a claimed key's row (and its stored response) is retained before the purge job
    /// removes it. Default 24 — approved delta: "no product mutating endpoint exists yet" was the
    /// reason to defer the mechanism, not a reason to keep its data forever once built; 24 hours
    /// comfortably covers a client's retry/backoff window (§9.8.2) while keeping the window a
    /// one-time response value (see <see cref="RedactFromIdempotencyReplayAttribute"/>) sits in a
    /// second table as short as the mechanism can make it.
    /// </summary>
    public int RetentionHours { get; set; } = 24;
}

/// <summary>Validates <see cref="IdempotencyOptions"/> at startup.</summary>
internal sealed class IdempotencyOptionsValidator : IValidateOptions<IdempotencyOptions>
{
    private const int MinimumRetentionHours = 1;
    private const int MaximumRetentionHours = 168; // one week — a ceiling, not a recommendation.

    /// <inheritdoc />
    public ValidateOptionsResult Validate(string? name, IdempotencyOptions options)
    {
        ArgumentNullException.ThrowIfNull(options);

        if (options.RetentionHours is < MinimumRetentionHours or > MaximumRetentionHours)
        {
            return ValidateOptionsResult.Fail(
                $"'{IdempotencyOptions.SectionName}:{nameof(IdempotencyOptions.RetentionHours)}' must " +
                $"be between {MinimumRetentionHours} and {MaximumRetentionHours}, but was " +
                $"{options.RetentionHours}.");
        }

        return ValidateOptionsResult.Success;
    }
}
