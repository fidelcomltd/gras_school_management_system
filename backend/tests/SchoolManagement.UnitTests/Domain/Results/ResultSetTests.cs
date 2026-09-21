using SchoolManagement.Domain.Results;

namespace SchoolManagement.UnitTests.Domain.Results;

/// <summary>Entity-local invariants for <see cref="ResultSet"/> (TASK-0076 dispatch B).</summary>
public sealed class ResultSetTests
{
    [Fact]
    public void Create_SetsNeedsRecomputeTrue()
    {
        var result = ResultSet.Create(Guid.CreateVersion7(), Guid.CreateVersion7(), Guid.CreateVersion7());

        result.IsSuccess.ShouldBeTrue();
        result.Value.NeedsRecompute.ShouldBeTrue();
        result.Value.State.ShouldBe(ResultSetState.Draft);
    }

    [Fact]
    public void MarkNeedsRecompute_OnAFreshSet_StaysTrue()
    {
        var resultSet = ResultSet.Create(Guid.CreateVersion7(), Guid.CreateVersion7(), Guid.CreateVersion7()).Value;

        resultSet.MarkNeedsRecompute();

        resultSet.NeedsRecompute.ShouldBeTrue();
    }

    // Proves the flag is unconditional, not a no-op guard against re-setting an already-true value —
    // the scenario a future computation endpoint depends on: clearing it, then a mark edit sets it
    // again.
    [Fact]
    public void MarkNeedsRecompute_AfterBeingClearedByReflection_SetsItBackToTrue()
    {
        var resultSet = ResultSet.Create(Guid.CreateVersion7(), Guid.CreateVersion7(), Guid.CreateVersion7()).Value;
        typeof(ResultSet).GetProperty(nameof(ResultSet.NeedsRecompute))!.SetValue(resultSet, false);
        resultSet.NeedsRecompute.ShouldBeFalse();

        resultSet.MarkNeedsRecompute();

        resultSet.NeedsRecompute.ShouldBeTrue();
    }

    private static ResultSet WithState(ResultSetState state)
    {
        var resultSet = ResultSet.Create(Guid.CreateVersion7(), Guid.CreateVersion7(), Guid.CreateVersion7()).Value;
        typeof(ResultSet).GetProperty(nameof(ResultSet.State))!.SetValue(resultSet, state);
        return resultSet;
    }

    // AC A3: Returned for Correction is the ONE state the system flag also moves — the state
    // machine's separate System row, distinct from the flag-only row every other state takes.
    [Fact]
    public void FlagNeedsRecomputeBySystem_OnAReturnedForCorrectionSet_DropsToDraftAndReportsAChange()
    {
        var resultSet = WithState(ResultSetState.ReturnedForCorrection);

        var stateChanged = resultSet.FlagNeedsRecomputeBySystem();

        stateChanged.ShouldBeTrue();
        resultSet.State.ShouldBe(ResultSetState.Draft);
        resultSet.NeedsRecompute.ShouldBeTrue();
    }

    // Awaiting Approval and Approved keep their state and only gain the flag (human ruling,
    // TASK-0088 carding) — proven for every OTHER state the flag can legally reach, so a change that
    // widened the drop-to-Draft branch to any of these would fail here first.
    [Theory]
    [InlineData(ResultSetState.Draft)]
    [InlineData(ResultSetState.AwaitingApproval)]
    [InlineData(ResultSetState.Approved)]
    public void FlagNeedsRecomputeBySystem_OnAnyOtherOpenState_OnlyFlagsAndReportsNoChange(ResultSetState state)
    {
        var resultSet = WithState(state);

        var stateChanged = resultSet.FlagNeedsRecomputeBySystem();

        stateChanged.ShouldBeFalse();
        resultSet.State.ShouldBe(state);
        resultSet.NeedsRecompute.ShouldBeTrue();
    }

    // Published and Withdrawn are never handed to this method by a caller that respects the
    // non-Published scope the flaggers query for — pinned anyway so the domain method itself, in
    // isolation, is proven not to silently resurrect a Published set into the editable state machine.
    [Theory]
    [InlineData(ResultSetState.Published)]
    [InlineData(ResultSetState.Withdrawn)]
    public void FlagNeedsRecomputeBySystem_OnAPublishedOrWithdrawnSet_NeverChangesState(ResultSetState state)
    {
        var resultSet = WithState(state);

        var stateChanged = resultSet.FlagNeedsRecomputeBySystem();

        stateChanged.ShouldBeFalse();
        resultSet.State.ShouldBe(state);
    }
}
