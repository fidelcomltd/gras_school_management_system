using System.Globalization;
using SchoolManagement.Domain.Common;

namespace SchoolManagement.Domain.Settings;

/// <summary>
/// One submitted component, before persistence. <c>Id</c> is <see langword="null"/> for a new
/// component; when it matches an existing <see cref="AssessmentComponent"/>'s <c>Id</c> it is an edit to that
/// same row (see <see cref="AssessmentComponent"/>'s remarks on why identity must be preserved).
/// </summary>
/// <param name="Id">Opaque id of an existing component, or <see langword="null"/> for a new one.</param>
/// <param name="Name">Up to <see cref="AssessmentComponent.NameMaxLength"/> characters. Unique, case-insensitive.</param>
/// <param name="ShortLabel">Up to <see cref="AssessmentComponent.ShortLabelMaxLength"/> characters. Unique, case-insensitive.</param>
/// <param name="MaxMark"><see cref="AssessmentComponent.MaxMarkMinimum"/> to <see cref="AssessmentComponent.MaxMarkCeiling"/>.</param>
/// <param name="IsExamination">Exactly one submitted component must set this <see langword="true"/>.</param>
public sealed record AssessmentComponentInput(Guid? Id, string Name, string ShortLabel, int MaxMark, bool IsExamination);

/// <summary>
/// Spec 6.2.6's six save-time validation rules, run over the WHOLE submitted structure as one unit —
/// the same "atomic, first failure only" contract as <see cref="GradingScaleRules"/>.
/// </summary>
public static class AssessmentStructureRules
{
    /// <summary>Rule 1.</summary>
    public const string NoNonExaminationComponentCode = "settings.assessment.no_non_examination_component";

    /// <summary>Rule 2.</summary>
    public const string NotExactlyOneExaminationCode = "settings.assessment.not_exactly_one_examination";

    /// <summary>Rules 3 and 3b.</summary>
    public const string DoesNotTotalHundredCode = "settings.assessment.does_not_total_hundred";

    /// <summary>Rule 4.</summary>
    public const string MaxMarkOutOfRangeCode = "settings.assessment.max_mark_out_of_range";

    /// <summary>Rule 5.</summary>
    public const string DuplicateNameCode = "settings.assessment.duplicate_name";

    /// <summary>Rule 5, short-label half.</summary>
    public const string DuplicateShortLabelCode = "settings.assessment.duplicate_short_label";

    /// <summary>
    /// Validates <paramref name="components"/> against all six rules, in spec order, stopping at the
    /// first failure.
    /// </summary>
    public static Result ValidateWholeStructure(IReadOnlyList<AssessmentComponentInput> components)
    {
        ArgumentNullException.ThrowIfNull(components);

        var examinationCount = components.Count(component => component.IsExamination);
        var nonExaminationCount = components.Count - examinationCount;

        // Rule 1: at least one non-examination component exists.
        if (nonExaminationCount == 0)
        {
            return Fail(
                NoNonExaminationComponentCode,
                "Add at least one continuous assessment component. A structure of examination only is not supported.");
        }

        // Rule 2: exactly one component is flagged as the examination.
        if (examinationCount != 1)
        {
            return Fail(
                NotExactlyOneExaminationCode,
                Invariant($"Exactly one component must be marked as the examination. You have marked {examinationCount}."));
        }

        // Rules 3/3b: every maximum sums to exactly 100.
        var total = components.Sum(component => component.MaxMark);
        if (total < 100)
        {
            return Fail(
                DoesNotTotalHundredCode,
                Invariant($"The components total {total}. You are {100 - total} marks short of 100. Increase a maximum or add a component."));
        }

        if (total > 100)
        {
            return Fail(
                DoesNotTotalHundredCode,
                Invariant($"The components total {total}. You are {total - 100} marks over 100. Reduce a maximum or remove a component."));
        }

        // Rule 4: every maximum is within range. The spec names only the floor ("at least 1 mark");
        // the ceiling mirrors the field's own 1-100 bound (6.2.6's max_mark column) and is authored
        // copy — see backend/docs/ASSUMPTIONS.md.
        foreach (var component in components)
        {
            if (component.MaxMark < AssessmentComponent.MaxMarkMinimum)
            {
                return Fail(
                    MaxMarkOutOfRangeCode,
                    Invariant($"{component.Name} has a maximum of {component.MaxMark}. Every component must be worth at least {AssessmentComponent.MaxMarkMinimum} mark."));
            }

            if (component.MaxMark > AssessmentComponent.MaxMarkCeiling)
            {
                return Fail(
                    MaxMarkOutOfRangeCode,
                    Invariant($"{component.Name} has a maximum of {component.MaxMark}. Every component must be worth at most {AssessmentComponent.MaxMarkCeiling} marks."));
            }
        }

        // Rule 5: names and short labels are each unique, case-insensitive.
        var seenNames = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var component in components)
        {
            if (!seenNames.Add(component.Name))
            {
                return Fail(
                    DuplicateNameCode,
                    Invariant($"Two components are named {component.Name}. Component names must be unique."));
            }
        }

        var seenShortLabels = new HashSet<string>(StringComparer.OrdinalIgnoreCase);
        foreach (var component in components)
        {
            if (!seenShortLabels.Add(component.ShortLabel))
            {
                return Fail(
                    DuplicateShortLabelCode,
                    Invariant($"Two components use the short label {component.ShortLabel}. Component short labels must be unique."));
            }
        }

        return Result.Success();
    }

    private static Result Fail(string code, string description) =>
        Result.Failure(Error.Validation(code, description));

    private static string Invariant(FormattableString formattable) =>
        formattable.ToString(CultureInfo.InvariantCulture);
}
