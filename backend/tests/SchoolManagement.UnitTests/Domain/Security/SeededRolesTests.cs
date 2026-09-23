using SchoolManagement.Domain.Security;

namespace SchoolManagement.UnitTests.Domain.Security;

/// <summary>
/// Pins spec 4.5's six seeded roles: fixed ids and, for each of the five non-system roles, an
/// EXACT privilege set — every <c>*</c> wildcard and every "except" clause in 4.5's prose expanded
/// by hand against the register and asserted member-by-member.
/// </summary>
/// <remarks>
/// Deliberately does NOT call any shared expansion helper <see cref="SeededRoles"/> itself uses —
/// each expected array below is a flat, independently transcribed list of literal
/// <see cref="Privileges"/> constants, compared with <c>ShouldBe</c> (exact sequence equality, not a
/// subset or count check) after both sides are sorted the same ordinal way <see cref="Role.Create"/>
/// itself sorts. A test that re-ran the same "take session.* minus delete" logic production code
/// runs would only prove the logic agrees with itself.
/// </remarks>
public sealed class SeededRolesTests
{
    [Fact]
    public void SixRoleIds_AreFixedDistinctAndNonEmpty()
    {
        Guid[] ids =
        [
            SeededRoles.SuperAdminId, SeededRoles.SchoolAdministratorId, SeededRoles.HeadTeacherId,
            SeededRoles.ClassTeacherId, SeededRoles.BursarId, SeededRoles.AuditorId,
        ];

        ids.ShouldAllBe(id => id != Guid.Empty);
        ids.Distinct().Count().ShouldBe(6);

        // Pinned literally: a change here changes what an existing database and a fresh install
        // disagree on, which must never happen silently.
        SeededRoles.SuperAdminId.ShouldBe(Guid.Parse("00000000-0000-0000-0000-000000000101"));
        SeededRoles.SchoolAdministratorId.ShouldBe(Guid.Parse("00000000-0000-0000-0000-000000000102"));
        SeededRoles.HeadTeacherId.ShouldBe(Guid.Parse("00000000-0000-0000-0000-000000000103"));
        SeededRoles.ClassTeacherId.ShouldBe(Guid.Parse("00000000-0000-0000-0000-000000000104"));
        SeededRoles.BursarId.ShouldBe(Guid.Parse("00000000-0000-0000-0000-000000000105"));
        SeededRoles.AuditorId.ShouldBe(Guid.Parse("00000000-0000-0000-0000-000000000106"));
    }

    [Fact]
    public void SuperAdmin_HoldsEveryCodeInTheRegisterAndIsSystem()
    {
        // Spec 4.5: "Every privilege in the register. Not editable." SuperAdminPrivileges is built
        // from PrivilegeRegistry.All itself (a projection, not its own transcription — see the type's
        // remarks), so this count tracks the register's actual size, currently spec 4.4's 93 plus the
        // two TASK-0072 additions (settings.ratingscales.update, settings.developmentdomains.update) and pupil.admission.override
        // PrivilegeRegistryTests documents are not in spec 4.4's own table.
        var everyRegisteredCode = PrivilegeRegistry.All.Select(definition => definition.Code).ToArray();

        SeededRoles.SuperAdminPrivileges.Count.ShouldBe(96);
        Sorted(SeededRoles.SuperAdminPrivileges).ShouldBe(Sorted(everyRegisteredCode));
    }

    [Fact]
    public void SchoolAdministrator_MatchesTheIndependentlyTranscribedExpansion()
    {
        // Spec 4.5: "All of session.*, level.* except delete, arm.* except delete, pupil.* except
        // delete and regnumber.correct, guardian.*, subject.* except delete, promotion.run,
        // role.scope.assign, admin.view, settings.view, pin.view, pin.usage.view, result.view,
        // result.print, report.*." guardian.* resolves to its three contact.* replacements.
        string[] expected =
        [
            Privileges.Session.View, Privileges.Session.Create, Privileges.Session.Update,
            Privileges.Level.View, Privileges.Level.Create, Privileges.Level.Update, Privileges.Level.Deactivate,
            Privileges.Arm.View, Privileges.Arm.Create, Privileges.Arm.Update, Privileges.Arm.FormTeacherAssign,
            Privileges.Arm.CapacityOverride,
            Privileges.Pupil.View, Privileges.Pupil.Create, Privileges.Pupil.Update, Privileges.Pupil.PhotoUpdate,
            Privileges.Pupil.StatusUpdate, Privileges.Pupil.Transfer, Privileges.Pupil.Import,
            Privileges.Pupil.AdmissionApprove, Privileges.Pupil.SafeguardingView,
            Privileges.Pupil.SafeguardingUpdate, Privileges.Pupil.DocumentManage,
            Privileges.Contact.Create, Privileges.Contact.Update, Privileges.Contact.View,
            Privileges.Subject.View, Privileges.Subject.Create, Privileges.Subject.Update,
            Privileges.Subject.Deactivate, Privileges.Subject.Map, Privileges.Subject.MapArm,
            Privileges.Subject.Unmap,
            Privileges.Promotion.Run, Privileges.Role.ScopeAssign, Privileges.Admin.View,
            Privileges.Settings.View, Privileges.Pin.View, Privileges.Pin.UsageView, Privileges.Results.View,
            Privileges.Results.Print,
            Privileges.Report.View, Privileges.Report.Export,
        ];

        expected.Length.ShouldBe(43);
        Sorted(SeededRoles.SchoolAdministratorPrivileges).ShouldBe(Sorted(expected));

        // Explicit exclusions named by 4.5's "except" clauses — never present.
        SeededRoles.SchoolAdministratorPrivileges.ShouldNotContain(Privileges.Level.Delete);
        SeededRoles.SchoolAdministratorPrivileges.ShouldNotContain(Privileges.Arm.Delete);
        SeededRoles.SchoolAdministratorPrivileges.ShouldNotContain(Privileges.Pupil.Delete);
        SeededRoles.SchoolAdministratorPrivileges.ShouldNotContain(Privileges.Pupil.RegNumberCorrect);
        SeededRoles.SchoolAdministratorPrivileges.ShouldNotContain(Privileges.Subject.Delete);

        // "guardian.*" (spec prose) never survives as a stored code.
        SeededRoles.SchoolAdministratorPrivileges.ShouldAllBe(code => !code.StartsWith("guardian.", StringComparison.Ordinal));
    }

    [Fact]
    public void HeadTeacher_MatchesTheIndependentlyTranscribedExpansion()
    {
        string[] expected =
        [
            Privileges.Results.View, Privileges.Results.Compute, Privileges.Results.RemarkHeadTeacher,
            Privileges.Results.Approve, Privileges.Results.Return, Privileges.Results.Publish,
            Privileges.Results.AnnualCompute, Privileges.Results.Print, Privileges.Promotion.Decide,
            Privileges.Pupil.View, Privileges.Contact.View, Privileges.Pupil.SafeguardingView,
            Privileges.Pupil.SafeguardingUpdate, Privileges.Pupil.AdmissionApprove, Privileges.Pupil.AdmissionOverride,
            Privileges.Weekly.View, Privileges.Weekly.Enter, Privileges.Weekly.Publish,
            Privileges.Arm.View, Privileges.Subject.View, Privileges.Session.View, Privileges.Level.View,
            Privileges.Settings.View, Privileges.Pin.View,
            Privileges.Report.View, Privileges.Report.Export,
        ];

        expected.Length.ShouldBe(26);
        Sorted(SeededRoles.HeadTeacherPrivileges).ShouldBe(Sorted(expected));

        // Head Teacher never enters marks (spec 4.1) and never generates pins.
        SeededRoles.HeadTeacherPrivileges.ShouldNotContain(Privileges.Results.ScoreEnter);
        SeededRoles.HeadTeacherPrivileges.ShouldNotContain(Privileges.Pin.Generate);
    }

    [Fact]
    public void ClassTeacher_MatchesTheIndependentlyTranscribedExpansion()
    {
        string[] expected =
        [
            Privileges.Results.View, Privileges.Results.ScoreEnter, Privileges.Results.TraitEnter,
            Privileges.Results.AttendanceEnter, Privileges.Results.RemarkClassTeacher, Privileges.Results.Compute,
            Privileges.Results.Submit, Privileges.Results.Print,
            Privileges.Pupil.View, Privileges.Pupil.PhotoUpdate, Privileges.Contact.View,
            Privileges.Pupil.SafeguardingView,
            Privileges.Weekly.View, Privileges.Weekly.Enter, Privileges.Weekly.Publish,
            Privileges.Arm.View, Privileges.Subject.View, Privileges.Session.View, Privileges.Level.View,
        ];

        expected.Length.ShouldBe(19);
        Sorted(SeededRoles.ClassTeacherPrivileges).ShouldBe(Sorted(expected));

        // Class Teacher never approves, publishes or writes the head teacher's remark.
        SeededRoles.ClassTeacherPrivileges.ShouldNotContain(Privileges.Results.Approve);
        SeededRoles.ClassTeacherPrivileges.ShouldNotContain(Privileges.Results.Publish);
        SeededRoles.ClassTeacherPrivileges.ShouldNotContain(Privileges.Results.RemarkHeadTeacher);
    }

    [Fact]
    public void Bursar_MatchesTheIndependentlyTranscribedExpansion()
    {
        string[] expected =
        [
            Privileges.Pin.View, Privileges.Pin.Generate, Privileges.Pin.Print, Privileges.Pin.Revoke,
            Privileges.Pin.UsageView,
            Privileges.Pupil.View, Privileges.Contact.View, Privileges.Weekly.View,
            Privileges.Arm.View, Privileges.Session.View, Privileges.Level.View,
        ];

        expected.Length.ShouldBe(11);
        Sorted(SeededRoles.BursarPrivileges).ShouldBe(Sorted(expected));

        // Spec 4.5: "No result privilege of any kind, and no safeguarding privilege."
        SeededRoles.BursarPrivileges.ShouldAllBe(code => !code.StartsWith("result.", StringComparison.Ordinal));
        SeededRoles.BursarPrivileges.ShouldAllBe(code => !code.StartsWith("pupil.safeguarding.", StringComparison.Ordinal));
    }

    [Fact]
    public void Auditor_MatchesTheIndependentlyTranscribedExpansionAndHoldsNoWritePrivilege()
    {
        string[] expected =
        [
            Privileges.Audit.View, Privileges.Audit.Export, Privileges.Settings.View, Privileges.Admin.View,
            Privileges.Role.View, Privileges.Session.View, Privileges.Level.View, Privileges.Arm.View,
            Privileges.Subject.View, Privileges.Pupil.View, Privileges.Results.View, Privileges.Pin.View,
            Privileges.Pin.UsageView,
            Privileges.Report.View, Privileges.Report.Export,
        ];

        expected.Length.ShouldBe(15);
        Sorted(SeededRoles.AuditorPrivileges).ShouldBe(Sorted(expected));

        // Spec 4.5: "No write privilege." Every seeded code must end in a read-shaped verb.
        SeededRoles.AuditorPrivileges.ShouldAllBe(code =>
            code.EndsWith(".view", StringComparison.Ordinal) || code.EndsWith(".export", StringComparison.Ordinal));
    }

    [Theory]
    [MemberData(nameof(AllSixRoleSets))]
    public void EveryCodeInEverySeededRole_ExistsInTheRegister(string roleName, IReadOnlyList<string> privileges)
    {
        privileges.ShouldNotBeEmpty($"{roleName} must hold at least one privilege.");

        foreach (var code in privileges)
        {
            PrivilegeRegistry.Exists(code).ShouldBeTrue(
                $"{roleName} names '{code}', which is not in the privilege register — a likely transcription typo.");
        }
    }

    public static TheoryData<string, IReadOnlyList<string>> AllSixRoleSets() => new()
    {
        { "Super Admin", SeededRoles.SuperAdminPrivileges },
        { "School Administrator", SeededRoles.SchoolAdministratorPrivileges },
        { "Head Teacher", SeededRoles.HeadTeacherPrivileges },
        { "Class Teacher", SeededRoles.ClassTeacherPrivileges },
        { "Bursar", SeededRoles.BursarPrivileges },
        { "Auditor", SeededRoles.AuditorPrivileges },
    };

    private static string[] Sorted(IReadOnlyCollection<string> codes) =>
        codes.OrderBy(code => code, StringComparer.Ordinal).ToArray();
}
