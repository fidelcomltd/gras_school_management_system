using NSubstitute;
using SchoolManagement.Application.Abstractions.Authorization;
using SchoolManagement.Application.Abstractions.Pupils;
using SchoolManagement.Application.Authorization;
using SchoolManagement.Domain.Pupils;

namespace SchoolManagement.UnitTests.Application.Authorization;

/// <summary>
/// The four resolution rules of spec 4.2.1, exercised directly against <see cref="ScopeResolver"/>.
/// </summary>
public sealed class ScopeResolverTests
{
    private readonly IPupilArmOfRecordLookup _pupilArmLookup = Substitute.For<IPupilArmOfRecordLookup>();
    private readonly IResultSetArmLookup _resultSetArmLookup = Substitute.For<IResultSetArmLookup>();

    private readonly IPupilRepository _pupils = Substitute.For<IPupilRepository>();

    private ScopeResolver CreateResolver() => new(_pupilArmLookup, _resultSetArmLookup, _pupils);

    [Fact]
    public async Task ArmKind_ResolvesDirectlyToTheSuppliedId()
    {
        // Rule 1: "A request naming an arm resolves to that arm."
        var armId = Guid.CreateVersion7();

        var resolution = await CreateResolver().ResolveAsync(
            ScopeParameterKind.Arm, armId, TestContext.Current.CancellationToken);

        resolution.ShouldBeOfType<ScopeResolution.ResolvedArm>().ArmId.ShouldBe(armId);
    }

    [Fact]
    public async Task ArmKind_WithNoParameterValue_IsUnresolvable()
    {
        var resolution = await CreateResolver().ResolveAsync(
            ScopeParameterKind.Arm, null, TestContext.Current.CancellationToken);

        resolution.ShouldBeOfType<ScopeResolution.Unresolvable>();
    }

    [Fact]
    public async Task PupilKind_ResolvesToTheArmOfRecordFromTheLookup()
    {
        // Rule 2: "A request naming a pupil resolves to the pupil's arm of record for the active
        // term" — the resolver defers this to the lookup (which spec 9.2 requires to be the pupil's
        // OPEN ENROLMENT, never a client-supplied arm id); this test only asserts the resolver wires
        // the lookup's answer through correctly.
        var pupilId = Guid.CreateVersion7();
        var armId = Guid.CreateVersion7();
        _pupilArmLookup.GetArmIdAsync(pupilId, Arg.Any<CancellationToken>()).Returns(armId);

        var resolution = await CreateResolver().ResolveAsync(
            ScopeParameterKind.Pupil, pupilId, TestContext.Current.CancellationToken);

        resolution.ShouldBeOfType<ScopeResolution.ResolvedArm>().ArmId.ShouldBe(armId);
    }

    [Fact]
    public async Task PupilKind_AnExistingPupilWithNoOpenEnrolment_RequiresSchoolWide()
    {
        // A pending admission or a leaver: real, but in no arm (human ruling 2026-09-23).
        var pupil = Pupil.Create(
            Guid.CreateVersion7(), "Okafor", "Chidi", null, PupilSex.Male, new DateOnly(2019, 3, 1), new DateOnly(2026, 9, 1),
            "Nigerian", "Anambra", "Awka South", "12 Zik Avenue", previousSchool: null, previousClass: null, otherInformation: null).Value;
        _pupilArmLookup.GetArmIdAsync(pupil.Id, Arg.Any<CancellationToken>()).Returns((Guid?)null);
        _pupils.FindReadOnlyByIdAsync(pupil.Id, Arg.Any<CancellationToken>()).Returns(pupil);

        var resolution = await CreateResolver().ResolveAsync(
            ScopeParameterKind.Pupil, pupil.Id, TestContext.Current.CancellationToken);

        resolution.ShouldBeOfType<ScopeResolution.RequiresSchoolWide>();
    }

    [Fact]
    public async Task PupilKind_AnIdNamingNoPupil_IsUnresolvable()
    {
        var pupilId = Guid.CreateVersion7();
        _pupilArmLookup.GetArmIdAsync(pupilId, Arg.Any<CancellationToken>()).Returns((Guid?)null);

        var resolution = await CreateResolver().ResolveAsync(
            ScopeParameterKind.Pupil, pupilId, TestContext.Current.CancellationToken);

        resolution.ShouldBeOfType<ScopeResolution.Unresolvable>();
    }

    [Fact]
    public async Task ResultSetKind_ResolvesToTheArmFromTheLookup()
    {
        // Rule 3: "A request naming a result set resolves to that result set's arm."
        var resultSetId = Guid.CreateVersion7();
        var armId = Guid.CreateVersion7();
        _resultSetArmLookup.GetArmIdAsync(resultSetId, Arg.Any<CancellationToken>()).Returns(armId);

        var resolution = await CreateResolver().ResolveAsync(
            ScopeParameterKind.ResultSet, resultSetId, TestContext.Current.CancellationToken);

        resolution.ShouldBeOfType<ScopeResolution.ResolvedArm>().ArmId.ShouldBe(armId);
    }

    [Fact]
    public async Task LevelKind_AlwaysRequiresSchoolWide_RegardlessOfTheSuppliedId()
    {
        // Rule 4: "A request naming a level, with no arm, requires the privilege school-wide. An
        // arm-scoped holder cannot perform level-wide operations even over a level containing only
        // their own arm." The level id therefore plays no part in the outcome.
        var resolution = await CreateResolver().ResolveAsync(
            ScopeParameterKind.Level, Guid.CreateVersion7(), TestContext.Current.CancellationToken);

        resolution.ShouldBeOfType<ScopeResolution.RequiresSchoolWide>();
    }

    [Fact]
    public async Task NoneKind_IsNotApplicable()
    {
        var resolution = await CreateResolver().ResolveAsync(
            ScopeParameterKind.None, null, TestContext.Current.CancellationToken);

        resolution.ShouldBeOfType<ScopeResolution.NotApplicable>();
    }
}
