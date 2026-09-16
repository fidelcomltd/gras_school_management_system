using System.Globalization;
using SchoolManagement.Domain.Common;

namespace SchoolManagement.Domain.Settings;

/// <summary>One submitted band, before persistence (no id — <c>PUT /settings/grading</c> replaces the whole set; see <see cref="GradingBand"/>'s remarks).</summary>
/// <param name="LowerBound">0 to 100 inclusive.</param>
/// <param name="UpperBound">0 to 100 inclusive, greater than or equal to <paramref name="LowerBound"/>.</param>
/// <param name="GradeLetter">Up to <see cref="GradingBand.GradeLetterMaxLength"/> characters. Unique, case-insensitive.</param>
/// <param name="Remark">At least <see cref="GradingBand.RemarkMinLength"/> characters.</param>
public sealed record GradingBandInput(int LowerBound, int UpperBound, string GradeLetter, string Remark);

/// <summary>
/// A grading-scale rejection carrying WHICH submitted band failed (spec 6.2.12: "Returns 422 with the
/// single first failure and the offending band index"), so the editor can highlight that row without
/// re-parsing the message text.
/// </summary>
/// <param name="Code">Stable error code.</param>
/// <param name="Description">The rule-specific message, naming the band and (rules 5-8) the mark.</param>
/// <param name="BandIndex">
/// 0-based position in the SUBMITTED array. <c>-1</c> for rule 1 (empty scale), which names no band.
/// </param>
public sealed record GradingBandValidationError(string Code, string Description, int BandIndex)
    : Error(Code, Description, ErrorType.Validation);

/// <summary>
/// Spec 6.2.5's ten save-time validation rules, run over the WHOLE submitted scale as one unit
/// (6.2.5: "Validation runs over the whole submitted scale as one unit. A partially valid scale is
/// never saved... exactly one message is shown, choosing the first failure in the order listed here").
/// </summary>
/// <remarks>
/// Rule 2 ("every bound is a whole number") has no code path here: <see cref="GradingBandInput.LowerBound"/>
/// and <see cref="GradingBandInput.UpperBound"/> are <see cref="int"/> on the wire, so a fractional
/// value never reaches this method — it is rejected by JSON model binding before a command exists to
/// validate, the same way <c>UpdateRegNumberCommand.SerialWidth</c> makes a fractional width
/// unreachable. See <c>backend/docs/ASSUMPTIONS.md</c>.
/// </remarks>
public static class GradingScaleRules
{
    /// <summary>Rule 1.</summary>
    public const string EmptyScaleCode = "settings.grading.empty_scale";

    /// <summary>Rule 3.</summary>
    public const string BoundOutOfRangeCode = "settings.grading.bound_out_of_range";

    /// <summary>Rule 4.</summary>
    public const string BoundsReversedCode = "settings.grading.bounds_reversed";

    /// <summary>Rule 5.</summary>
    public const string BandsOverlapCode = "settings.grading.bands_overlap";

    /// <summary>Rule 6.</summary>
    public const string CoverageGapCode = "settings.grading.coverage_gap";

    /// <summary>Rule 7.</summary>
    public const string DoesNotStartAtZeroCode = "settings.grading.does_not_start_at_zero";

    /// <summary>Rule 8.</summary>
    public const string DoesNotEndAtHundredCode = "settings.grading.does_not_end_at_hundred";

    /// <summary>Rule 9.</summary>
    public const string DuplicateGradeLetterCode = "settings.grading.duplicate_grade_letter";

    /// <summary>Rule 10.</summary>
    public const string RemarkTooShortCode = "settings.grading.remark_too_short";

    /// <summary>
    /// Validates <paramref name="bands"/> against all ten rules, in spec order, stopping at the first
    /// failure. A successful <see cref="Result"/> means the whole scale may be written.
    /// </summary>
    public static Result ValidateWholeScale(IReadOnlyList<GradingBandInput> bands)
    {
        ArgumentNullException.ThrowIfNull(bands);

        // Rule 1: at least one band exists.
        if (bands.Count == 0)
        {
            return Fail(EmptyScaleCode, "The grading scale must contain at least one band. Add a band before saving.", -1);
        }

        // Rule 3: every bound is between 0 and 100 inclusive. Checked in submitted order so the FIRST
        // offending band in the array is the one named, matching "choosing the first failure" in spirit.
        for (var i = 0; i < bands.Count; i++)
        {
            var band = bands[i];

            if (band.LowerBound is < 0 or > 100)
            {
                return Fail(
                    BoundOutOfRangeCode,
                    Invariant($"Band {band.GradeLetter} has a lower bound of {band.LowerBound}. Grade bounds must be between 0 and 100."),
                    i);
            }

            if (band.UpperBound is < 0 or > 100)
            {
                return Fail(
                    BoundOutOfRangeCode,
                    Invariant($"Band {band.GradeLetter} has an upper bound of {band.UpperBound}. Grade bounds must be between 0 and 100."),
                    i);
            }
        }

        // Rule 4: lower_bound <= upper_bound, per band.
        for (var i = 0; i < bands.Count; i++)
        {
            var band = bands[i];

            if (band.LowerBound > band.UpperBound)
            {
                return Fail(
                    BoundsReversedCode,
                    Invariant($"Band {band.GradeLetter} has a lower bound of {band.LowerBound} above its upper bound of {band.UpperBound}. Swap them or correct the band."),
                    i);
            }
        }

        // Rules 5-8, the coverage test: "sort by lower_bound and walk the list once" (6.2.5), which
        // finds every failure in a single pass and lets the message name the specific band and the
        // specific mark at fault. Original submitted index travels with each band for BandIndex.
        var sorted = bands
            .Select((band, index) => (Band: band, Index: index))
            .OrderBy(entry => entry.Band.LowerBound)
            .ToList();

        var first = sorted[0];
        if (first.Band.LowerBound != 0)
        {
            return Fail(
                DoesNotStartAtZeroCode,
                Invariant($"The scale starts at {first.Band.LowerBound}. Marks 0 to {first.Band.LowerBound - 1} belong to no band. Extend the lowest band down to 0."),
                first.Index);
        }

        for (var i = 0; i < sorted.Count - 1; i++)
        {
            var left = sorted[i];
            var right = sorted[i + 1];

            if (left.Band.UpperBound >= right.Band.LowerBound)
            {
                var overlapStart = right.Band.LowerBound;
                var overlapEnd = Math.Min(left.Band.UpperBound, right.Band.UpperBound);
                var marks = overlapStart == overlapEnd
                    ? Invariant($"mark {overlapStart}")
                    : overlapEnd - overlapStart == 1
                        ? Invariant($"marks {overlapStart} and {overlapEnd}")
                        : Invariant($"marks {overlapStart} to {overlapEnd}");

                return Fail(
                    BandsOverlapCode,
                    Invariant($"Band {left.Band.GradeLetter} ({left.Band.LowerBound} to {left.Band.UpperBound}) overlaps band {right.Band.GradeLetter} ({right.Band.LowerBound} to {right.Band.UpperBound}) at {marks}."),
                    left.Index);
            }

            if (left.Band.UpperBound + 1 < right.Band.LowerBound)
            {
                return Fail(
                    CoverageGapCode,
                    Invariant($"There is a gap between band {left.Band.GradeLetter} ({left.Band.LowerBound} to {left.Band.UpperBound}) and band {right.Band.GradeLetter} ({right.Band.LowerBound} to {right.Band.UpperBound}). Mark {left.Band.UpperBound + 1} belongs to no band."),
                    left.Index);
            }
        }

        var last = sorted[^1];
        if (last.Band.UpperBound != 100)
        {
            return Fail(
                DoesNotEndAtHundredCode,
                Invariant($"The scale ends at {last.Band.UpperBound}. Mark 100 belongs to no band. Extend the highest band up to 100."),
                last.Index);
        }

        // Rule 9: grade letters unique, case-insensitive, across the whole submitted set.
        var seenLetters = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        for (var i = 0; i < bands.Count; i++)
        {
            var letter = bands[i].GradeLetter;
            if (seenLetters.ContainsKey(letter))
            {
                return Fail(
                    DuplicateGradeLetterCode,
                    Invariant($"Two bands use the grade letter {letter}. Grade letters must be unique."),
                    i);
            }

            seenLetters[letter] = i;
        }

        // Rule 10: every band has a remark of at least three characters.
        for (var i = 0; i < bands.Count; i++)
        {
            var band = bands[i];
            var remark = band.Remark?.Trim() ?? string.Empty;

            if (remark.Length == 0)
            {
                return Fail(
                    RemarkTooShortCode,
                    Invariant($"Band {band.GradeLetter} has no remark. Every band needs a remark, for example Excellent."),
                    i);
            }

            if (remark.Length < GradingBand.RemarkMinLength)
            {
                return Fail(
                    RemarkTooShortCode,
                    Invariant($"Band {band.GradeLetter}'s remark is too short. Every band needs a remark of at least {GradingBand.RemarkMinLength} characters, for example Excellent."),
                    i);
            }
        }

        return Result.Success();
    }

    private static Result Fail(string code, string description, int bandIndex) =>
        Result.Failure(new GradingBandValidationError(code, description, bandIndex));

    private static string Invariant(FormattableString formattable) =>
        formattable.ToString(CultureInfo.InvariantCulture);
}
