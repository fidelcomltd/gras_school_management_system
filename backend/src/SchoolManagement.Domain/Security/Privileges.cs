namespace SchoolManagement.Domain.Security;

/// <summary>
/// THE PRIVILEGE REGISTER — every privilege the system understands, as immutable code-shipped
/// constants. Spec 4.4: "This table is the security contract for the product. Every route in the
/// system maps to exactly one privilege in this table. A route that maps to nothing is a defect."
/// </summary>
/// <remarks>
/// <para>
/// Admins cannot create privileges (spec 4.2) — that is what "shipped with the code" means here.
/// A role is an admin-created NAMED SET of these strings; the strings themselves are fixed.
/// </para>
/// <para>
/// Grouped by string prefix (module) rather than by the register's six document sections, because
/// that is how an engineer will look one up. <see cref="PrivilegeRegistry"/> is the flat list with
/// the <c>scopable</c> flag from spec 4.4 — consult it, not this file, to answer "is X scopable".
/// </para>
/// <para>
/// <c>PrivilegeRegistryTests.EveryConstantIsRegisteredAndViceVersa</c> keeps this file and
/// <see cref="PrivilegeRegistry"/> from drifting apart.
/// </para>
/// </remarks>
public static class Privileges
{
    /// <summary>Admin account management (spec 4.4.1). None are scopable.</summary>
    public static class Admin
    {
        /// <summary>List and open admin accounts.</summary>
        public const string View = "admin.view";

        /// <summary>Create an admin account.</summary>
        public const string Create = "admin.create";

        /// <summary>Edit an admin account's name, email, phone.</summary>
        public const string Update = "admin.update";

        /// <summary>Move an account to suspended and back to active.</summary>
        public const string Suspend = "admin.suspend";

        /// <summary>Move an account to deactivated. Irreversible except by a Super Admin reactivating it.</summary>
        public const string Deactivate = "admin.deactivate";

        /// <summary>Force a password reset for another account.</summary>
        public const string PasswordReset = "admin.password.reset";

        /// <summary>Kill another account's active sessions.</summary>
        public const string SessionRevoke = "admin.session.revoke";
    }

    /// <summary>Role management (spec 4.4.1). None are scopable.</summary>
    public static class Role
    {
        /// <summary>List roles and see their privilege sets.</summary>
        public const string View = "role.view";

        /// <summary>Create a role.</summary>
        public const string Create = "role.create";

        /// <summary>Add or remove privileges from a role.</summary>
        public const string Update = "role.update";

        /// <summary>Delete a role that has no active assignments.</summary>
        public const string Delete = "role.delete";

        /// <summary>Assign a role to an account school-wide.</summary>
        public const string Assign = "role.assign";

        /// <summary>Assign a role to an account over a named list of arms.</summary>
        public const string ScopeAssign = "role.scope.assign";
    }

    /// <summary>Audit log access (spec 4.4.1). None are scopable.</summary>
    public static class Audit
    {
        /// <summary>Read the audit log.</summary>
        public const string View = "audit.view";

        /// <summary>Export a filtered audit log to CSV.</summary>
        public const string Export = "audit.export";
    }

    /// <summary>System configuration (spec 4.4.2). None are scopable — see spec 4.3.</summary>
    public static class Settings
    {
        /// <summary>Read every settings page. Read-only.</summary>
        public const string View = "settings.view";

        /// <summary>
        /// Edit school name, short name, address, phone, email, motto, logo, head teacher name
        /// and signature image.
        /// </summary>
        public const string IdentityUpdate = "settings.identity.update";

        /// <summary>Edit the school abbreviation used in registration numbers.</summary>
        public const string AbbreviationUpdate = "settings.abbreviation.update";

        /// <summary>Edit serial width, separator and the reset rule for the registration number pattern.</summary>
        public const string RegNumberUpdate = "settings.regnumber.update";

        /// <summary>Add, edit, remove and reorder grading bands. Reset to seeded defaults.</summary>
        public const string GradingUpdate = "settings.grading.update";

        /// <summary>
        /// Add, rename, remove and reorder continuous assessment components, and set the
        /// examination maximum.
        /// </summary>
        public const string AssessmentUpdate = "settings.assessment.update";

        /// <summary>
        /// Edit the affective and psychomotor trait lists and which rating scale each block is rated
        /// against (spec 6.2.13; TASK-0072 stage 3b: each trait block references its own
        /// <see cref="RatingScalesUpdate"/>-managed scale by id, not one shared school-wide scale).
        /// </summary>
        public const string TraitsUpdate = "settings.traits.update";

        /// <summary>
        /// Edit annual computation method and weights, position scope, level position
        /// visibility, tie-breaking rule, pass mark, promotion threshold.
        /// </summary>
        public const string ResultRulesUpdate = "settings.resultrules.update";

        /// <summary>Edit default pin length, default maximum uses and the character set.</summary>
        public const string PinUpdate = "settings.pin.update";

        /// <summary>Restore the grading scale, assessment structure or trait lists to seeded values.</summary>
        public const string ResetDefaults = "settings.reset.defaults";

        /// <summary>
        /// Add, rename, remove and reorder rating scales and their points (spec 6.2.13; TASK-0072
        /// stage 1). Separate from <see cref="TraitsUpdate"/> because scales are now shared records
        /// referenced by any rating block, not owned by the trait screen alone.
        /// </summary>
        public const string RatingScalesUpdate = "settings.ratingscales.update";

        /// <summary>
        /// Add, rename, reorder, archive and remove development domains and indicators (spec 6.2.13;
        /// TASK-0072 stage 2b). Separate from <see cref="TraitsUpdate"/> for the same reason as
        /// <see cref="RatingScalesUpdate"/> — a distinct, nursery-only configuration screen, not the
        /// trait editor.
        /// </summary>
        public const string DevelopmentDomainsUpdate = "settings.developmentdomains.update";
    }

    /// <summary>Academic sessions and terms (spec 4.4.3). None are scopable.</summary>
    public static class Session
    {
        /// <summary>List sessions and terms.</summary>
        public const string View = "session.view";

        /// <summary>Create a session and its three terms.</summary>
        public const string Create = "session.create";

        /// <summary>Edit session and term dates, times school opened, resumption date.</summary>
        public const string Update = "session.update";
    }

    /// <summary>Term lifecycle (spec 4.4.3). None are scopable.</summary>
    public static class Term
    {
        /// <summary>Move a term from upcoming to active.</summary>
        public const string Open = "term.open";

        /// <summary>Move the active term to closed.</summary>
        public const string Close = "term.close";
    }

    /// <summary>
    /// Promotion (spans spec 4.4.3 and 4.4.5 — grouped here by shared string prefix). None are
    /// scopable.
    /// </summary>
    public static class Promotion
    {
        /// <summary>Run end-of-session promotion for a level or the whole school.</summary>
        public const string Run = "promotion.run";

        /// <summary>Reverse a promotion batch.</summary>
        public const string Reverse = "promotion.reverse";

        /// <summary>Override the system-proposed promotion status on a Third Term result.</summary>
        public const string Decide = "promotion.decide";
    }

    /// <summary>Class levels (spec 4.4.3). None are scopable.</summary>
    public static class Level
    {
        /// <summary>List and open class levels.</summary>
        public const string View = "level.view";

        /// <summary>Create a class level and place it in the progression chain.</summary>
        public const string Create = "level.create";

        /// <summary>Rename a level, change its section, reorder it, change its next level.</summary>
        public const string Update = "level.update";

        /// <summary>Deactivate or reactivate a level.</summary>
        public const string Deactivate = "level.deactivate";

        /// <summary>Hard delete a level that nothing has ever referenced.</summary>
        public const string Delete = "level.delete";
    }

    /// <summary>Arms (spec 4.4.3). <see cref="View"/> and <see cref="CapacityOverride"/> are scopable.</summary>
    public static class Arm
    {
        /// <summary>List and open arms. Scopable.</summary>
        public const string View = "arm.view";

        /// <summary>Create an arm under a level for a session, singly or in bulk.</summary>
        public const string Create = "arm.create";

        /// <summary>Edit an arm's label, capacity and status.</summary>
        public const string Update = "arm.update";

        /// <summary>Set or change the form teacher on an arm.</summary>
        public const string FormTeacherAssign = "arm.formteacher.assign";

        /// <summary>Hard delete an arm that has never held an enrolment.</summary>
        public const string Delete = "arm.delete";

        /// <summary>Enrol a pupil into an arm that is already at capacity. Scopable.</summary>
        public const string CapacityOverride = "arm.capacity.override";
    }

    /// <summary>Pupil records (spec 4.4.4).</summary>
    public static class Pupil
    {
        /// <summary>List and open pupil records. Scopable.</summary>
        public const string View = "pupil.view";

        /// <summary>Register a pupil and issue a registration number.</summary>
        public const string Create = "pupil.create";

        /// <summary>Edit pupil biographical fields. Scopable.</summary>
        public const string Update = "pupil.update";

        /// <summary>Upload or replace a pupil photograph. Scopable.</summary>
        public const string PhotoUpdate = "pupil.photo.update";

        /// <summary>Change status between active, transferred, withdrawn, graduated.</summary>
        public const string StatusUpdate = "pupil.status.update";

        /// <summary>Move a pupil between arms, singly or in bulk.</summary>
        public const string Transfer = "pupil.transfer";

        /// <summary>Run a bulk import from spreadsheet.</summary>
        public const string Import = "pupil.import";

        /// <summary>Correct a wrongly issued registration number.</summary>
        public const string RegNumberCorrect = "pupil.regnumber.correct";

        /// <summary>Approve a pending admission, moving it to active and issuing the registration number.</summary>
        public const string AdmissionApprove = "pupil.admission.approve";

        /// <summary>
        /// Approve an admission whose health questions the parent declined to answer, with a reason (spec 6.5.16).
        /// Not in spec 4.4's table: added by human ruling 2026-09-23.
        /// </summary>
        public const string AdmissionOverride = "pupil.admission.override";

        /// <summary>
        /// Read the section F health block and the barred-persons list. Every read is audited.
        /// Scopable.
        /// </summary>
        public const string SafeguardingView = "pupil.safeguarding.view";

        /// <summary>Edit the health block and the barred-persons list. Scopable.</summary>
        public const string SafeguardingUpdate = "pupil.safeguarding.update";

        /// <summary>Tick off and attach files against the section H admission document checklist. Scopable.</summary>
        public const string DocumentManage = "pupil.document.manage";

        /// <summary>Soft delete a pupil record that has no result history.</summary>
        public const string Delete = "pupil.delete";
    }

    /// <summary>
    /// Parent, guardian and emergency contacts (spec 4.4.4). Replaces the old <c>guardian.*</c>
    /// privileges — see <see cref="PrivilegeAliases"/>.
    /// </summary>
    public static class Contact
    {
        /// <summary>Add a parent, guardian or emergency contact. Scopable.</summary>
        public const string Create = "contact.create";

        /// <summary>Edit a contact. Scopable.</summary>
        public const string Update = "contact.update";

        /// <summary>See contact names, phone numbers and relationships across all five roles. Scopable.</summary>
        public const string View = "contact.view";
    }

    /// <summary>Weekly pastoral report sheets (spec 4.4.4, spec 6.10).</summary>
    public static class Weekly
    {
        /// <summary>List and read weekly report sheets. Scopable.</summary>
        public const string View = "weekly.view";

        /// <summary>Write and edit weekly report day notes. Scopable.</summary>
        public const string Enter = "weekly.enter";

        /// <summary>Publish or unpublish a week to the parent portal. Scopable.</summary>
        public const string Publish = "weekly.publish";
    }

    /// <summary>Subjects and their mappings (spec 4.4.4).</summary>
    public static class Subject
    {
        /// <summary>List subjects and see mappings. Scopable.</summary>
        public const string View = "subject.view";

        /// <summary>Create a subject.</summary>
        public const string Create = "subject.create";

        /// <summary>Edit subject name, code, description.</summary>
        public const string Update = "subject.update";

        /// <summary>Deactivate or reactivate a subject.</summary>
        public const string Deactivate = "subject.deactivate";

        /// <summary>Hard delete a subject never mapped and never scored.</summary>
        public const string Delete = "subject.delete";

        /// <summary>Map a subject to a level for a session and term.</summary>
        public const string Map = "subject.map";

        /// <summary>Create a per-arm exception to a level mapping.</summary>
        public const string MapArm = "subject.map.arm";

        /// <summary>End a mapping.</summary>
        public const string Unmap = "subject.unmap";
    }

    /// <summary>Results (spec 4.4.5).</summary>
    public static class Results
    {
        /// <summary>Open a score sheet or a computed result. Scopable.</summary>
        public const string View = "result.view";

        /// <summary>
        /// Enter and edit continuous assessment and examination marks while the result set is
        /// Draft or Returned for Correction. Scopable.
        /// </summary>
        public const string ScoreEnter = "result.score.enter";

        /// <summary>Void an already entered mark with a stated reason. Scopable.</summary>
        public const string ScoreVoid = "result.score.void";

        /// <summary>Enter affective and psychomotor ratings. Scopable.</summary>
        public const string TraitEnter = "result.trait.enter";

        /// <summary>Enter times present and times absent per pupil. Scopable.</summary>
        public const string AttendanceEnter = "result.attendance.enter";

        /// <summary>Write or edit the class teacher's remark. Scopable.</summary>
        public const string RemarkClassTeacher = "result.remark.classteacher";

        /// <summary>Write or edit the head teacher's remark.</summary>
        public const string RemarkHeadTeacher = "result.remark.headteacher";

        /// <summary>Run computation over a result set. Scopable.</summary>
        public const string Compute = "result.compute";

        /// <summary>Move a result set from Draft to Awaiting Approval. Scopable.</summary>
        public const string Submit = "result.submit";

        /// <summary>Move a result set from Awaiting Approval to Approved.</summary>
        public const string Approve = "result.approve";

        /// <summary>Return a result set to the class teacher with a reason.</summary>
        public const string Return = "result.return";

        /// <summary>Publish an approved result set and write the configuration snapshot.</summary>
        public const string Publish = "result.publish";

        /// <summary>Withdraw a published result set from the parent portal.</summary>
        public const string Unpublish = "result.unpublish";

        /// <summary>Compute annual cumulative results for an arm once Third Term is published.</summary>
        public const string AnnualCompute = "result.annual.compute";

        /// <summary>Render and download the result PDF from inside the back office. Scopable.</summary>
        public const string Print = "result.print";
    }

    /// <summary>Access pins (spec 4.4.6). None are scopable.</summary>
    public static class Pin
    {
        /// <summary>List pin batches and open a batch.</summary>
        public const string View = "pin.view";

        /// <summary>Generate a pin batch.</summary>
        public const string Generate = "pin.generate";

        /// <summary>Render the print run for a batch, which reveals pin plaintext once.</summary>
        public const string Print = "pin.print";

        /// <summary>Revoke a single pin or a whole batch.</summary>
        public const string Revoke = "pin.revoke";

        /// <summary>Read the usage report for a pin, including timestamps and truncated source addresses.</summary>
        public const string UsageView = "pin.usage.view";
    }

    /// <summary>Reports (spec 4.4.6).</summary>
    public static class Report
    {
        /// <summary>Open any report in section 10. Scopable.</summary>
        public const string View = "report.view";

        /// <summary>Export a report to CSV or PDF. Scopable.</summary>
        public const string Export = "report.export";
    }
}
