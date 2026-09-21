using NSubstitute;
using SchoolManagement.Application.Abstractions.Audit;
using SchoolManagement.Application.Abstractions.Authorization;
using SchoolManagement.Application.Abstractions.Identity;
using SchoolManagement.Application.Abstractions.Results;
using SchoolManagement.Application.Results;
using SchoolManagement.Domain.Results;
using SchoolManagement.Domain.Security;

namespace SchoolManagement.UnitTests.Application.Results;

/// <summary>Tests <see cref="CreateRemarkTemplateHandler"/>: per-kind privilege, duplicate 409, audit populated.</summary>
public sealed class CreateRemarkTemplateHandlerTests
{
    private static readonly Guid UserId = Guid.CreateVersion7();
    private static readonly Guid ArmId = Guid.CreateVersion7();

    private readonly IRemarkTemplateRepository _templates = Substitute.For<IRemarkTemplateRepository>();
    private readonly IEffectivePrivilegeProvider _grantsProvider = Substitute.For<IEffectivePrivilegeProvider>();
    private readonly ICurrentUser _currentUser = Substitute.For<ICurrentUser>();
    private readonly ISystemAuditSink _auditSink = Substitute.For<ISystemAuditSink>();
    private readonly TimeProvider _timeProvider = TimeProvider.System;

    public CreateRemarkTemplateHandlerTests()
    {
        _currentUser.UserId.Returns(UserId.ToString());
        _templates.ExistsWithTextAsync(Arg.Any<RemarkKind>(), Arg.Any<string>(), Arg.Any<CancellationToken>()).Returns(false);
        _grantsProvider.GetGrantsAsync(UserId.ToString(), Arg.Any<CancellationToken>())
            .Returns([new PrivilegeGrant(Privileges.Results.RemarkClassTeacher, ScopeType.ArmList, new HashSet<Guid> { ArmId }, SessionId: null)]);
    }

    private CreateRemarkTemplateHandler CreateHandler() => new(_templates, _grantsProvider, _currentUser, _auditSink, _timeProvider);

    [Fact]
    public async Task HandleAsync_WithAnArmScopedGrantForTheKind_Succeeds()
    {
        var result = await CreateHandler().HandleAsync(
            new CreateRemarkTemplateCommand(RemarkKind.ClassTeacher, "  A pleasure to have in school.  "), TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue();
        result.Value.Kind.ShouldBe(RemarkKind.ClassTeacher);
        result.Value.Text.ShouldBe("A pleasure to have in school.");
        await _templates.Received(1).AddAsync(Arg.Any<RemarkTemplate>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleAsync_HoldingOnlyTheOtherKindsPrivilege_Returns403()
    {
        var result = await CreateHandler().HandleAsync(
            new CreateRemarkTemplateCommand(RemarkKind.HeadTeacher, "Text"), TestContext.Current.CancellationToken);

        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe("remark_template.create_forbidden");
        await _templates.DidNotReceive().AddAsync(Arg.Any<RemarkTemplate>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleAsync_ADuplicateWithinTheSameKind_Returns409()
    {
        _templates.ExistsWithTextAsync(RemarkKind.ClassTeacher, "already exists.", Arg.Any<CancellationToken>()).Returns(true);

        var result = await CreateHandler().HandleAsync(
            new CreateRemarkTemplateCommand(RemarkKind.ClassTeacher, "Already Exists."), TestContext.Current.CancellationToken);

        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe("remark_template.duplicate");
        await _templates.DidNotReceive().AddAsync(Arg.Any<RemarkTemplate>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleAsync_OnSuccess_AuditsWithTheTextIncluded()
    {
        // Delta item 4 + card note: template text is NOT a pupil's personal data, so unlike remark
        // text (stage A) it MAY appear in audit JSON.
        var result = await CreateHandler().HandleAsync(
            new CreateRemarkTemplateCommand(RemarkKind.ClassTeacher, "A pleasure to have in school."), TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue();
        await _auditSink.Received(1).RecordAsync(
            Arg.Is(Privileges.Results.RemarkClassTeacher), Arg.Is("remark_template"), Arg.Any<string?>(),
            Arg.Is<IReadOnlyDictionary<string, object?>>(metadata => ContainsTheText(metadata)),
            Arg.Any<string?>(), Arg.Any<CancellationToken>(), Arg.Any<string?>(),
            Arg.Is<IReadOnlyDictionary<string, object?>?>(metadata => metadata == null));
    }

    private static bool ContainsTheText(IReadOnlyDictionary<string, object?>? metadata) =>
        metadata is not null && metadata.TryGetValue("text", out var text) && Equals(text, "A pleasure to have in school.");
}
