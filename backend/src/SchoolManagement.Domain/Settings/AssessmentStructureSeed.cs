namespace SchoolManagement.Domain.Settings;

/// <summary>
/// Spec 6.2.13's amendment seed: three components (1st CA 20, 2nd CA 20, Exam 60), replacing 6.2.6's
/// superseded four-component 15/15/10/60 table. <c>short_label</c> values are NOT given by 6.2.13's
/// three-column table (only Component/Maximum/Is examination/Display order) — authored here following
/// 6.2.6's own example convention ("CA1, EXAM"); see <c>backend/docs/ASSUMPTIONS.md</c>.
/// </summary>
/// <remarks>
/// <para>
/// <see cref="GrasDefaultComponents"/> is the SINGLE source both the install-time migration seed
/// (<c>AssessmentComponentConfiguration.HasData</c>) and a fresh install's assessment structure build
/// from — see <see cref="GradingScaleSeed"/>'s remarks for the identical relationship on the grading
/// side.
/// </para>
/// <para>
/// <see cref="WithAssignmentComponents"/> is 6.2.13's SECOND named profile — "a convenience that
/// writes rows into <c>assessment_component</c>. It is not a mode, it is not stored on the school, and
/// nothing later branches on which was chosen." No endpoint or parameter selects it: a school (or a
/// test proving the profile is genuinely reachable) submits this exact shape through the ordinary
/// <c>PUT /settings/assessment</c>, which validates and saves it with no special-casing whatsoever —
/// that absence of special-casing is what "nothing branches on the choice" means at the API level.
/// </para>
/// </remarks>
public static class AssessmentStructureSeed
{
    /// <summary>The <c>gras_default</c> profile (20/20/60) — what a fresh database seeds.</summary>
    public static readonly IReadOnlyList<AssessmentComponentInput> GrasDefaultComponents =
    [
        new(null, "1st CA", "CA1", 20, false),
        new(null, "2nd CA", "CA2", 20, false),
        new(null, "Exam", "EXAM", 60, true),
    ];

    /// <summary>The <c>with_assignment</c> profile (15/15/10/60) — offered as a first-run convenience, never seeded.</summary>
    public static readonly IReadOnlyList<AssessmentComponentInput> WithAssignmentComponents =
    [
        new(null, "1st CA", "CA1", 15, false),
        new(null, "2nd CA", "CA2", 15, false),
        new(null, "Assignment", "ASSGN", 10, false),
        new(null, "Exam", "EXAM", 60, true),
    ];
}
