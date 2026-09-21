using NSubstitute;
using SchoolManagement.Application.Abstractions.Audit;
using SchoolManagement.Application.Abstractions.Authorization;
using SchoolManagement.Application.Abstractions.Identity;
using SchoolManagement.Application.Abstractions.Results;
using SchoolManagement.Application.Results;
using SchoolManagement.Domain.Results;
using SchoolManagement.Domain.Security;

namespace SchoolManagement.UnitTests.Application.Results;

/// <summary>
/// Tests <see cref="DeleteRemarkTemplateHandler"/>: the 403-before-404 ordering and the
/// wrong-kind-never-404-never-deletes rule (delta item 4).
/// </summary>
public sealed class DeleteRemarkTemplateHandlerTests
{
    private static readonly Guid UserId = Guid.CreateVersion7();
    private static readonly Guid TemplateId = Guid.CreateVersion7();
    private static readonly Guid ArmId = Guid.CreateVersion7();

    private readonly IRemarkTemplateRepository _templates = Substitute.For<IRemarkTemplateRepository>();
    private readonly IEffectivePrivilegeProvider _grantsProvider = Substitute.For<IEffectivePrivilegeProvider>();
    private readonly ICurrentUser _currentUser = Substitute.For<ICurrentUser>();
    private readonly ISystemAuditSink _auditSink = Substitute.For<ISystemAuditSink>();

    public DeleteRemarkTemplateHandlerTests() => _currentUser.UserId.Returns(UserId.ToString());

    private DeleteRemarkTemplateHandler CreateHandler() => new(_templates, _grantsProvider, _currentUser, _auditSink);

    [Fact]
    public async Task HandleAsync_HoldingNeitherPrivilege_Returns403WithoutEverLookingUpTheId()
    {
        _grantsProvider.GetGrantsAsync(UserId.ToString(), Arg.Any<CancellationToken>()).Returns([]);

        var result = await CreateHandler().HandleAsync(new DeleteRemarkTemplateCommand(TemplateId), TestContext.Current.CancellationToken);

        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe("remark_template.delete_forbidden");
        // 403 before 404: an id lookup must never happen for a caller holding neither privilege.
        await _templates.DidNotReceive().FindTrackedByIdAsync(Arg.Any<Guid>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleAsync_HoldingAtLeastOnePrivilege_UnknownId_Returns404()
    {
        _grantsProvider.GetGrantsAsync(UserId.ToString(), Arg.Any<CancellationToken>())
            .Returns([new PrivilegeGrant(Privileges.Results.RemarkClassTeacher, ScopeType.ArmList, new HashSet<Guid> { ArmId }, SessionId: null)]);
        _templates.FindTrackedByIdAsync(TemplateId, Arg.Any<CancellationToken>()).Returns((RemarkTemplate?)null);

        var result = await CreateHandler().HandleAsync(new DeleteRemarkTemplateCommand(TemplateId), TestContext.Current.CancellationToken);

        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe("remark_template.not_found");
    }

    [Fact]
    public async Task HandleAsync_HoldingOnlyTheOtherKindsPrivilege_Returns403NeverDeletesNeverFound()
    {
        _grantsProvider.GetGrantsAsync(UserId.ToString(), Arg.Any<CancellationToken>())
            .Returns([new PrivilegeGrant(Privileges.Results.RemarkHeadTeacher, ScopeType.SchoolWide, new HashSet<Guid>(), SessionId: null)]);
        var existing = RemarkTemplate.Create(TemplateId, RemarkKind.ClassTeacher, "Text").Value;
        _templates.FindTrackedByIdAsync(TemplateId, Arg.Any<CancellationToken>()).Returns(existing);

        var result = await CreateHandler().HandleAsync(new DeleteRemarkTemplateCommand(TemplateId), TestContext.Current.CancellationToken);

        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe("remark_template.delete_forbidden");
        await _templates.DidNotReceive().RemoveAsync(Arg.Any<RemarkTemplate>(), Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task HandleAsync_HoldingTheMatchingKindsPrivilege_DeletesAndAudits()
    {
        _grantsProvider.GetGrantsAsync(UserId.ToString(), Arg.Any<CancellationToken>())
            .Returns([new PrivilegeGrant(Privileges.Results.RemarkClassTeacher, ScopeType.ArmList, new HashSet<Guid> { ArmId }, SessionId: null)]);
        var existing = RemarkTemplate.Create(TemplateId, RemarkKind.ClassTeacher, "Text").Value;
        _templates.FindTrackedByIdAsync(TemplateId, Arg.Any<CancellationToken>()).Returns(existing);

        var result = await CreateHandler().HandleAsync(new DeleteRemarkTemplateCommand(TemplateId), TestContext.Current.CancellationToken);

        result.IsSuccess.ShouldBeTrue();
        await _templates.Received(1).RemoveAsync(existing, Arg.Any<CancellationToken>());
        await _auditSink.Received(1).RecordAsync(
            Arg.Is(Privileges.Results.RemarkClassTeacher), Arg.Is("remark_template"), Arg.Any<string?>(),
            Arg.Is<IReadOnlyDictionary<string, object?>?>(metadata => metadata == null),
            Arg.Any<string?>(), Arg.Any<CancellationToken>(), Arg.Any<string?>(),
            Arg.Is<IReadOnlyDictionary<string, object?>>(metadata => ContainsTheText(metadata)));
    }

    private static bool ContainsTheText(IReadOnlyDictionary<string, object?>? metadata) =>
        metadata is not null && metadata.TryGetValue("text", out var text) && Equals(text, "Text");
}
