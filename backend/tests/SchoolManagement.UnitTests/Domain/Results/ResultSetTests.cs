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
}
