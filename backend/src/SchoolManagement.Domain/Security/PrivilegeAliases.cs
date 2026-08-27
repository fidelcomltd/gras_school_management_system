namespace SchoolManagement.Domain.Security;

/// <summary>
/// Legacy <c>guardian.*</c> privilege names, retained as aliases of their <c>contact.*</c>
/// replacements so a role or assignment written against the old name still resolves.
/// </summary>
/// <remarks>
/// Spec <c>02-data-model.md</c> §5.5 item 1: "<c>guardian</c> becomes <c>pupil_contact</c> with a
/// role... Existing references to <c>guardian.view</c> are aliased to <c>contact.view</c> rather
/// than broken." The register (spec 4.4.4) states the same for <c>contact.create</c> (replaces
/// <c>guardian.create</c>) and <c>contact.update</c> (replaces <c>guardian.update</c>); all three
/// old names are aliased here for consistency, not just the one the data-model note names
/// explicitly.
/// </remarks>
public static class PrivilegeAliases
{
    /// <summary>Old privilege code to its canonical replacement.</summary>
    public static readonly IReadOnlyDictionary<string, string> Map = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["guardian.view"] = Privileges.Contact.View,
        ["guardian.create"] = Privileges.Contact.Create,
        ["guardian.update"] = Privileges.Contact.Update,
    };

    /// <summary>
    /// Returns the canonical code for <paramref name="code"/>, resolving a legacy
    /// <c>guardian.*</c> alias if it is one. Any other code is returned unchanged, including one
    /// that is not in the register at all — this method aliases, it does not validate.
    /// </summary>
    /// <param name="code">The privilege code as supplied by a caller, a role, or a stored assignment.</param>
    public static string Resolve(string code) => Map.TryGetValue(code, out var canonical) ? canonical : code;
}
