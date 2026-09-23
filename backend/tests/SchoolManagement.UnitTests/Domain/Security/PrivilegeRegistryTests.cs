using System.Reflection;
using SchoolManagement.Domain.Security;

namespace SchoolManagement.UnitTests.Domain.Security;

/// <summary>
/// Pins the privilege register to spec 4.4.1 through 4.4.6, transcribed independently of
/// <see cref="PrivilegeRegistry"/> so a code change that drifts from the spec — or a future spec
/// revision nobody updated the code for — fails a test rather than shipping silently. TASK-0072
/// (stages 1 and 2b) added two privileges the spec's own table does not enumerate — a deliberate,
/// human-approved product decision, not spec drift — so this is no longer a PURE spec transcription;
/// see the two entries below marked as such.
/// </summary>
public sealed class PrivilegeRegistryTests
{
    /// <summary>
    /// Every privilege code, its <c>scopable</c> flag, its module group and its verbatim "Permits"
    /// sentence, transcribed directly from spec 4.4.1 through 4.4.6 IN SPEC TABLE ORDER, with two
    /// named exceptions inserted at the exact position <see cref="PrivilegeRegistry"/> itself puts
    /// them (see their own comments below). This is the register's SOURCE, independent of
    /// <see cref="PrivilegeRegistry.All"/> — do not "simplify" this by referencing the production
    /// list. Order matters here: this is what proves TASK-0028 dispatch 1's "row for row and in
    /// order" acceptance criterion, not just set equality.
    /// </summary>
    private static readonly (string Code, bool Scopable, PrivilegeModule Module, string Permits)[] ExpectedFromSpec =
    [
        // 4.4.1 Administration and access control — none scopable
        ("admin.view", false, PrivilegeModule.Administration, "List and open admin accounts."),
        ("admin.create", false, PrivilegeModule.Administration, "Create an admin account."),
        ("admin.update", false, PrivilegeModule.Administration, "Edit an admin account's name, email, phone."),
        ("admin.suspend", false, PrivilegeModule.Administration, "Move an account to suspended and back to active."),
        ("admin.deactivate", false, PrivilegeModule.Administration,
            "Move an account to deactivated. Irreversible except by a Super Admin reactivating it."),
        ("admin.password.reset", false, PrivilegeModule.Administration, "Force a password reset for another account."),
        ("admin.session.revoke", false, PrivilegeModule.Administration, "Kill another account's active sessions."),
        ("role.view", false, PrivilegeModule.Administration, "List roles and see their privilege sets."),
        ("role.create", false, PrivilegeModule.Administration, "Create a role."),
        ("role.update", false, PrivilegeModule.Administration, "Add or remove privileges from a role."),
        ("role.delete", false, PrivilegeModule.Administration, "Delete a role that has no active assignments."),
        ("role.assign", false, PrivilegeModule.Administration, "Assign a role to an account school-wide."),
        ("role.scope.assign", false, PrivilegeModule.Administration, "Assign a role to an account over a named list of arms."),
        ("audit.view", false, PrivilegeModule.Administration, "Read the audit log."),
        ("audit.export", false, PrivilegeModule.Administration, "Export a filtered audit log to CSV."),

        // 4.4.2 Settings — none scopable
        ("settings.view", false, PrivilegeModule.Settings, "Read every settings page. Read-only."),
        ("settings.identity.update", false, PrivilegeModule.Settings,
            "Edit school name, short name, address, phone, email, motto, logo, head teacher name and signature image."),
        ("settings.abbreviation.update", false, PrivilegeModule.Settings,
            "Edit the school abbreviation used in registration numbers. Split out from identity because it has consequences identity fields do not."),
        ("settings.regnumber.update", false, PrivilegeModule.Settings,
            "Edit serial width, separator and the reset rule for the registration number pattern."),
        ("settings.grading.update", false, PrivilegeModule.Settings,
            "Add, edit, remove and reorder grading bands. Reset to seeded defaults."),
        ("settings.assessment.update", false, PrivilegeModule.Settings,
            "Add, rename, remove and reorder continuous assessment components, and set the examination maximum."),
        ("settings.traits.update", false, PrivilegeModule.Settings,
            "Edit the affective and psychomotor trait lists and the trait rating scale."),
        ("settings.resultrules.update", false, PrivilegeModule.Settings,
            "Edit annual computation method and weights, position scope, level position visibility, tie-breaking rule, pass mark, promotion threshold."),
        ("settings.pin.update", false, PrivilegeModule.Settings,
            "Edit default pin length, default maximum uses and the character set."),
        ("settings.reset.defaults", false, PrivilegeModule.Settings,
            "Restore the grading scale, assessment structure or trait lists to seeded values."),

        // NOT from spec 4.4.2's literal table (01-actors-and-privileges.md still lists only
        // settings.traits.update for both scales and domains) — added by two deliberate,
        // human-approved product decisions (TASK-0072's approved contract delta,
        // decisions/2026-Q3.md) that split rating-scale and development-domain administration into
        // their own privileges, distinct from the trait screen. Listed here, not appended at the end
        // of this array, because this is where PrivilegeRegistry.cs itself inserts them — immediately
        // after settings.reset.defaults, still inside the Settings module and before Academic
        // Structure begins. This test's actual contract (below) is exact row-for-row, in-order
        // equality against the real registry, not against spec 4.4 alone, so the two amendments must
        // sit at the position the registry puts them, not wherever would keep a round count tidy.
        ("settings.ratingscales.update", false, PrivilegeModule.Settings,
            "Add, rename, remove and reorder rating scales and their points."),
        ("settings.developmentdomains.update", false, PrivilegeModule.Settings,
            "Add, rename, reorder, archive and remove development domains and indicators."),

        // 4.4.3 Academic structure
        ("session.view", false, PrivilegeModule.AcademicStructure, "List sessions and terms."),
        ("session.create", false, PrivilegeModule.AcademicStructure, "Create a session and its three terms."),
        ("session.update", false, PrivilegeModule.AcademicStructure,
            "Edit session and term dates, times school opened, resumption date."),
        ("term.open", false, PrivilegeModule.AcademicStructure, "Move a term from upcoming to active."),
        ("term.close", false, PrivilegeModule.AcademicStructure, "Move the active term to closed."),
        ("promotion.run", false, PrivilegeModule.AcademicStructure, "Run end-of-session promotion for a level or the whole school."),
        ("promotion.reverse", false, PrivilegeModule.AcademicStructure, "Reverse a promotion batch."),
        ("level.view", false, PrivilegeModule.AcademicStructure, "List and open class levels."),
        ("level.create", false, PrivilegeModule.AcademicStructure, "Create a class level and place it in the progression chain."),
        ("level.update", false, PrivilegeModule.AcademicStructure,
            "Rename a level, change its section, reorder it, change its next level."),
        ("level.deactivate", false, PrivilegeModule.AcademicStructure, "Deactivate or reactivate a level."),
        ("level.delete", false, PrivilegeModule.AcademicStructure, "Hard delete a level that nothing has ever referenced."),
        ("arm.view", true, PrivilegeModule.AcademicStructure, "List and open arms."),
        ("arm.create", false, PrivilegeModule.AcademicStructure, "Create an arm under a level for a session, singly or in bulk."),
        ("arm.update", false, PrivilegeModule.AcademicStructure, "Edit an arm's label, capacity and status."),
        ("arm.formteacher.assign", false, PrivilegeModule.AcademicStructure, "Set or change the form teacher on an arm."),
        ("arm.delete", false, PrivilegeModule.AcademicStructure, "Hard delete an arm that has never held an enrolment."),
        ("arm.capacity.override", true, PrivilegeModule.AcademicStructure, "Enrol a pupil into an arm that is already at capacity."),

        // 4.4.4 Pupils, guardians and subjects
        ("pupil.view", true, PrivilegeModule.PupilsAndSubjects, "List and open pupil records."),
        ("pupil.create", false, PrivilegeModule.PupilsAndSubjects, "Register a pupil and issue a registration number."),
        ("pupil.update", true, PrivilegeModule.PupilsAndSubjects, "Edit pupil biographical fields."),
        ("pupil.photo.update", true, PrivilegeModule.PupilsAndSubjects, "Upload or replace a pupil photograph."),
        ("pupil.status.update", false, PrivilegeModule.PupilsAndSubjects,
            "Change status between active, transferred, withdrawn, graduated."),
        ("pupil.transfer", false, PrivilegeModule.PupilsAndSubjects, "Move a pupil between arms, singly or in bulk."),
        ("pupil.import", false, PrivilegeModule.PupilsAndSubjects, "Run a bulk import from spreadsheet."),
        ("pupil.regnumber.correct", false, PrivilegeModule.PupilsAndSubjects, "Correct a wrongly issued registration number."),
        ("pupil.admission.approve", false, PrivilegeModule.PupilsAndSubjects,
            "Approve a pending admission, moving it to active and issuing the registration number, per 6.5.11."),
        // NOT from spec 4.4: human ruling 2026-09-23 (spec 6.5.16's head-teacher override).
        ("pupil.admission.override", false, PrivilegeModule.PupilsAndSubjects,
            "Approve an admission whose health questions the parent declined to answer, with a recorded reason, per 6.5.16."),
        ("pupil.safeguarding.view", true, PrivilegeModule.PupilsAndSubjects,
            "Read the section F health block and the barred-persons list, per 6.5.6 and 6.5.7. Every read is audited. Deliberately withheld from the Bursar and the Auditor."),
        ("pupil.safeguarding.update", true, PrivilegeModule.PupilsAndSubjects, "Edit the health block and the barred-persons list."),
        ("pupil.document.manage", true, PrivilegeModule.PupilsAndSubjects,
            "Tick off and attach files against the section H admission document checklist."),
        ("contact.create", true, PrivilegeModule.PupilsAndSubjects,
            "Add a parent, guardian or emergency contact. Replaces `guardian.create`, since one entity now serves all five contact roles per 6.5.5."),
        ("contact.update", true, PrivilegeModule.PupilsAndSubjects, "Edit a contact. Replaces `guardian.update`."),
        ("weekly.view", true, PrivilegeModule.PupilsAndSubjects, "List and read weekly report sheets, per 6.10."),
        ("weekly.enter", true, PrivilegeModule.PupilsAndSubjects, "Write and edit weekly report day notes."),
        ("weekly.publish", true, PrivilegeModule.PupilsAndSubjects, "Publish or unpublish a week to the parent portal."),
        ("pupil.delete", false, PrivilegeModule.PupilsAndSubjects, "Soft delete a pupil record that has no result history."),
        ("contact.view", true, PrivilegeModule.PupilsAndSubjects,
            "See contact names, phone numbers and relationships across all five roles in 6.5.5. Formerly `guardian.view`; the old name is retained as an alias in the seed data so existing role assignments do not break."),
        ("subject.view", true, PrivilegeModule.PupilsAndSubjects, "List subjects and see mappings."),
        ("subject.create", false, PrivilegeModule.PupilsAndSubjects, "Create a subject."),
        ("subject.update", false, PrivilegeModule.PupilsAndSubjects, "Edit subject name, code, description."),
        ("subject.deactivate", false, PrivilegeModule.PupilsAndSubjects, "Deactivate or reactivate a subject."),
        ("subject.delete", false, PrivilegeModule.PupilsAndSubjects, "Hard delete a subject never mapped and never scored."),
        ("subject.map", false, PrivilegeModule.PupilsAndSubjects, "Map a subject to a level for a session and term."),
        ("subject.map.arm", false, PrivilegeModule.PupilsAndSubjects, "Create a per-arm exception to a level mapping."),
        ("subject.unmap", false, PrivilegeModule.PupilsAndSubjects, "End a mapping."),

        // 4.4.5 Results
        ("result.view", true, PrivilegeModule.Results, "Open a score sheet or a computed result."),
        ("result.score.enter", true, PrivilegeModule.Results,
            "Enter and edit continuous assessment and examination marks while the result set is Draft or Returned for Correction."),
        ("result.score.void", true, PrivilegeModule.Results,
            "Void an already entered mark with a stated reason, so that a mapping can be ended or an error unwound."),
        ("result.trait.enter", true, PrivilegeModule.Results, "Enter affective and psychomotor ratings."),
        ("result.attendance.enter", true, PrivilegeModule.Results, "Enter times present and times absent per pupil."),
        ("result.remark.classteacher", true, PrivilegeModule.Results, "Write or edit the class teacher's remark."),
        ("result.remark.headteacher", false, PrivilegeModule.Results, "Write or edit the head teacher's remark."),
        ("result.compute", true, PrivilegeModule.Results, "Run computation over a result set."),
        ("result.submit", true, PrivilegeModule.Results, "Move a result set from Draft to Awaiting Approval."),
        ("result.approve", false, PrivilegeModule.Results, "Move a result set from Awaiting Approval to Approved."),
        ("result.return", false, PrivilegeModule.Results, "Return a result set to the class teacher with a reason."),
        ("result.publish", false, PrivilegeModule.Results, "Publish an approved result set and write the configuration snapshot."),
        ("result.unpublish", false, PrivilegeModule.Results, "Withdraw a published result set from the parent portal."),
        ("result.annual.compute", false, PrivilegeModule.Results,
            "Compute annual cumulative results for an arm once Third Term is published."),
        ("result.print", true, PrivilegeModule.Results, "Render and download the result PDF from inside the back office."),
        ("promotion.decide", false, PrivilegeModule.Results, "Override the system-proposed promotion status on a Third Term result."),

        // 4.4.6 Pins and reports
        ("pin.view", false, PrivilegeModule.PinsAndReports, "List pin batches and open a batch."),
        ("pin.generate", false, PrivilegeModule.PinsAndReports, "Generate a pin batch."),
        ("pin.print", false, PrivilegeModule.PinsAndReports, "Render the print run for a batch, which reveals pin plaintext once."),
        ("pin.revoke", false, PrivilegeModule.PinsAndReports, "Revoke a single pin or a whole batch."),
        ("pin.usage.view", false, PrivilegeModule.PinsAndReports,
            "Read the usage report for a pin, including timestamps and truncated source addresses."),
        ("report.view", true, PrivilegeModule.PinsAndReports, "Open any report in section 10."),
        ("report.export", true, PrivilegeModule.PinsAndReports, "Export a report to CSV or PDF."),
    ];

    [Fact]
    public void TheRegisterHasExactlyNinetyThreeSpecPrivilegesPlusThreeApprovedAdditions()
    {
        // Spec 4.4 itself still enumerates exactly 93: 15 + 10 + 18 + 27 + 16 + 7. TASK-0072 stage 1
        // and stage 2b each added one privilege beyond that table (settings.ratingscales.update,
        // settings.developmentdomains.update) by human-approved product decision, not spec revision —
        // see the two entries' own comments above. 95 is therefore the correct total, not a rounding
        // of 93; if a future spec revision folds these into 4.4.2 directly, this comment (and the
        // "NOT from spec" comments above) is what should be deleted, not the count. The human ruling of
        // 2026-09-23 added a third, pupil.admission.override (spec 6.5.16), so 96.
        ExpectedFromSpec.Length.ShouldBe(96, "the transcription above is wrong, not the production code");
        PrivilegeRegistry.All.Count.ShouldBe(96);
    }

    [Fact]
    public void EveryRegisteredPrivilegeMatchesTheSpecTranscriptionRowForRowInOrder()
    {
        // Deliberately NOT sorted before comparing: TASK-0028 dispatch 1's acceptance criterion is
        // that the register matches spec 4.4 row for row AND IN ORDER (groups 4.4.1 -> 4.4.6, rows
        // in spec table order), not merely that the same set of rows exists somewhere in the list.
        var actual = PrivilegeRegistry.All
            .Select(definition => (definition.Code, definition.Scopable, definition.Module, definition.Permits))
            .ToArray();

        actual.ShouldBe(ExpectedFromSpec);
    }

    [Fact]
    public void NoPrivilegeCodeIsDuplicated()
    {
        var duplicates = PrivilegeRegistry.All
            .GroupBy(definition => definition.Code, StringComparer.Ordinal)
            .Where(group => group.Count() > 1)
            .Select(group => group.Key)
            .ToArray();

        duplicates.ShouldBeEmpty();
    }

    [Fact]
    public void EveryConstantInPrivilegesIsRegistered_AndViceVersa()
    {
        // Reflects over every nested static class in Privileges and every public const string field
        // in each, so a constant added to Privileges.cs without a matching PrivilegeRegistry.All
        // entry (or the reverse) fails here rather than only showing up as a runtime
        // "not in the register" exception the first time a route uses it.
        var constantValues = typeof(Privileges)
            .GetNestedTypes(BindingFlags.Public | BindingFlags.Static)
            .SelectMany(nested => nested.GetFields(BindingFlags.Public | BindingFlags.Static | BindingFlags.FlattenHierarchy))
            .Where(field => field.IsLiteral && field.FieldType == typeof(string))
            .Select(field => (string)field.GetRawConstantValue()!)
            .OrderBy(value => value, StringComparer.Ordinal)
            .ToArray();

        var registeredCodes = PrivilegeRegistry.All
            .Select(definition => definition.Code)
            .OrderBy(code => code, StringComparer.Ordinal)
            .ToArray();

        constantValues.ShouldBe(registeredCodes);
    }

    [Theory]
    [InlineData("guardian.view", true)]
    [InlineData("guardian.create", true)]
    [InlineData("guardian.update", true)]
    [InlineData("guardian.delete", false)]
    [InlineData("not.a.real.privilege", false)]
    public void ExistsReflectsRegisteredAndAliasedCodes(string code, bool expected)
    {
        PrivilegeRegistry.Exists(code).ShouldBe(expected);
    }

    [Fact]
    public void IsScopable_ThrowsForAnUnknownCode()
    {
        Should.Throw<ArgumentException>(() => PrivilegeRegistry.IsScopable("not.a.real.privilege"));
    }
}
