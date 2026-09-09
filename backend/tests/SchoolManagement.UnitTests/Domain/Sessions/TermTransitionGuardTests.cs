using SchoolManagement.Domain.Sessions;

namespace SchoolManagement.UnitTests.Domain.Sessions;

/// <summary>Spec 6.3.6's cross-entity open/reopen preconditions, as pure decisions.</summary>
public sealed class TermTransitionGuardTests
{
    private static readonly Guid SessionId = Guid.Parse("0192f0c4-7c3e-7a1b-9f2d-3b8e5a6c1d41");

    private static AcademicSession CreateSession(string name = "2026/2027") =>
        AcademicSession.Create(Guid.CreateVersion7(), name, new DateOnly(2026, 9, 14), new DateOnly(2027, 7, 25)).Value;

    private static Term CreateTerm(int ordinal, string name, Guid? sessionId = null) =>
        Term.Create(
                Guid.CreateVersion7(),
                sessionId ?? SessionId,
                ordinal,
                name,
                new DateOnly(2026, 9, 14),
                new DateOnly(2026, 12, 18))
            .Value;

    [Fact]
    public void CanOpen_OrdinalOne_WithNoActiveTermAnywhere_Succeeds()
    {
        var session = CreateSession();
        var term = CreateTerm(1, "First Term", session.Id);

        var result = TermTransitionGuard.CanOpen(
            term, session, previousTermInSession: null, activeTermElsewhere: null, hasArmsForSession: true);

        result.IsSuccess.ShouldBeTrue();
    }

    // TASK-0039, spec 6.3.6's third precondition, and spec 12's own worked flow example verbatim:
    // "No arms exist for 2026/2027. Create at least one arm before opening a term."
    [Fact]
    public void CanOpen_WithNoArmsForSession_Rejects()
    {
        var session = CreateSession("2026/2027");
        var term = CreateTerm(1, "First Term", session.Id);

        var result = TermTransitionGuard.CanOpen(
            term, session, previousTermInSession: null, activeTermElsewhere: null, hasArmsForSession: false);

        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe("term.no_arms_for_session");
        result.Error.Description.ShouldBe(
            "No arms exist for 2026/2027. Create at least one arm before opening a term.");
    }

    // Spec 6.3.6's own example: "First Term 2026/2027 cannot be opened because Third Term 2025/2026
    // is still active. Close it first."
    [Fact]
    public void CanOpen_OrdinalOne_WithAnActiveTermElsewhere_RejectsNamingIt()
    {
        var session = CreateSession("2026/2027");
        var term = CreateTerm(1, "First Term", session.Id);

        var result = TermTransitionGuard.CanOpen(
            term, session, previousTermInSession: null, activeTermElsewhere: ("Third Term", "2025/2026"),
            hasArmsForSession: true);

        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe("term.previous_session_still_active");
        result.Error.Description.ShouldBe(
            "First Term 2026/2027 cannot be opened because Third Term 2025/2026 is still active. Close it first.");
    }

    [Fact]
    public void CanOpen_OrdinalTwo_WithPreviousTermClosed_Succeeds()
    {
        var session = CreateSession();
        var previous = CreateTerm(1, "First Term", session.Id);
        previous.Open();
        previous.SetTimesSchoolOpened(62);
        previous.Close(DateTimeOffset.UtcNow, "admin-1");
        var term = CreateTerm(2, "Second Term", session.Id);

        var result = TermTransitionGuard.CanOpen(term, session, previous, activeTermElsewhere: null, hasArmsForSession: true);

        result.IsSuccess.ShouldBeTrue();
    }

    [Fact]
    public void CanOpen_OrdinalTwo_WithPreviousTermStillActive_Rejects()
    {
        var session = CreateSession();
        var previous = CreateTerm(1, "First Term", session.Id);
        previous.Open();
        var term = CreateTerm(2, "Second Term", session.Id);

        var result = TermTransitionGuard.CanOpen(term, session, previous, activeTermElsewhere: null, hasArmsForSession: true);

        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe("term.previous_term_not_closed");
        result.Error.Description.ShouldContain("still active");
    }

    [Fact]
    public void CanOpen_OrdinalTwo_WithPreviousTermNeverOpened_Rejects()
    {
        var session = CreateSession();
        var previous = CreateTerm(1, "First Term", session.Id);
        var term = CreateTerm(2, "Second Term", session.Id);

        var result = TermTransitionGuard.CanOpen(term, session, previous, activeTermElsewhere: null, hasArmsForSession: true);

        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe("term.previous_term_not_closed");
        result.Error.Description.ShouldContain("not yet closed");
    }

    [Fact]
    public void CanOpen_WhenTermIsNotUpcoming_Rejects()
    {
        var session = CreateSession();
        var term = CreateTerm(1, "First Term", session.Id);
        term.Open();

        var result = TermTransitionGuard.CanOpen(term, session, previousTermInSession: null, activeTermElsewhere: null, hasArmsForSession: true);

        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe("term.open_invalid_state");
    }

    [Fact]
    public void CanReopen_WhenClosedAndFollowingTermNotOpened_Succeeds()
    {
        var term = CreateTerm(1, "First Term");
        term.Open();
        term.SetTimesSchoolOpened(62);
        term.Close(DateTimeOffset.UtcNow, "admin-1");

        var result = TermTransitionGuard.CanReopen(term, followingTermOpened: false);

        result.IsSuccess.ShouldBeTrue();
    }

    [Fact]
    public void CanReopen_WhenNotClosed_Rejects()
    {
        var term = CreateTerm(1, "First Term");
        term.Open();

        var result = TermTransitionGuard.CanReopen(term, followingTermOpened: false);

        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe("term.reopen_invalid_state");
    }

    // Spec 6.3.6: reopening "is refused outright if the following term has already been opened" —
    // this is also load-bearing for the one-active-term invariant (ReopenTermHandler's remarks).
    [Fact]
    public void CanReopen_WhenFollowingTermAlreadyOpened_Rejects()
    {
        var term = CreateTerm(1, "First Term");
        term.Open();
        term.SetTimesSchoolOpened(62);
        term.Close(DateTimeOffset.UtcNow, "admin-1");

        var result = TermTransitionGuard.CanReopen(term, followingTermOpened: true);

        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe("term.reopen_blocked_by_following_term");
    }
}
