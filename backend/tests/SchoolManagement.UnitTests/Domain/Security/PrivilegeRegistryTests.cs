using System.Reflection;
using SchoolManagement.Domain.Security;

namespace SchoolManagement.UnitTests.Domain.Security;

/// <summary>
/// Pins the privilege register to spec 4.4.1 through 4.4.6, transcribed independently of
/// <see cref="PrivilegeRegistry"/> so a code change that drifts from the spec — or a future spec
/// revision nobody updated the code for — fails a test rather than shipping silently.
/// </summary>
public sealed class PrivilegeRegistryTests
{
    /// <summary>
    /// Every privilege code and its <c>scopable</c> flag, transcribed directly from spec 4.4.1
    /// through 4.4.6. This is the register's SOURCE, independent of
    /// <see cref="PrivilegeRegistry.All"/> — do not "simplify" this by referencing the production
    /// list.
    /// </summary>
    private static readonly (string Code, bool Scopable)[] ExpectedFromSpec =
    [
        // 4.4.1 Administration and access control — none scopable
        ("admin.view", false),
        ("admin.create", false),
        ("admin.update", false),
        ("admin.suspend", false),
        ("admin.deactivate", false),
        ("admin.password.reset", false),
        ("admin.session.revoke", false),
        ("role.view", false),
        ("role.create", false),
        ("role.update", false),
        ("role.delete", false),
        ("role.assign", false),
        ("role.scope.assign", false),
        ("audit.view", false),
        ("audit.export", false),

        // 4.4.2 Settings — none scopable
        ("settings.view", false),
        ("settings.identity.update", false),
        ("settings.abbreviation.update", false),
        ("settings.regnumber.update", false),
        ("settings.grading.update", false),
        ("settings.assessment.update", false),
        ("settings.traits.update", false),
        ("settings.resultrules.update", false),
        ("settings.pin.update", false),
        ("settings.reset.defaults", false),

        // 4.4.3 Academic structure
        ("session.view", false),
        ("session.create", false),
        ("session.update", false),
        ("term.open", false),
        ("term.close", false),
        ("promotion.run", false),
        ("promotion.reverse", false),
        ("level.view", false),
        ("level.create", false),
        ("level.update", false),
        ("level.deactivate", false),
        ("level.delete", false),
        ("arm.view", true),
        ("arm.create", false),
        ("arm.update", false),
        ("arm.formteacher.assign", false),
        ("arm.delete", false),
        ("arm.capacity.override", true),

        // 4.4.4 Pupils, guardians and subjects
        ("pupil.view", true),
        ("pupil.create", false),
        ("pupil.update", true),
        ("pupil.photo.update", true),
        ("pupil.status.update", false),
        ("pupil.transfer", false),
        ("pupil.import", false),
        ("pupil.regnumber.correct", false),
        ("pupil.admission.approve", false),
        ("pupil.safeguarding.view", true),
        ("pupil.safeguarding.update", true),
        ("pupil.document.manage", true),
        ("contact.create", true),
        ("contact.update", true),
        ("weekly.view", true),
        ("weekly.enter", true),
        ("weekly.publish", true),
        ("pupil.delete", false),
        ("contact.view", true),
        ("subject.view", true),
        ("subject.create", false),
        ("subject.update", false),
        ("subject.deactivate", false),
        ("subject.delete", false),
        ("subject.map", false),
        ("subject.map.arm", false),
        ("subject.unmap", false),

        // 4.4.5 Results
        ("result.view", true),
        ("result.score.enter", true),
        ("result.score.void", true),
        ("result.trait.enter", true),
        ("result.attendance.enter", true),
        ("result.remark.classteacher", true),
        ("result.remark.headteacher", false),
        ("result.compute", true),
        ("result.submit", true),
        ("result.approve", false),
        ("result.return", false),
        ("result.publish", false),
        ("result.unpublish", false),
        ("result.annual.compute", false),
        ("result.print", true),
        ("promotion.decide", false),

        // 4.4.6 Pins and reports
        ("pin.view", false),
        ("pin.generate", false),
        ("pin.print", false),
        ("pin.revoke", false),
        ("pin.usage.view", false),
        ("report.view", true),
        ("report.export", true),
    ];

    [Fact]
    public void TheRegisterHasExactlyNinetyThreePrivileges()
    {
        // Spec 4.4: 15 + 10 + 18 + 27 + 16 + 7 = 93.
        ExpectedFromSpec.Length.ShouldBe(93, "the transcription above is wrong, not the production code");
        PrivilegeRegistry.All.Count.ShouldBe(93);
    }

    [Fact]
    public void EveryRegisteredPrivilegeMatchesTheSpecTranscription()
    {
        var actual = PrivilegeRegistry.All
            .Select(definition => (definition.Code, definition.Scopable))
            .OrderBy(entry => entry.Code, StringComparer.Ordinal)
            .ToArray();

        var expected = ExpectedFromSpec
            .OrderBy(entry => entry.Code, StringComparer.Ordinal)
            .ToArray();

        actual.ShouldBe(expected);
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
