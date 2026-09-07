namespace SchoolManagement.Domain.Security;

/// <summary>
/// Spec-derived metadata for each <see cref="PrivilegeModule"/>: the approved contract delta's
/// group key and the verbatim spec 4.4.x section heading.
/// </summary>
/// <remarks>
/// Approved contract delta: <c>.agent/decisions/2026-Q3-contract-deltas.md</c>, entry
/// <c>TASK-0028</c>, section 1 — the six keys (<c>administration</c>, <c>settings</c>,
/// <c>academic_structure</c>, <c>pupils_and_subjects</c>, <c>results</c>, <c>pins_and_reports</c>)
/// and titles are fixed by that delta, not derived from <see cref="PrivilegeModule"/>'s own member
/// names.
/// </remarks>
public static class PrivilegeModuleCatalog
{
    private static readonly Dictionary<PrivilegeModule, (string Key, string Title)> Entries =
        new()
        {
            [PrivilegeModule.Administration] = ("administration", "Administration and access control"),
            [PrivilegeModule.Settings] = ("settings", "Settings"),
            [PrivilegeModule.AcademicStructure] = ("academic_structure", "Academic structure"),
            [PrivilegeModule.PupilsAndSubjects] = ("pupils_and_subjects", "Pupils, guardians and subjects"),
            [PrivilegeModule.Results] = ("results", "Results"),
            [PrivilegeModule.PinsAndReports] = ("pins_and_reports", "Pins and reports"),
        };

    /// <summary>
    /// The approved contract delta's lowercase, underscore-separated key for <paramref name="module"/>.
    /// </summary>
    /// <param name="module">The module to look up.</param>
    public static string KeyFor(PrivilegeModule module) => Entries[module].Key;

    /// <summary>The verbatim spec 4.4.x section heading for <paramref name="module"/>.</summary>
    /// <param name="module">The module to look up.</param>
    public static string TitleFor(PrivilegeModule module) => Entries[module].Title;
}
