namespace SchoolManagement.Domain.Security;

/// <summary>
/// The flat, authoritative list behind <see cref="Privileges"/>: every privilege code paired with
/// its <c>scopable</c> flag, module group and verbatim "Permits" sentence from spec 4.4. This is
/// what code asks "does X exist", "is X scopable" and "what module is X in" against — never a
/// hand-rolled switch scattered through the codebase.
/// </summary>
public static class PrivilegeRegistry
{
    /// <summary>
    /// Every privilege in the register, in the order spec 4.4 lists them (4.4.1 through 4.4.6).
    /// </summary>
    public static readonly IReadOnlyList<PrivilegeDefinition> All =
    [
        // 4.4.1 Administration and access control
        new(Privileges.Admin.View, Scopable: false, Module: PrivilegeModule.Administration,
            Permits: "List and open admin accounts."),
        new(Privileges.Admin.Create, Scopable: false, Module: PrivilegeModule.Administration,
            Permits: "Create an admin account."),
        new(Privileges.Admin.Update, Scopable: false, Module: PrivilegeModule.Administration,
            Permits: "Edit an admin account's name, email, phone."),
        new(Privileges.Admin.Suspend, Scopable: false, Module: PrivilegeModule.Administration,
            Permits: "Move an account to suspended and back to active."),
        new(Privileges.Admin.Deactivate, Scopable: false, Module: PrivilegeModule.Administration,
            Permits: "Move an account to deactivated. Irreversible except by a Super Admin reactivating it."),
        new(Privileges.Admin.PasswordReset, Scopable: false, Module: PrivilegeModule.Administration,
            Permits: "Force a password reset for another account."),
        new(Privileges.Admin.SessionRevoke, Scopable: false, Module: PrivilegeModule.Administration,
            Permits: "Kill another account's active sessions."),
        new(Privileges.Role.View, Scopable: false, Module: PrivilegeModule.Administration,
            Permits: "List roles and see their privilege sets."),
        new(Privileges.Role.Create, Scopable: false, Module: PrivilegeModule.Administration,
            Permits: "Create a role."),
        new(Privileges.Role.Update, Scopable: false, Module: PrivilegeModule.Administration,
            Permits: "Add or remove privileges from a role."),
        new(Privileges.Role.Delete, Scopable: false, Module: PrivilegeModule.Administration,
            Permits: "Delete a role that has no active assignments."),
        new(Privileges.Role.Assign, Scopable: false, Module: PrivilegeModule.Administration,
            Permits: "Assign a role to an account school-wide."),
        new(Privileges.Role.ScopeAssign, Scopable: false, Module: PrivilegeModule.Administration,
            Permits: "Assign a role to an account over a named list of arms."),
        new(Privileges.Audit.View, Scopable: false, Module: PrivilegeModule.Administration,
            Permits: "Read the audit log."),
        new(Privileges.Audit.Export, Scopable: false, Module: PrivilegeModule.Administration,
            Permits: "Export a filtered audit log to CSV."),

        // 4.4.2 Settings
        new(Privileges.Settings.View, Scopable: false, Module: PrivilegeModule.Settings,
            Permits: "Read every settings page. Read-only."),
        new(Privileges.Settings.IdentityUpdate, Scopable: false, Module: PrivilegeModule.Settings,
            Permits: "Edit school name, short name, address, phone, email, motto, logo, head teacher name and signature image."),
        new(Privileges.Settings.AbbreviationUpdate, Scopable: false, Module: PrivilegeModule.Settings,
            Permits: "Edit the school abbreviation used in registration numbers. Split out from identity because it has consequences identity fields do not."),
        new(Privileges.Settings.RegNumberUpdate, Scopable: false, Module: PrivilegeModule.Settings,
            Permits: "Edit serial width, separator and the reset rule for the registration number pattern."),
        new(Privileges.Settings.GradingUpdate, Scopable: false, Module: PrivilegeModule.Settings,
            Permits: "Add, edit, remove and reorder grading bands. Reset to seeded defaults."),
        new(Privileges.Settings.AssessmentUpdate, Scopable: false, Module: PrivilegeModule.Settings,
            Permits: "Add, rename, remove and reorder continuous assessment components, and set the examination maximum."),
        new(Privileges.Settings.TraitsUpdate, Scopable: false, Module: PrivilegeModule.Settings,
            Permits: "Edit the affective and psychomotor trait lists and the trait rating scale."),
        new(Privileges.Settings.ResultRulesUpdate, Scopable: false, Module: PrivilegeModule.Settings,
            Permits: "Edit annual computation method and weights, position scope, level position visibility, tie-breaking rule, pass mark, promotion threshold."),
        new(Privileges.Settings.PinUpdate, Scopable: false, Module: PrivilegeModule.Settings,
            Permits: "Edit default pin length, default maximum uses and the character set."),
        new(Privileges.Settings.ResetDefaults, Scopable: false, Module: PrivilegeModule.Settings,
            Permits: "Restore the grading scale, assessment structure or trait lists to seeded values."),
        new(Privileges.Settings.RatingScalesUpdate, Scopable: false, Module: PrivilegeModule.Settings,
            Permits: "Add, rename, remove and reorder rating scales and their points."),
        new(Privileges.Settings.DevelopmentDomainsUpdate, Scopable: false, Module: PrivilegeModule.Settings,
            Permits: "Add, rename, reorder, archive and remove development domains and indicators."),

        // 4.4.3 Academic structure
        new(Privileges.Session.View, Scopable: false, Module: PrivilegeModule.AcademicStructure,
            Permits: "List sessions and terms."),
        new(Privileges.Session.Create, Scopable: false, Module: PrivilegeModule.AcademicStructure,
            Permits: "Create a session and its three terms."),
        new(Privileges.Session.Update, Scopable: false, Module: PrivilegeModule.AcademicStructure,
            Permits: "Edit session and term dates, times school opened, resumption date."),
        new(Privileges.Term.Open, Scopable: false, Module: PrivilegeModule.AcademicStructure,
            Permits: "Move a term from upcoming to active."),
        new(Privileges.Term.Close, Scopable: false, Module: PrivilegeModule.AcademicStructure,
            Permits: "Move the active term to closed."),
        new(Privileges.Promotion.Run, Scopable: false, Module: PrivilegeModule.AcademicStructure,
            Permits: "Run end-of-session promotion for a level or the whole school."),
        new(Privileges.Promotion.Reverse, Scopable: false, Module: PrivilegeModule.AcademicStructure,
            Permits: "Reverse a promotion batch."),
        new(Privileges.Level.View, Scopable: false, Module: PrivilegeModule.AcademicStructure,
            Permits: "List and open class levels."),
        new(Privileges.Level.Create, Scopable: false, Module: PrivilegeModule.AcademicStructure,
            Permits: "Create a class level and place it in the progression chain."),
        new(Privileges.Level.Update, Scopable: false, Module: PrivilegeModule.AcademicStructure,
            Permits: "Rename a level, change its section, reorder it, change its next level."),
        new(Privileges.Level.Deactivate, Scopable: false, Module: PrivilegeModule.AcademicStructure,
            Permits: "Deactivate or reactivate a level."),
        new(Privileges.Level.Delete, Scopable: false, Module: PrivilegeModule.AcademicStructure,
            Permits: "Hard delete a level that nothing has ever referenced."),
        new(Privileges.Arm.View, Scopable: true, Module: PrivilegeModule.AcademicStructure,
            Permits: "List and open arms."),
        new(Privileges.Arm.Create, Scopable: false, Module: PrivilegeModule.AcademicStructure,
            Permits: "Create an arm under a level for a session, singly or in bulk."),
        new(Privileges.Arm.Update, Scopable: false, Module: PrivilegeModule.AcademicStructure,
            Permits: "Edit an arm's label, capacity and status."),
        new(Privileges.Arm.FormTeacherAssign, Scopable: false, Module: PrivilegeModule.AcademicStructure,
            Permits: "Set or change the form teacher on an arm."),
        new(Privileges.Arm.Delete, Scopable: false, Module: PrivilegeModule.AcademicStructure,
            Permits: "Hard delete an arm that has never held an enrolment."),
        new(Privileges.Arm.CapacityOverride, Scopable: true, Module: PrivilegeModule.AcademicStructure,
            Permits: "Enrol a pupil into an arm that is already at capacity."),

        // 4.4.4 Pupils, guardians and subjects
        new(Privileges.Pupil.View, Scopable: true, Module: PrivilegeModule.PupilsAndSubjects,
            Permits: "List and open pupil records."),
        new(Privileges.Pupil.Create, Scopable: false, Module: PrivilegeModule.PupilsAndSubjects,
            Permits: "Register a pupil and issue a registration number."),
        new(Privileges.Pupil.Update, Scopable: true, Module: PrivilegeModule.PupilsAndSubjects,
            Permits: "Edit pupil biographical fields."),
        new(Privileges.Pupil.PhotoUpdate, Scopable: true, Module: PrivilegeModule.PupilsAndSubjects,
            Permits: "Upload or replace a pupil photograph."),
        new(Privileges.Pupil.StatusUpdate, Scopable: false, Module: PrivilegeModule.PupilsAndSubjects,
            Permits: "Change status between active, transferred, withdrawn, graduated."),
        new(Privileges.Pupil.Transfer, Scopable: false, Module: PrivilegeModule.PupilsAndSubjects,
            Permits: "Move a pupil between arms, singly or in bulk."),
        new(Privileges.Pupil.Import, Scopable: false, Module: PrivilegeModule.PupilsAndSubjects,
            Permits: "Run a bulk import from spreadsheet."),
        new(Privileges.Pupil.RegNumberCorrect, Scopable: false, Module: PrivilegeModule.PupilsAndSubjects,
            Permits: "Correct a wrongly issued registration number."),
        new(Privileges.Pupil.AdmissionApprove, Scopable: false, Module: PrivilegeModule.PupilsAndSubjects,
            Permits: "Approve a pending admission, moving it to active and issuing the registration number, per 6.5.11."),

        // NOT from spec 4.4's table: human ruling 2026-09-23 gives spec 6.5.16's head-teacher override its own privilege.
        new(Privileges.Pupil.AdmissionOverride, Scopable: false, Module: PrivilegeModule.PupilsAndSubjects,
            Permits: "Approve an admission whose health questions the parent declined to answer, with a recorded reason, per 6.5.16."),
        new(Privileges.Pupil.SafeguardingView, Scopable: true, Module: PrivilegeModule.PupilsAndSubjects,
            Permits: "Read the section F health block and the barred-persons list, per 6.5.6 and 6.5.7. Every read is audited. Deliberately withheld from the Bursar and the Auditor."),
        new(Privileges.Pupil.SafeguardingUpdate, Scopable: true, Module: PrivilegeModule.PupilsAndSubjects,
            Permits: "Edit the health block and the barred-persons list."),
        new(Privileges.Pupil.DocumentManage, Scopable: true, Module: PrivilegeModule.PupilsAndSubjects,
            Permits: "Tick off and attach files against the section H admission document checklist."),
        new(Privileges.Contact.Create, Scopable: true, Module: PrivilegeModule.PupilsAndSubjects,
            Permits: "Add a parent, guardian or emergency contact. Replaces `guardian.create`, since one entity now serves all five contact roles per 6.5.5."),
        new(Privileges.Contact.Update, Scopable: true, Module: PrivilegeModule.PupilsAndSubjects,
            Permits: "Edit a contact. Replaces `guardian.update`."),
        new(Privileges.Weekly.View, Scopable: true, Module: PrivilegeModule.PupilsAndSubjects,
            Permits: "List and read weekly report sheets, per 6.10."),
        new(Privileges.Weekly.Enter, Scopable: true, Module: PrivilegeModule.PupilsAndSubjects,
            Permits: "Write and edit weekly report day notes."),
        new(Privileges.Weekly.Publish, Scopable: true, Module: PrivilegeModule.PupilsAndSubjects,
            Permits: "Publish or unpublish a week to the parent portal."),
        new(Privileges.Pupil.Delete, Scopable: false, Module: PrivilegeModule.PupilsAndSubjects,
            Permits: "Soft delete a pupil record that has no result history."),
        new(Privileges.Contact.View, Scopable: true, Module: PrivilegeModule.PupilsAndSubjects,
            Permits: "See contact names, phone numbers and relationships across all five roles in 6.5.5. Formerly `guardian.view`; the old name is retained as an alias in the seed data so existing role assignments do not break."),
        new(Privileges.Subject.View, Scopable: true, Module: PrivilegeModule.PupilsAndSubjects,
            Permits: "List subjects and see mappings."),
        new(Privileges.Subject.Create, Scopable: false, Module: PrivilegeModule.PupilsAndSubjects,
            Permits: "Create a subject."),
        new(Privileges.Subject.Update, Scopable: false, Module: PrivilegeModule.PupilsAndSubjects,
            Permits: "Edit subject name, code, description."),
        new(Privileges.Subject.Deactivate, Scopable: false, Module: PrivilegeModule.PupilsAndSubjects,
            Permits: "Deactivate or reactivate a subject."),
        new(Privileges.Subject.Delete, Scopable: false, Module: PrivilegeModule.PupilsAndSubjects,
            Permits: "Hard delete a subject never mapped and never scored."),
        new(Privileges.Subject.Map, Scopable: false, Module: PrivilegeModule.PupilsAndSubjects,
            Permits: "Map a subject to a level for a session and term."),
        new(Privileges.Subject.MapArm, Scopable: false, Module: PrivilegeModule.PupilsAndSubjects,
            Permits: "Create a per-arm exception to a level mapping."),
        new(Privileges.Subject.Unmap, Scopable: false, Module: PrivilegeModule.PupilsAndSubjects,
            Permits: "End a mapping."),

        // 4.4.5 Results
        new(Privileges.Results.View, Scopable: true, Module: PrivilegeModule.Results,
            Permits: "Open a score sheet or a computed result."),
        new(Privileges.Results.ScoreEnter, Scopable: true, Module: PrivilegeModule.Results,
            Permits: "Enter and edit continuous assessment and examination marks while the result set is Draft or Returned for Correction."),
        new(Privileges.Results.ScoreVoid, Scopable: true, Module: PrivilegeModule.Results,
            Permits: "Void an already entered mark with a stated reason, so that a mapping can be ended or an error unwound."),
        new(Privileges.Results.TraitEnter, Scopable: true, Module: PrivilegeModule.Results,
            Permits: "Enter affective and psychomotor ratings."),
        new(Privileges.Results.AttendanceEnter, Scopable: true, Module: PrivilegeModule.Results,
            Permits: "Enter times present and times absent per pupil."),
        new(Privileges.Results.RemarkClassTeacher, Scopable: true, Module: PrivilegeModule.Results,
            Permits: "Write or edit the class teacher's remark."),
        new(Privileges.Results.RemarkHeadTeacher, Scopable: false, Module: PrivilegeModule.Results,
            Permits: "Write or edit the head teacher's remark."),
        new(Privileges.Results.Compute, Scopable: true, Module: PrivilegeModule.Results,
            Permits: "Run computation over a result set."),
        new(Privileges.Results.Submit, Scopable: true, Module: PrivilegeModule.Results,
            Permits: "Move a result set from Draft to Awaiting Approval."),
        new(Privileges.Results.Approve, Scopable: false, Module: PrivilegeModule.Results,
            Permits: "Move a result set from Awaiting Approval to Approved."),
        new(Privileges.Results.Return, Scopable: false, Module: PrivilegeModule.Results,
            Permits: "Return a result set to the class teacher with a reason."),
        new(Privileges.Results.Publish, Scopable: false, Module: PrivilegeModule.Results,
            Permits: "Publish an approved result set and write the configuration snapshot."),
        new(Privileges.Results.Unpublish, Scopable: false, Module: PrivilegeModule.Results,
            Permits: "Withdraw a published result set from the parent portal."),
        new(Privileges.Results.AnnualCompute, Scopable: false, Module: PrivilegeModule.Results,
            Permits: "Compute annual cumulative results for an arm once Third Term is published."),
        new(Privileges.Results.Print, Scopable: true, Module: PrivilegeModule.Results,
            Permits: "Render and download the result PDF from inside the back office."),
        new(Privileges.Promotion.Decide, Scopable: false, Module: PrivilegeModule.Results,
            Permits: "Override the system-proposed promotion status on a Third Term result."),

        // 4.4.6 Pins and reports
        new(Privileges.Pin.View, Scopable: false, Module: PrivilegeModule.PinsAndReports,
            Permits: "List pin batches and open a batch."),
        new(Privileges.Pin.Generate, Scopable: false, Module: PrivilegeModule.PinsAndReports,
            Permits: "Generate a pin batch."),
        new(Privileges.Pin.Print, Scopable: false, Module: PrivilegeModule.PinsAndReports,
            Permits: "Render the print run for a batch, which reveals pin plaintext once."),
        new(Privileges.Pin.Revoke, Scopable: false, Module: PrivilegeModule.PinsAndReports,
            Permits: "Revoke a single pin or a whole batch."),
        new(Privileges.Pin.UsageView, Scopable: false, Module: PrivilegeModule.PinsAndReports,
            Permits: "Read the usage report for a pin, including timestamps and truncated source addresses."),
        new(Privileges.Report.View, Scopable: true, Module: PrivilegeModule.PinsAndReports,
            Permits: "Open any report in section 10."),
        new(Privileges.Report.Export, Scopable: true, Module: PrivilegeModule.PinsAndReports,
            Permits: "Export a report to CSV or PDF."),
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
