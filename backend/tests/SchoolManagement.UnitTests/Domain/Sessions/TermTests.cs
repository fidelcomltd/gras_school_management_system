using SchoolManagement.Domain.Sessions;

namespace SchoolManagement.UnitTests.Domain.Sessions;

/// <summary>Spec 6.3.4's field rules and 6.3.6's state machine for <see cref="Term"/>.</summary>
public sealed class TermTests
{
    private static readonly Guid FixedId = Guid.Parse("0192f0c4-7c3e-7a1b-9f2d-3b8e5a6c1d40");
    private static readonly Guid SessionId = Guid.Parse("0192f0c4-7c3e-7a1b-9f2d-3b8e5a6c1d41");
    private static readonly DateOnly Start = new(2026, 9, 14);
    private static readonly DateOnly End = new(2026, 12, 18);

    private static Term CreateUpcoming() => Term.Create(FixedId, SessionId, 1, "First Term", Start, End).Value;

    [Fact]
    public void Create_WithValidFields_Succeeds()
    {
        var result = Term.Create(FixedId, SessionId, 1, "First Term", Start, End);

        result.IsSuccess.ShouldBeTrue();
        result.Value.Ordinal.ShouldBe(1);
        result.Value.Name.ShouldBe("First Term");
        result.Value.State.ShouldBe(TermState.Upcoming);
        result.Value.TimesSchoolOpened.ShouldBeNull();
    }

    [Theory]
    [InlineData(0)]
    [InlineData(4)]
    public void Create_WithAnInvalidOrdinal_Rejects(int ordinal)
    {
        var result = Term.Create(FixedId, SessionId, ordinal, "First Term", Start, End);

        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe("term.ordinal_invalid");
    }

    [Fact]
    public void Create_WithStartDateNotBeforeEndDate_Rejects()
    {
        var result = Term.Create(FixedId, SessionId, 1, "First Term", End, End);

        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe("term.dates_out_of_order");
    }

    [Fact]
    public void Create_WithANameLongerThanTwentyCharacters_Rejects()
    {
        var result = Term.Create(FixedId, SessionId, 1, new string('a', Term.NameMaxLength + 1), Start, End);

        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe("term.name_too_long");
    }

    [Fact]
    public void SetTimesSchoolOpened_WithinRange_Succeeds()
    {
        var term = CreateUpcoming();

        var result = term.SetTimesSchoolOpened(62);

        result.IsSuccess.ShouldBeTrue();
        term.TimesSchoolOpened.ShouldBe(62);
    }

    [Theory]
    [InlineData(0)]
    [InlineData(201)]
    public void SetTimesSchoolOpened_OutOfRange_Rejects(int value)
    {
        var term = CreateUpcoming();

        var result = term.SetTimesSchoolOpened(value);

        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe("term.times_school_opened_out_of_range");
    }

    [Fact]
    public void SetTimesSchoolOpened_OnAClosedTerm_IsImmutable()
    {
        var term = CreateUpcoming();
        term.Open();
        term.SetTimesSchoolOpened(62);
        term.Close(DateTimeOffset.UtcNow, "admin-1");

        var result = term.SetTimesSchoolOpened(70);

        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe("term.times_school_opened_immutable");
        term.TimesSchoolOpened.ShouldBe(62);
    }

    [Fact]
    public void Open_FromUpcoming_Succeeds()
    {
        var term = CreateUpcoming();

        var result = term.Open();

        result.IsSuccess.ShouldBeTrue();
        term.State.ShouldBe(TermState.Active);
    }

    [Fact]
    public void Open_WhenNotUpcoming_Rejects()
    {
        var term = CreateUpcoming();
        term.Open();

        var result = term.Open();

        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe("term.open_invalid_state");
    }

    [Fact]
    public void Close_WhenTimesSchoolOpenedIsBlank_RejectsWithSpec6_3_9Wording()
    {
        var term = CreateUpcoming();
        term.Open();

        var result = term.Close(DateTimeOffset.UtcNow, "admin-1");

        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe("term.times_school_opened_required_to_close");
        result.Error.Description.ShouldContain("First Term");
    }

    [Fact]
    public void Close_WhenNotActive_Rejects()
    {
        var term = CreateUpcoming();

        var result = term.Close(DateTimeOffset.UtcNow, "admin-1");

        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe("term.close_invalid_state");
    }

    [Fact]
    public void Close_WhenActiveWithTimesSchoolOpenedSet_Succeeds()
    {
        var term = CreateUpcoming();
        term.Open();
        term.SetTimesSchoolOpened(62);
        var closedAt = DateTimeOffset.UtcNow;

        var result = term.Close(closedAt, "admin-1");

        result.IsSuccess.ShouldBeTrue();
        term.State.ShouldBe(TermState.Closed);
        term.ClosedAtUtc.ShouldBe(closedAt);
        term.ClosedBy.ShouldBe("admin-1");
    }

    [Fact]
    public void Reopen_WhenClosed_Succeeds()
    {
        var term = CreateUpcoming();
        term.Open();
        term.SetTimesSchoolOpened(62);
        term.Close(DateTimeOffset.UtcNow, "admin-1");

        var result = term.Reopen();

        result.IsSuccess.ShouldBeTrue();
        term.State.ShouldBe(TermState.Active);
        term.ClosedAtUtc.ShouldBeNull();
        term.ClosedBy.ShouldBeNull();
    }

    [Fact]
    public void Reopen_WhenNotClosed_Rejects()
    {
        var term = CreateUpcoming();

        var result = term.Reopen();

        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe("term.reopen_invalid_state");
    }

    // Spec 6.3.6: "score entry endpoints return 409 for that term; the trait, attendance and remark
    // endpoints do the same." No such endpoint exists yet — this proves the seam directly.
    [Fact]
    public void EnsureAcceptsEntry_WhenClosed_Rejects()
    {
        var term = CreateUpcoming();
        term.Open();
        term.SetTimesSchoolOpened(62);
        term.Close(DateTimeOffset.UtcNow, "admin-1");

        var result = term.EnsureAcceptsEntry();

        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe("term.closed_for_entry");
    }

    [Theory]
    [InlineData(0)] // Upcoming
    [InlineData(1)] // Active
    public void EnsureAcceptsEntry_WhenNotClosed_Succeeds(int stateOrdinal)
    {
        var term = CreateUpcoming();

        if (stateOrdinal == 1)
        {
            term.Open();
        }

        var result = term.EnsureAcceptsEntry();

        result.IsSuccess.ShouldBeTrue();
    }
}
