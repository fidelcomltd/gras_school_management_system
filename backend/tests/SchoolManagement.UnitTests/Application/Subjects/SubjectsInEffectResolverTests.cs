using NSubstitute;
using SchoolManagement.Application.Abstractions.Classes;
using SchoolManagement.Application.Abstractions.Subjects;
using SchoolManagement.Application.Subjects;
using SchoolManagement.Domain.Classes;
using SchoolManagement.Domain.Subjects;

namespace SchoolManagement.UnitTests.Application.Subjects;

/// <summary>
/// Tests <see cref="SubjectsInEffectResolver"/> — TASK-0070's stated real deliverable: "level
/// mappings for the term, plus the arm's include exceptions, minus its exclude exceptions" (spec
/// 8.1), expressed in exactly one place.
/// </summary>
public sealed class SubjectsInEffectResolverTests
{
    private readonly IArmRepository _arms = Substitute.For<IArmRepository>();
    private readonly ISubjectRepository _subjects = Substitute.For<ISubjectRepository>();
    private readonly ISubjectMappingRepository _mappings = Substitute.For<ISubjectMappingRepository>();
    private readonly ISubjectMappingExceptionRepository _exceptions = Substitute.For<ISubjectMappingExceptionRepository>();

    private readonly Guid _termId = Guid.CreateVersion7();
    private readonly Guid _classLevelId = Guid.CreateVersion7();

    public SubjectsInEffectResolverTests()
    {
        // Defaults: no mappings, no exceptions, no subjects — a test overrides only what it needs.
        _mappings.ListActiveByLevelAndTermReadOnlyAsync(Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns((IReadOnlyList<SubjectMapping>)[]);
        _exceptions.ListByArmAndTermReadOnlyAsync(Arg.Any<Guid>(), Arg.Any<Guid>(), Arg.Any<CancellationToken>())
            .Returns((IReadOnlyList<SubjectMappingException>)[]);
        _subjects.ListAllReadOnlyAsync(Arg.Any<CancellationToken>()).Returns((IReadOnlyList<Subject>)[]);
    }

    private SubjectsInEffectResolver CreateResolver() => new(_arms, _subjects, _mappings, _exceptions);

    private Arm StubArm(Guid armId)
    {
        var arm = Arm.Create(armId, _classLevelId, Guid.CreateVersion7(), "A", null, null).Value;
        _arms.FindReadOnlyByIdAsync(armId, Arg.Any<CancellationToken>()).Returns(arm);
        return arm;
    }

    private static Subject NewSubject(string name) => Subject.Create(Guid.CreateVersion7(), name, null, null).Value;

    private SubjectMapping NewMapping(Guid subjectId, int displayOrder) =>
        SubjectMapping.Create(Guid.CreateVersion7(), subjectId, _classLevelId, Guid.CreateVersion7(), _termId, displayOrder).Value;

    private SubjectMappingException NewException(Guid armId, Guid subjectId, SubjectExceptionMode mode) =>
        SubjectMappingException.Create(Guid.CreateVersion7(), armId, subjectId, _termId, mode, "test reason").Value;

    [Fact]
    public async Task ResolveAsync_WhenTheArmDoesNotExist_ReturnsArmNotFound()
    {
        var resolver = CreateResolver();

        var result = await resolver.ResolveAsync(Guid.CreateVersion7(), _termId, TestContext.Current.CancellationToken);

        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe("arm.not_found");
    }

    // The empty-mapping case named by the card: no level mappings and no exceptions resolves to
    // an empty, not-failing, set.
    [Fact]
    public async Task ResolveAsync_WithNoMappingsAndNoExceptions_ReturnsAnEmptySet()
    {
        var armId = Guid.CreateVersion7();
        StubArm(armId);

        var result = await CreateResolver().ResolveAsync(armId, _termId, TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue();
        result.Value.ShouldBeEmpty();
    }

    [Fact]
    public async Task ResolveAsync_MappingsPlusIncludesMinusExcludes_ResolvesTheCorrectSet()
    {
        var armId = Guid.CreateVersion7();
        StubArm(armId);

        var maths = NewSubject("Mathematics");
        var english = NewSubject("English Language");
        var french = NewSubject("French"); // include exception, not level-mapped
        var civic = NewSubject("Civic Education"); // level-mapped but excluded

        _subjects.ListAllReadOnlyAsync(Arg.Any<CancellationToken>())
            .Returns((IReadOnlyList<Subject>)[maths, english, french, civic]);

        _mappings.ListActiveByLevelAndTermReadOnlyAsync(_classLevelId, _termId, Arg.Any<CancellationToken>())
            .Returns((IReadOnlyList<SubjectMapping>)
            [
                NewMapping(maths.Id, 1),
                NewMapping(english.Id, 2),
                NewMapping(civic.Id, 3),
            ]);

        _exceptions.ListByArmAndTermReadOnlyAsync(armId, _termId, Arg.Any<CancellationToken>())
            .Returns((IReadOnlyList<SubjectMappingException>)
            [
                NewException(armId, french.Id, SubjectExceptionMode.Include),
                NewException(armId, civic.Id, SubjectExceptionMode.Exclude),
            ]);

        var result = await CreateResolver().ResolveAsync(armId, _termId, TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue();
        var resolvedIds = result.Value.Select(row => row.SubjectId).ToArray();
        resolvedIds.ShouldContain(maths.Id);
        resolvedIds.ShouldContain(english.Id);
        resolvedIds.ShouldContain(french.Id);
        resolvedIds.ShouldNotContain(civic.Id);
        result.Value.Count.ShouldBe(3);

        result.Value.Single(row => row.SubjectId == maths.Id).Source.ShouldBe(SubjectSourceKind.LevelInherited);
        result.Value.Single(row => row.SubjectId == french.Id).Source.ShouldBe(SubjectSourceKind.ArmException);
    }

    // The card's own example: "two arms of one level may legitimately end up with different subject
    // sets" (spec 8.4.7). Same level mappings, different arm exceptions, different resolved sets —
    // proves the resolution is keyed on the ARM, not only the level.
    [Fact]
    public async Task ResolveAsync_TwoArmsOfTheSameLevel_CanResolveToDifferentSets()
    {
        var firstArmId = Guid.CreateVersion7();
        var secondArmId = Guid.CreateVersion7();
        var firstArm = Arm.Create(firstArmId, _classLevelId, Guid.CreateVersion7(), "A", null, null).Value;
        var secondArm = Arm.Create(secondArmId, _classLevelId, Guid.CreateVersion7(), "B", null, null).Value;
        _arms.FindReadOnlyByIdAsync(firstArmId, Arg.Any<CancellationToken>()).Returns(firstArm);
        _arms.FindReadOnlyByIdAsync(secondArmId, Arg.Any<CancellationToken>()).Returns(secondArm);

        var french = NewSubject("French");
        var maths = NewSubject("Mathematics");
        _subjects.ListAllReadOnlyAsync(Arg.Any<CancellationToken>()).Returns((IReadOnlyList<Subject>)[french, maths]);

        _mappings.ListActiveByLevelAndTermReadOnlyAsync(_classLevelId, _termId, Arg.Any<CancellationToken>())
            .Returns((IReadOnlyList<SubjectMapping>)[NewMapping(maths.Id, 1)]);

        // Only the first arm carries an include exception for French.
        _exceptions.ListByArmAndTermReadOnlyAsync(firstArmId, _termId, Arg.Any<CancellationToken>())
            .Returns((IReadOnlyList<SubjectMappingException>)[NewException(firstArmId, french.Id, SubjectExceptionMode.Include)]);
        _exceptions.ListByArmAndTermReadOnlyAsync(secondArmId, _termId, Arg.Any<CancellationToken>())
            .Returns((IReadOnlyList<SubjectMappingException>)[]);

        var firstResult = await CreateResolver().ResolveAsync(firstArmId, _termId, TestContext.Current.CancellationToken);
        var secondResult = await CreateResolver().ResolveAsync(secondArmId, _termId, TestContext.Current.CancellationToken);

        firstResult.Value.Select(row => row.SubjectId).ShouldBe([maths.Id, french.Id]);
        secondResult.Value.Select(row => row.SubjectId).ShouldBe([maths.Id]);
    }

    // Pins the resolver's own documented judgement call: include rows are appended AFTER every
    // level row, ordered by name, with display_order = max(level rows) + 1, + 2, ... — not the
    // exception's creation order and not interleaved with the level rows.
    [Fact]
    public async Task ResolveAsync_IncludeRows_AreAppendedAfterLevelRowsOrderedByNameWithDisplayOrderContinuingFromTheMax()
    {
        var armId = Guid.CreateVersion7();
        StubArm(armId);

        var zebra = NewSubject("Zebra Studies"); // include, sorts last alphabetically
        var apple = NewSubject("Apple Studies"); // include, sorts first alphabetically
        var maths = NewSubject("Mathematics"); // level-mapped, display_order 5

        _subjects.ListAllReadOnlyAsync(Arg.Any<CancellationToken>()).Returns((IReadOnlyList<Subject>)[zebra, apple, maths]);

        _mappings.ListActiveByLevelAndTermReadOnlyAsync(_classLevelId, _termId, Arg.Any<CancellationToken>())
            .Returns((IReadOnlyList<SubjectMapping>)[NewMapping(maths.Id, 5)]);

        // Exceptions listed zebra-then-apple (creation order) to prove the resolver re-sorts by name
        // rather than trusting the repository's return order.
        _exceptions.ListByArmAndTermReadOnlyAsync(armId, _termId, Arg.Any<CancellationToken>())
            .Returns((IReadOnlyList<SubjectMappingException>)
            [
                NewException(armId, zebra.Id, SubjectExceptionMode.Include),
                NewException(armId, apple.Id, SubjectExceptionMode.Include),
            ]);

        var result = await CreateResolver().ResolveAsync(armId, _termId, TestContext.Current.CancellationToken);

        result.Value.Select(row => row.SubjectId).ShouldBe([maths.Id, apple.Id, zebra.Id]);
        result.Value.Single(row => row.SubjectId == maths.Id).DisplayOrder.ShouldBe(5);
        result.Value.Single(row => row.SubjectId == apple.Id).DisplayOrder.ShouldBe(6);
        result.Value.Single(row => row.SubjectId == zebra.Id).DisplayOrder.ShouldBe(7);
    }

    // Spec 6.6.6: "Deactivating a subject does not end its mappings. It stops new mappings only." The
    // resolver deliberately does NOT filter on subject status — an already-mapped, now-inactive
    // subject keeps appearing on the sheet until the mapping ends naturally at the term boundary.
    [Fact]
    public async Task ResolveAsync_ADeactivatedSubjectWithAnExistingMapping_StillAppearsInTheResolvedSet()
    {
        var armId = Guid.CreateVersion7();
        StubArm(armId);

        var deactivated = NewSubject("Handwriting");
        deactivated.Deactivate();
        _subjects.ListAllReadOnlyAsync(Arg.Any<CancellationToken>()).Returns((IReadOnlyList<Subject>)[deactivated]);

        _mappings.ListActiveByLevelAndTermReadOnlyAsync(_classLevelId, _termId, Arg.Any<CancellationToken>())
            .Returns((IReadOnlyList<SubjectMapping>)[NewMapping(deactivated.Id, 1)]);

        var result = await CreateResolver().ResolveAsync(armId, _termId, TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue();
        result.Value.Select(row => row.SubjectId).ShouldContain(deactivated.Id);
    }

    // Pins the resolver's other documented judgement call: a mapping or exception whose subject row
    // is missing from the loaded subject set is silently SKIPPED rather than thrown. Chosen as the
    // correct behaviour here (not merely convenient) because both subject_mapping.subject_id and
    // subject_mapping_exception.subject_id carry a database RESTRICT foreign key to subjects (see
    // SubjectMappingConfiguration/SubjectMappingExceptionConfiguration) — an orphaned reference is
    // therefore unreachable through any real write path, so this branch can only ever fire against a
    // corrupted database, where silently dropping the row (leaving a correct, if incomplete, result)
    // is safer for the arm's result sheet than throwing and failing the whole resolution outright.
    [Fact]
    public async Task ResolveAsync_WhenAMappingsSubjectIsMissingFromTheLoadedSet_SkipsItRatherThanThrowing()
    {
        var armId = Guid.CreateVersion7();
        StubArm(armId);

        var maths = NewSubject("Mathematics");
        var orphanSubjectId = Guid.CreateVersion7(); // referenced by a mapping but never loaded

        _subjects.ListAllReadOnlyAsync(Arg.Any<CancellationToken>()).Returns((IReadOnlyList<Subject>)[maths]);

        _mappings.ListActiveByLevelAndTermReadOnlyAsync(_classLevelId, _termId, Arg.Any<CancellationToken>())
            .Returns((IReadOnlyList<SubjectMapping>)
            [
                NewMapping(maths.Id, 1),
                NewMapping(orphanSubjectId, 2),
            ]);

        var result = await CreateResolver().ResolveAsync(armId, _termId, TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue();
        result.Value.Select(row => row.SubjectId).ShouldBe([maths.Id]);
    }
}
