namespace SchoolManagement.Domain.Security;

/// <summary>
/// The flat, authoritative list behind <see cref="Privileges"/>: every privilege code paired with
/// its <c>scopable</c> flag from spec 4.4. This is what code asks "does X exist" and "is X
/// scopable" against — never a hand-rolled switch scattered through the codebase.
/// </summary>
public static class PrivilegeRegistry
{
    /// <summary>
    /// Every privilege in the register, in the order spec 4.4 lists them (4.4.1 through 4.4.6).
    /// </summary>
    public static readonly IReadOnlyList<PrivilegeDefinition> All =
    [
        // 4.4.1 Administration and access control
        new(Privileges.Admin.View, Scopable: false),
        new(Privileges.Admin.Create, Scopable: false),
        new(Privileges.Admin.Update, Scopable: false),
        new(Privileges.Admin.Suspend, Scopable: false),
        new(Privileges.Admin.Deactivate, Scopable: false),
        new(Privileges.Admin.PasswordReset, Scopable: false),
        new(Privileges.Admin.SessionRevoke, Scopable: false),
        new(Privileges.Role.View, Scopable: false),
        new(Privileges.Role.Create, Scopable: false),
        new(Privileges.Role.Update, Scopable: false),
        new(Privileges.Role.Delete, Scopable: false),
        new(Privileges.Role.Assign, Scopable: false),
        new(Privileges.Role.ScopeAssign, Scopable: false),
        new(Privileges.Audit.View, Scopable: false),
        new(Privileges.Audit.Export, Scopable: false),

        // 4.4.2 Settings
        new(Privileges.Settings.View, Scopable: false),
        new(Privileges.Settings.IdentityUpdate, Scopable: false),
        new(Privileges.Settings.AbbreviationUpdate, Scopable: false),
        new(Privileges.Settings.RegNumberUpdate, Scopable: false),
        new(Privileges.Settings.GradingUpdate, Scopable: false),
        new(Privileges.Settings.AssessmentUpdate, Scopable: false),
        new(Privileges.Settings.TraitsUpdate, Scopable: false),
        new(Privileges.Settings.ResultRulesUpdate, Scopable: false),
        new(Privileges.Settings.PinUpdate, Scopable: false),
        new(Privileges.Settings.ResetDefaults, Scopable: false),

        // 4.4.3 Academic structure
        new(Privileges.Session.View, Scopable: false),
        new(Privileges.Session.Create, Scopable: false),
        new(Privileges.Session.Update, Scopable: false),
        new(Privileges.Term.Open, Scopable: false),
        new(Privileges.Term.Close, Scopable: false),
        new(Privileges.Promotion.Run, Scopable: false),
        new(Privileges.Promotion.Reverse, Scopable: false),
        new(Privileges.Level.View, Scopable: false),
        new(Privileges.Level.Create, Scopable: false),
        new(Privileges.Level.Update, Scopable: false),
        new(Privileges.Level.Deactivate, Scopable: false),
        new(Privileges.Level.Delete, Scopable: false),
        new(Privileges.Arm.View, Scopable: true),
        new(Privileges.Arm.Create, Scopable: false),
        new(Privileges.Arm.Update, Scopable: false),
        new(Privileges.Arm.FormTeacherAssign, Scopable: false),
        new(Privileges.Arm.Delete, Scopable: false),
        new(Privileges.Arm.CapacityOverride, Scopable: true),

        // 4.4.4 Pupils, guardians and subjects
        new(Privileges.Pupil.View, Scopable: true),
        new(Privileges.Pupil.Create, Scopable: false),
        new(Privileges.Pupil.Update, Scopable: true),
        new(Privileges.Pupil.PhotoUpdate, Scopable: true),
        new(Privileges.Pupil.StatusUpdate, Scopable: false),
        new(Privileges.Pupil.Transfer, Scopable: false),
        new(Privileges.Pupil.Import, Scopable: false),
        new(Privileges.Pupil.RegNumberCorrect, Scopable: false),
        new(Privileges.Pupil.AdmissionApprove, Scopable: false),
        new(Privileges.Pupil.SafeguardingView, Scopable: true),
        new(Privileges.Pupil.SafeguardingUpdate, Scopable: true),
        new(Privileges.Pupil.DocumentManage, Scopable: true),
        new(Privileges.Contact.Create, Scopable: true),
        new(Privileges.Contact.Update, Scopable: true),
        new(Privileges.Weekly.View, Scopable: true),
        new(Privileges.Weekly.Enter, Scopable: true),
        new(Privileges.Weekly.Publish, Scopable: true),
        new(Privileges.Pupil.Delete, Scopable: false),
        new(Privileges.Contact.View, Scopable: true),
        new(Privileges.Subject.View, Scopable: true),
        new(Privileges.Subject.Create, Scopable: false),
        new(Privileges.Subject.Update, Scopable: false),
        new(Privileges.Subject.Deactivate, Scopable: false),
        new(Privileges.Subject.Delete, Scopable: false),
        new(Privileges.Subject.Map, Scopable: false),
        new(Privileges.Subject.MapArm, Scopable: false),
        new(Privileges.Subject.Unmap, Scopable: false),

        // 4.4.5 Results
        new(Privileges.Results.View, Scopable: true),
        new(Privileges.Results.ScoreEnter, Scopable: true),
        new(Privileges.Results.ScoreVoid, Scopable: true),
        new(Privileges.Results.TraitEnter, Scopable: true),
        new(Privileges.Results.AttendanceEnter, Scopable: true),
        new(Privileges.Results.RemarkClassTeacher, Scopable: true),
        new(Privileges.Results.RemarkHeadTeacher, Scopable: false),
        new(Privileges.Results.Compute, Scopable: true),
        new(Privileges.Results.Submit, Scopable: true),
        new(Privileges.Results.Approve, Scopable: false),
        new(Privileges.Results.Return, Scopable: false),
        new(Privileges.Results.Publish, Scopable: false),
        new(Privileges.Results.Unpublish, Scopable: false),
        new(Privileges.Results.AnnualCompute, Scopable: false),
        new(Privileges.Results.Print, Scopable: true),
        new(Privileges.Promotion.Decide, Scopable: false),

        // 4.4.6 Pins and reports
        new(Privileges.Pin.View, Scopable: false),
        new(Privileges.Pin.Generate, Scopable: false),
        new(Privileges.Pin.Print, Scopable: false),
        new(Privileges.Pin.Revoke, Scopable: false),
        new(Privileges.Pin.UsageView, Scopable: false),
        new(Privileges.Report.View, Scopable: true),
        new(Privileges.Report.Export, Scopable: true),
    ];

    private static readonly Dictionary<string, PrivilegeDefinition> ByCode =
        All.ToDictionary(definition => definition.Code, StringComparer.Ordinal);

    /// <summary>Whether <paramref name="code"/> (after alias resolution) is in the register.</summary>
    /// <param name="code">The privilege code to look up.</param>
    public static bool Exists(string code) => ByCode.ContainsKey(PrivilegeAliases.Resolve(code));

    /// <summary>
    /// Looks up a privilege's definition, resolving a legacy <c>guardian.*</c> alias first.
    /// </summary>
    /// <param name="code">The privilege code to look up.</param>
    /// <param name="definition">The definition, when found.</param>
    public static bool TryGet(string code, out PrivilegeDefinition? definition) =>
        ByCode.TryGetValue(PrivilegeAliases.Resolve(code), out definition);

    /// <summary>Whether the privilege (after alias resolution) may be granted over an arm list.</summary>
    /// <param name="code">The privilege code to check.</param>
    /// <exception cref="ArgumentException"><paramref name="code"/> is not in the register.</exception>
    public static bool IsScopable(string code)
    {
        if (!TryGet(code, out var definition))
        {
            throw new ArgumentException($"'{code}' is not in the privilege register.", nameof(code));
        }

        return definition!.Scopable;
    }
}
