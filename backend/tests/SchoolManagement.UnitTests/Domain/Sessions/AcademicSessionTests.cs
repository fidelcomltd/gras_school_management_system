using SchoolManagement.Domain.Sessions;

namespace SchoolManagement.UnitTests.Domain.Sessions;

/// <summary>Spec 6.3.3's field rules and invariants for <see cref="AcademicSession"/>.</summary>
public sealed class AcademicSessionTests
{
    private static readonly Guid FixedId = Guid.Parse("0192f0c4-7c3e-7a1b-9f2d-3b8e5a6c1d40");

    [Fact]
    public void Create_WithValidFields_Succeeds()
    {
        var result = AcademicSession.Create(
            FixedId, "2026/2027", new DateOnly(2026, 9, 14), new DateOnly(2027, 7, 25));

        result.IsSuccess.ShouldBeTrue();
        result.Value.Name.ShouldBe("2026/2027");
        result.Value.StartDate.ShouldBe(new DateOnly(2026, 9, 14));
        result.Value.EndDate.ShouldBe(new DateOnly(2027, 7, 25));
        result.Value.State.ShouldBe(SessionState.Upcoming);
    }

    [Fact]
    public void Create_WithAnEmptyId_Fails()
    {
        var result = AcademicSession.Create(
            Guid.Empty, "2026/2027", new DateOnly(2026, 9, 14), new DateOnly(2027, 7, 25));

        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe("session.id_required");
    }

    [Theory]
    [InlineData("2026-2027")]
    [InlineData("2026/27")]
    [InlineData("26/2027")]
    [InlineData("2026/2027 ")]
    [InlineData("")]
    public void Create_WithAMalformedName_Rejects(string name)
    {
        var result = AcademicSession.Create(FixedId, name, new DateOnly(2026, 9, 14), new DateOnly(2027, 7, 25));

        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe("session.name_invalid_format");
    }

    [Fact]
    public void Create_WithNonConsecutiveYears_Rejects()
    {
        // Spec 6.3.3: "A session runs across two calendar years. 2026/2028 is not valid."
        var result = AcademicSession.Create(FixedId, "2026/2028", new DateOnly(2026, 9, 14), new DateOnly(2028, 7, 25));

        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe("session.name_years_not_consecutive");
        result.Error.Description.ShouldContain("2026/2028");
    }

    [Fact]
    public void Create_WithStartDateOutsideFirstYear_Rejects()
    {
        var result = AcademicSession.Create(FixedId, "2026/2027", new DateOnly(2027, 1, 1), new DateOnly(2027, 7, 25));

        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe("session.start_date_wrong_year");
    }

    [Fact]
    public void Create_WithEndDateOutsideSecondYear_Rejects()
    {
        var result = AcademicSession.Create(FixedId, "2026/2027", new DateOnly(2026, 9, 14), new DateOnly(2026, 12, 25));

        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe("session.end_date_wrong_year");
    }

    // No case can reach "start >= end" once both year-membership checks pass — a date anywhere in
    // the first named year always precedes a date anywhere in the second (see AcademicSession's own
    // remarks) — so this proves the boundary instead: the LATEST possible start (31 Dec of the first
    // year) against the EARLIEST possible end (1 Jan of the second) still succeeds.
    [Fact]
    public void Create_WithStartOnTheLastDayOfItsYear_StillSucceeds()
    {
        var result = AcademicSession.Create(FixedId, "2026/2027", new DateOnly(2026, 12, 31), new DateOnly(2027, 1, 1));

        result.IsSuccess.ShouldBeTrue();
    }

    [Fact]
    public void Reschedule_WithValidFields_Succeeds()
    {
        var session = AcademicSession
            .Create(FixedId, "2026/2027", new DateOnly(2026, 9, 14), new DateOnly(2027, 7, 25))
            .Value;

        var result = session.Reschedule("2026/2027", new DateOnly(2026, 9, 1), new DateOnly(2027, 7, 30));

        result.IsSuccess.ShouldBeTrue();
        session.StartDate.ShouldBe(new DateOnly(2026, 9, 1));
        session.EndDate.ShouldBe(new DateOnly(2027, 7, 30));
    }

    [Fact]
    public void Reschedule_WithAMalformedName_Rejects()
    {
        var session = AcademicSession
            .Create(FixedId, "2026/2027", new DateOnly(2026, 9, 14), new DateOnly(2027, 7, 25))
            .Value;

        var result = session.Reschedule("2026-2027", session.StartDate, session.EndDate);

        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe("session.name_invalid_format");
    }

    [Fact]
    public void Activate_MovesToActive()
    {
        var session = AcademicSession
            .Create(FixedId, "2026/2027", new DateOnly(2026, 9, 14), new DateOnly(2027, 7, 25))
            .Value;

        session.Activate();

        session.State.ShouldBe(SessionState.Active);
    }

    [Fact]
    public void Close_MovesToClosed()
    {
        var session = AcademicSession
            .Create(FixedId, "2026/2027", new DateOnly(2026, 9, 14), new DateOnly(2027, 7, 25))
            .Value;

        session.Activate();
        session.Close();

        session.State.ShouldBe(SessionState.Closed);
    }
}
