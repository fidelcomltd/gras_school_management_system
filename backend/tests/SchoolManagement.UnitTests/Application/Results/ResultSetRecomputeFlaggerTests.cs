using NSubstitute;
using SchoolManagement.Application.Abstractions.Audit;
using SchoolManagement.Application.Results;
using SchoolManagement.Domain.Results;

namespace SchoolManagement.UnitTests.Application.Results;

/// <summary>
/// Tests <see cref="ResultSetRecomputeFlagger"/> — the shared step behind TASK-0088 AC A1/A2/A3: every
/// already-locked set is flagged, and only the ONE state that also moves (Returned for Correction to
/// Draft) gets its own audit event with <c>before_json</c>/<c>after_json</c>.
/// </summary>
public sealed class ResultSetRecomputeFlaggerTests
{
    private readonly ISystemAuditSink _auditSink = Substitute.For<ISystemAuditSink>();

    private static ResultSet WithState(ResultSetState state)
    {
        var resultSet = ResultSet.Create(Guid.CreateVersion7(), Guid.CreateVersion7(), Guid.CreateVersion7()).Value;
        typeof(ResultSet).GetProperty(nameof(ResultSet.State))!.SetValue(resultSet, state);
        typeof(ResultSet).GetProperty(nameof(ResultSet.NeedsRecompute))!.SetValue(resultSet, false);
        return resultSet;
    }

    [Fact]
    public async Task FlagAsync_OnAReturnedForCorrectionSet_DropsItToDraftAndRecordsOneAuditEventAsSystem()
    {
        var resultSet = WithState(ResultSetState.ReturnedForCorrection);

        await ResultSetRecomputeFlagger.FlagAsync([resultSet], _auditSink, TestContext.Current.CancellationToken);

        resultSet.State.ShouldBe(ResultSetState.Draft);
        resultSet.NeedsRecompute.ShouldBeTrue();

        await _auditSink.Received(1).RecordAsync(
            "result_set.returned_for_correction_reverted_to_draft",
            "result_set",
            resultSet.Id.ToString(),
            Arg.Is<IReadOnlyDictionary<string, object?>?>(metadata =>
                metadata != null && (bool)metadata["needsRecompute"]! && (string)metadata["state"]! == "Draft"),
            actorAdminId: null,
            Arg.Any<CancellationToken>(),
            reason: null,
            beforeMetadata: Arg.Is<IReadOnlyDictionary<string, object?>?>(metadata =>
                metadata != null && !(bool)metadata["needsRecompute"]! && (string)metadata["state"]! == "ReturnedForCorrection"));
    }

    // Awaiting Approval and Approved keep their state and only gain the flag — no audit event of
    // their own, because this is not a state change (the triggering settings/mapping save already
    // carries its own audit event).
    [Theory]
    [InlineData(ResultSetState.Draft)]
    [InlineData(ResultSetState.AwaitingApproval)]
    [InlineData(ResultSetState.Approved)]
    public async Task FlagAsync_OnAnyOtherOpenState_FlagsWithoutAnAuditEvent(ResultSetState state)
    {
        var resultSet = WithState(state);

        await ResultSetRecomputeFlagger.FlagAsync([resultSet], _auditSink, TestContext.Current.CancellationToken);

        resultSet.State.ShouldBe(state);
        resultSet.NeedsRecompute.ShouldBeTrue();

        await _auditSink.DidNotReceive().RecordAsync(
            Arg.Any<string>(), Arg.Any<string?>(), Arg.Any<string?>(),
            Arg.Any<IReadOnlyDictionary<string, object?>?>(), Arg.Any<string?>(), Arg.Any<CancellationToken>(),
            Arg.Any<string?>(), Arg.Any<IReadOnlyDictionary<string, object?>?>());
    }

    [Fact]
    public async Task FlagAsync_OverASetOfMixedStates_FlagsEveryOneAndAuditsOnlyTheOneThatMoved()
    {
        var draft = WithState(ResultSetState.Draft);
        var approved = WithState(ResultSetState.Approved);
        var returned = WithState(ResultSetState.ReturnedForCorrection);

        await ResultSetRecomputeFlagger.FlagAsync([draft, approved, returned], _auditSink, TestContext.Current.CancellationToken);

        draft.NeedsRecompute.ShouldBeTrue();
        approved.NeedsRecompute.ShouldBeTrue();
        returned.NeedsRecompute.ShouldBeTrue();
        draft.State.ShouldBe(ResultSetState.Draft);
        approved.State.ShouldBe(ResultSetState.Approved);
        returned.State.ShouldBe(ResultSetState.Draft);

        await _auditSink.Received(1).RecordAsync(
            "result_set.returned_for_correction_reverted_to_draft",
            Arg.Any<string?>(), Arg.Any<string?>(), Arg.Any<IReadOnlyDictionary<string, object?>?>(),
            Arg.Any<string?>(), Arg.Any<CancellationToken>(), Arg.Any<string?>(), Arg.Any<IReadOnlyDictionary<string, object?>?>());
    }
}
