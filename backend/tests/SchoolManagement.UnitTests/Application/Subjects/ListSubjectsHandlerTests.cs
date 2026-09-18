using NSubstitute;
using SchoolManagement.Application.Abstractions.Classes;
using SchoolManagement.Application.Abstractions.Enrolments;
using SchoolManagement.Application.Abstractions.Sessions;
using SchoolManagement.Application.Abstractions.Subjects;
using SchoolManagement.Application.Subjects;
using SchoolManagement.Domain.Classes;
using SchoolManagement.Domain.Sessions;
using SchoolManagement.Domain.Subjects;

namespace SchoolManagement.UnitTests.Application.Subjects;

/// <summary>
/// Tests <see cref="ListSubjectsHandler"/>'s TASK-0070 delta amendment 3: "this endpoint never
/// guesses a term" — the three term-scoped counts are null whenever <c>termId</c> is absent, and
/// populated (via the SAME <see cref="SubjectsInEffectResolver"/> the resolver tests already cover)
/// when it is supplied.
/// </summary>
public sealed class ListSubjectsHandlerTests
{
    private readonly ISubjectRepository _subjects = Substitute.For<ISubjectRepository>();
    private readonly ISubjectMappingRepository _mappings = Substitute.For<ISubjectMappingRepository>();
    private readonly ISubjectMappingExceptionRepository _exceptions = Substitute.For<ISubjectMappingExceptionRepository>();
    private readonly IArmRepository _arms = Substitute.For<IArmRepository>();
    private readonly ITermRepository _terms = Substitute.For<ITermRepository>();
    private readonly IEnrolmentRepository _enrolments = Substitute.For<IEnrolmentRepository>();

    private readonly Guid _termId = Guid.CreateVersion7();
    private readonly Guid _sessionId = Guid.CreateVersion7();
    private readonly Subject _subject = Subject.Create(Guid.CreateVersion7(), "Mathematics", null, null).Value;

    public ListSubjectsHandlerTests()
    {
        _subjects.ListAllReadOnlyAsync(Arg.Any<CancellationToken>()).Returns((IReadOnlyList<Subject>)[_subject]);
        _mappings.ListActiveByTermReadOnlyAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns((IReadOnlyList<SubjectMapping>)[]);
        _exceptions.ListByTermReadOnlyAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>()).Returns((IReadOnlyList<SubjectMappingException>)[]);
        _arms.ListAllReadOnlyAsync(Arg.Any<CancellationToken>()).Returns((IReadOnlyList<Arm>)[]);

        var term = Term.Create(_termId, _sessionId, 1, "First Term", new DateOnly(2026, 9, 1), new DateOnly(2026, 12, 1)).Value;
        _terms.FindReadOnlyByIdAsync(_termId, Arg.Any<CancellationToken>()).Returns(term);
    }

    // A resolver built with these fakes never needs to resolve anything real, since no arm exists —
    // the handler's own composition root builds the real resolver in production; here it is
    // constructed directly for the same reason SubjectsInEffectResolverTests constructs it directly.
    private ListSubjectsHandler CreateHandler() =>
        new(_subjects, _mappings, _exceptions, _arms, _terms, _enrolments,
            new SubjectsInEffectResolver(_arms, _subjects, _mappings, _exceptions));

    [Fact]
    public async Task HandleAsync_WithNoTermId_ReturnsNullForAllThreeCounts()
    {
        var result = await CreateHandler().HandleAsync(
            new ListSubjectsQuery(null, null, null, null, TermId: null), TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue();
        var dto = result.Value.Items.Single();
        dto.MappedLevelCount.ShouldBeNull();
        dto.ArmExceptionCount.ShouldBeNull();
        dto.PupilsTakingCount.ShouldBeNull();
    }

    [Fact]
    public async Task HandleAsync_WithATermId_ReturnsComputedCountsRatherThanNull()
    {
        var levelId = Guid.CreateVersion7();
        var mapping = SubjectMapping.Create(Guid.CreateVersion7(), _subject.Id, levelId, _sessionId, _termId, 1).Value;
        _mappings.ListActiveByTermReadOnlyAsync(_termId, Arg.Any<CancellationToken>()).Returns((IReadOnlyList<SubjectMapping>)[mapping]);

        var result = await CreateHandler().HandleAsync(
            new ListSubjectsQuery(null, null, null, null, _termId.ToString()), TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue();
        var dto = result.Value.Items.Single();
        dto.MappedLevelCount.ShouldBe(1);
        dto.ArmExceptionCount.ShouldBe(0);
        dto.PupilsTakingCount.ShouldBe(0); // no arms exist in the fake session, so nobody is "taking" it yet
    }

    [Fact]
    public async Task HandleAsync_WithATermIdThatDoesNotExist_ReturnsTermNotFound()
    {
        var unknownTermId = Guid.CreateVersion7();
        _terms.FindReadOnlyByIdAsync(unknownTermId, Arg.Any<CancellationToken>()).Returns((Term?)null);

        var result = await CreateHandler().HandleAsync(
            new ListSubjectsQuery(null, null, null, null, unknownTermId.ToString()), TestContext.Current.CancellationToken);

        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe("subject.term_not_found");
    }
}
