using NSubstitute;
using SchoolManagement.Application.Abstractions.Audit;
using SchoolManagement.Application.Abstractions.Identity;
using SchoolManagement.Application.Abstractions.Subjects;
using SchoolManagement.Application.Subjects;
using SchoolManagement.Domain.Subjects;

namespace SchoolManagement.UnitTests.Application.Subjects;

/// <summary>
/// Tests <see cref="CreateSubjectHandler"/>: the two uniqueness rejections (spec 6.6.2) and the happy
/// path. Carried from TASK-0070 closure — the <c>code_duplicate</c> rejection had no test of its own
/// (<c>name_duplicate</c>'s sibling), and no unit test file existed for this handler at all.
/// </summary>
public sealed class CreateSubjectCommandHandlerTests
{
    private readonly ISubjectRepository _subjects = Substitute.For<ISubjectRepository>();
    private readonly ICurrentUser _currentUser = Substitute.For<ICurrentUser>();
    private readonly ISystemAuditSink _auditSink = Substitute.For<ISystemAuditSink>();

    public CreateSubjectCommandHandlerTests()
    {
        _subjects.NameExistsAsync(Arg.Any<string>(), null, Arg.Any<CancellationToken>()).Returns(false);
        _subjects.FindByCodeKeyAsync(Arg.Any<string>(), null, Arg.Any<CancellationToken>()).Returns((Subject?)null);
        _currentUser.UserId.Returns("admin-1");
    }

    private CreateSubjectHandler CreateHandler() => new(_subjects, _currentUser, _auditSink);

    [Fact]
    public async Task HandleAsync_WithANameAlreadyInUse_Returns409AndDoesNotCreateTheSubject()
    {
        _subjects.NameExistsAsync("mathematics", null, Arg.Any<CancellationToken>()).Returns(true);

        var result = await CreateHandler().HandleAsync(
            new CreateSubjectCommand("Mathematics", null, null),
            TestContext.Current.CancellationToken);

        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe("subject.name_duplicate");
        result.Error.Description.ShouldContain("Mathematics");
        await _subjects.DidNotReceive().AddAsync(Arg.Any<Subject>(), Arg.Any<CancellationToken>());
    }

    // The carried test: CreateSubjectHandler's other uniqueness branch, FindByCodeKeyAsync returning
    // an existing subject, was never exercised by any test in the suite (TASK-0070 closure).
    [Fact]
    public async Task HandleAsync_WithACodeAlreadyUsedByAnotherSubject_Returns409NamingTheConflictingSubject()
    {
        var existing = Subject.Create(Guid.CreateVersion7(), "English Language", "ENG", null).Value;
        _subjects.FindByCodeKeyAsync("eng", null, Arg.Any<CancellationToken>()).Returns(existing);

        var result = await CreateHandler().HandleAsync(
            new CreateSubjectCommand("English Studies", "ENG", null),
            TestContext.Current.CancellationToken);

        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe("subject.code_duplicate");
        result.Error.Description.ShouldContain("English Language");
        await _subjects.DidNotReceive().AddAsync(Arg.Any<Subject>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleAsync_WithNoCodeSupplied_NeverChecksCodeUniqueness()
    {
        var result = await CreateHandler().HandleAsync(
            new CreateSubjectCommand("Mathematics", null, null),
            TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue();
        await _subjects.DidNotReceive().FindByCodeKeyAsync(Arg.Any<string>(), Arg.Any<Guid?>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleAsync_WithAUniqueNameAndCode_CreatesTheSubjectAndAudits()
    {
        var result = await CreateHandler().HandleAsync(
            new CreateSubjectCommand("Mathematics", "MTH", "Core numeracy subject"),
            TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue();
        result.Value.Name.ShouldBe("Mathematics");
        result.Value.Code.ShouldBe("MTH");

        await _subjects.Received(1).AddAsync(
            Arg.Is<Subject>(subject => subject != null && subject.Name == "Mathematics" && subject.Code == "MTH"),
            Arg.Any<CancellationToken>());
        await _auditSink.Received(1).RecordAsync(
            Arg.Any<string>(),
            "subject",
            Arg.Any<string>(),
            Arg.Any<IReadOnlyDictionary<string, object?>>(),
            "admin-1",
            Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleAsync_RejectsANullRequest()
    {
        await Should.ThrowAsync<ArgumentNullException>(
            () => CreateHandler().HandleAsync(null!, TestContext.Current.CancellationToken));
    }
}
