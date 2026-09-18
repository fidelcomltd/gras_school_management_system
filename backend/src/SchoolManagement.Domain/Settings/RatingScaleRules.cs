using System.Globalization;
using SchoolManagement.Domain.Common;

namespace SchoolManagement.Domain.Settings;

/// <summary>One submitted point, before persistence (no id — <c>PUT /settings/rating-scales</c> replaces the whole set; see <see cref="RatingScale"/>'s remarks).</summary>
/// <param name="PointCode">Up to <see cref="RatingScalePoint.PointCodeMaxLength"/> character. Unique within the scale.</param>
/// <param name="PointLabel">Up to <see cref="RatingScalePoint.PointLabelMaxLength"/> characters.</param>
/// <param name="PointOrder">Ascending from worst to best. Unique within the scale.</param>
public sealed record RatingScalePointInput(string PointCode, string PointLabel, int PointOrder);

/// <summary>One submitted scale, before persistence.</summary>
/// <param name="Name">Up to <see cref="RatingScale.NameMaxLength"/> characters. Unique, case-insensitive, across the whole submitted set.</param>
/// <param name="Points">Between <see cref="RatingScalePoint.MinPointsPerScale"/> and <see cref="RatingScalePoint.MaxPointsPerScale"/> points.</param>
public sealed record RatingScaleInput(string Name, IReadOnlyList<RatingScalePointInput> Points);

/// <summary>
/// A rating-scale rejection carrying WHICH submitted scale (and, where the failure is about one
/// point, which point within it) failed — same shape as <see cref="GradingBandValidationError"/>'s
/// <c>BandIndex</c>, so the editor can highlight the offending row without re-parsing the message.
/// </summary>
/// <param name="Code">Stable error code.</param>
/// <param name="Description">The rule-specific message.</param>
/// <param name="ScaleIndex">0-based position of the offending scale in the submitted array. <c>-1</c> when the failure names no single scale.</param>
/// <param name="PointIndex">0-based position of the offending point within that scale, when the failure is about one point. <see langword="null"/> otherwise.</param>
public sealed record RatingScaleValidationError(string Code, string Description, int ScaleIndex, int? PointIndex)
    : Error(Code, Description, ErrorType.Validation);

/// <summary>
/// Save-time validation rules for the whole submitted rating-scale set (spec 6.2.13), run as one unit
/// exactly as <see cref="GradingScaleRules"/> validates a whole grading scale — a partially valid set
/// is never saved, and the first failure in the order below is the one returned.
/// </summary>
public static class RatingScaleRules
{
    /// <summary>Rule 1: point count, per scale.</summary>
    public const string PointCountInvalidCode = "settings.ratingscales.point_count_invalid";

    /// <summary>Rule 2: duplicate point code, within a scale.</summary>
    public const string DuplicatePointCodeCode = "settings.ratingscales.duplicate_point_code";

    /// <summary>Rule 3: duplicate point order, within a scale.</summary>
    public const string DuplicatePointOrderCode = "settings.ratingscales.duplicate_point_order";

    /// <summary>Rule 4: duplicate scale name, across the submitted set.</summary>
    public const string DuplicateNameCode = "settings.ratingscales.duplicate_name";

    /// <summary>
    /// Validates <paramref name="scales"/> against every rule, in order, stopping at the first
    /// failure. A successful <see cref="Result"/> means the whole set may be written.
    /// </summary>
    public static Result ValidateWholeSet(IReadOnlyList<RatingScaleInput> scales)
    {
        ArgumentNullException.ThrowIfNull(scales);

        // Rules 1-3 run per scale, in submitted order, so the FIRST offending scale is the one named.
        for (var scaleIndex = 0; scaleIndex < scales.Count; scaleIndex++)
        {
            var scale = scales[scaleIndex];
            var points = scale.Points ?? [];

            // Rule 1: 6.2.7's carried-forward rule — "The scale must have between 2 and 9 points."
            if (points.Count < RatingScalePoint.MinPointsPerScale)
            {
                return Fail(
                    PointCountInvalidCode,
                    "A rating scale needs at least two points.",
                    scaleIndex,
                    null);
            }

            if (points.Count > RatingScalePoint.MaxPointsPerScale)
            {
                return Fail(
                    PointCountInvalidCode,
                    Invariant($"Scale {scale.Name} has {points.Count} points. A rating scale may have at most {RatingScalePoint.MaxPointsPerScale} points."),
                    scaleIndex,
                    null);
            }

            // Rule 2: point codes unique within the scale, case-insensitive.
            var seenCodes = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
            for (var pointIndex = 0; pointIndex < points.Count; pointIndex++)
            {
                var code = points[pointIndex].PointCode;
                if (code is not null && seenCodes.ContainsKey(code))
                {
                    return Fail(
                        DuplicatePointCodeCode,
                        Invariant($"Scale {scale.Name} uses the point code {code} more than once. Point codes must be unique within a scale."),
                        scaleIndex,
                        pointIndex);
                }

                if (code is not null)
                {
                    seenCodes[code] = pointIndex;
                }
            }

            // Rule 3: point orders unique within the scale.
            var seenOrders = new Dictionary<int, int>();
            for (var pointIndex = 0; pointIndex < points.Count; pointIndex++)
            {
                var order = points[pointIndex].PointOrder;
                if (seenOrders.ContainsKey(order))
                {
                    return Fail(
                        DuplicatePointOrderCode,
                        Invariant($"Scale {scale.Name} uses the point order {order} more than once. Point orders must be unique within a scale."),
                        scaleIndex,
                        pointIndex);
                }

                seenOrders[order] = pointIndex;
            }
        }

        // Rule 4: scale names unique across the whole submitted set, case-insensitive.
        var seenNames = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);
        for (var scaleIndex = 0; scaleIndex < scales.Count; scaleIndex++)
        {
            var name = scales[scaleIndex].Name;
            if (name is not null && seenNames.ContainsKey(name))
            {
                return Fail(
                    DuplicateNameCode,
                    Invariant($"Two scales are named {name}. Scale names must be unique."),
                    scaleIndex,
                    null);
            }

            if (name is not null)
            {
                seenNames[name] = scaleIndex;
            }
        }

        return Result.Success();
    }

    private static Result Fail(string code, string description, int scaleIndex, int? pointIndex) =>
        Result.Failure(new RatingScaleValidationError(code, description, scaleIndex, pointIndex));

    private static string Invariant(FormattableString formattable) =>
        formattable.ToString(CultureInfo.InvariantCulture);
}
