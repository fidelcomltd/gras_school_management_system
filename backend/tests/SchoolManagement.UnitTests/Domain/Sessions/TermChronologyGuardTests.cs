using SchoolManagement.Domain.Sessions;

namespace SchoolManagement.UnitTests.Domain.Sessions;

/// <summary>Spec 6.3.4's cross-term/session date-sequencing rules.</summary>
public sealed class TermChronologyGuardTests
{
    [Fact]
    public void ValidateWithinSession_WhenInsideRange_Succeeds()
    {
        var result = TermChronologyGuard.ValidateWithinSession(
            "2026/2027", new DateOnly(2026, 9, 14), new DateOnly(2027, 7, 25),
            "First Term", new DateOnly(2026, 9, 14), new DateOnly(2026, 12, 18));

        result.IsSuccess.ShouldBeTrue();
    }

    [Fact]
    public void ValidateWithinSession_WhenStartsBeforeSession_Rejects()
    {
        var result = TermChronologyGuard.ValidateWithinSession(
            "2026/2027", new DateOnly(2026, 9, 14), new DateOnly(2027, 7, 25),
            "First Term", new DateOnly(2026, 8, 1), new DateOnly(2026, 12, 18));

        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe("term.outside_session_range");
    }

    [Fact]
    public void ValidateWithinSession_WhenEndsAfterSession_Rejects()
    {
        var result = TermChronologyGuard.ValidateWithinSession(
            "2026/2027", new DateOnly(2026, 9, 14), new DateOnly(2027, 7, 25),
            "Third Term", new DateOnly(2027, 4, 20), new DateOnly(2027, 8, 1));

        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe("term.outside_session_range");
    }

    [Fact]
    public void ValidateSequential_WhenNextStartsAfterPreviousEnds_Succeeds()
    {
        var result = TermChronologyGuard.ValidateSequential(
            "First Term", new DateOnly(2026, 12, 18), "Second Term", new DateOnly(2027, 1, 5));

        result.IsSuccess.ShouldBeTrue();
    }

    // Spec 6.3.9's own example: "Second Term starts on 05/01/2027, before First Term ends on
    // 18/12/2026 has passed. Terms cannot overlap."
    [Fact]
    public void ValidateSequential_WhenOverlapping_RejectsNamingBothTermsAndDates()
    {
        var result = TermChronologyGuard.ValidateSequential(
            "First Term", new DateOnly(2026, 12, 18), "Second Term", new DateOnly(2026, 12, 10));

        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe("term.overlap");
        result.Error.Description.ShouldContain("Second Term");
        result.Error.Description.ShouldContain("First Term");
        result.Error.Description.ShouldContain("10/12/2026");
        result.Error.Description.ShouldContain("18/12/2026");
    }

    [Fact]
    public void ValidateSequential_WhenNextStartsExactlyOnPreviousEnd_Rejects()
    {
        var result = TermChronologyGuard.ValidateSequential(
            "First Term", new DateOnly(2026, 12, 18), "Second Term", new DateOnly(2026, 12, 18));

        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe("term.overlap");
    }
}
