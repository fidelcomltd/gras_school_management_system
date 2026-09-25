using SchoolManagement.Domain.Common;
using SchoolManagement.Domain.Pupils;

namespace SchoolManagement.UnitTests.Domain.Pupils;

/// <summary>Entity-local invariants for <see cref="Pupil"/> (spec 6.5.4, 6.5.10).</summary>
public sealed class PupilTests
{
    private static readonly DateOnly Today = new(2026, 9, 9);

    private static Result<Pupil> CreateValid(
        DateOnly? dateOfBirth = null,
        string? nationality = null,
        string stateOfOrigin = "Anambra",
        string lga = "Awka South") =>
        Pupil.Create(
            Guid.CreateVersion7(),
            "Okafor",
            "Chidera",
            "Ngozi",
            PupilSex.Female,
            dateOfBirth ?? new DateOnly(2020, 5, 3),
            Today,
            nationality,
            stateOfOrigin,
            lga,
            "14 Zik Avenue, Awka",
            previousSchool: null,
            previousClass: null,
            otherInformation: null);

    [Fact]
    public void Create_HappyPath_DefaultsPendingAndNullRegistrationNumber()
    {
        var result = CreateValid();

        result.IsSuccess.ShouldBeTrue(result.IsFailure ? result.Error.Description : string.Empty);
        result.Value.Status.ShouldBe(PupilStatus.Pending);
        result.Value.RegistrationNumber.ShouldBeNull();
    }

    [Fact]
    public void Create_WithNoMiddleNameOrNationality_DefaultsNationalityToNigerian()
    {
        var result = Pupil.Create(
            Guid.CreateVersion7(), "Bello", "Amina", null, PupilSex.Female, new DateOnly(2019, 1, 1), Today,
            nationality: null, "Kano", "Kano Municipal", "1 Zaria Road", null, null, null);

        result.IsSuccess.ShouldBeTrue();
        result.Value.Nationality.ShouldBe(Pupil.DefaultNationality);
    }

    // Spec 6.5.4's own worked example, formatted with real values rather than copied literally.
    [Fact]
    public void Create_WithAnAgeBelowTheMinimum_RejectsWithTheVerbatimMessageShape()
    {
        var dateOfBirth = new DateOnly(2026, 5, 3); // ~4 months old as of Today — under MinAgeYears.

        var result = CreateValid(dateOfBirth);

        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe("pupil.date_of_birth_out_of_range");
        result.Error.Description.ShouldBe("A date of birth of 03/05/2026 makes this pupil 0 years old. Check the date.");
    }

    [Fact]
    public void Create_WithAnAgeAboveTheMaximum_Rejects()
    {
        var dateOfBirth = new DateOnly(2005, 1, 1); // 21 as of Today — one over MaxAgeYears.

        var result = CreateValid(dateOfBirth);

        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe("pupil.date_of_birth_out_of_range");
        result.Error.Description.ShouldContain("21 years old");
    }

    [Theory]
    [InlineData(2)]
    [InlineData(20)]
    public void Create_AtEitherAgeBoundary_IsInclusiveAndSucceeds(int ageYears)
    {
        var dateOfBirth = Today.AddYears(-ageYears);

        var result = CreateValid(dateOfBirth);

        result.IsSuccess.ShouldBeTrue(result.IsFailure ? result.Error.Description : string.Empty);
    }

    [Fact]
    public void Create_WithAFreeTextStateOfOrigin_Rejects()
    {
        var result = CreateValid(stateOfOrigin: "Anam Bra", lga: "Awka South");

        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe("pupil.state_of_origin_invalid");
    }

    [Fact]
    public void Create_WithALgaFromTheWrongState_Rejects()
    {
        var result = CreateValid(stateOfOrigin: "Lagos", lga: "Awka South");

        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe("pupil.lga_invalid");
    }

    [Theory]
    [InlineData("anambra")]
    [InlineData("ANAMBRA")]
    [InlineData(" Anambra ")]
    public void Create_NormalisesStateOfOriginCasingAndWhitespace(string typed)
    {
        var result = CreateValid(stateOfOrigin: typed);

        result.IsSuccess.ShouldBeTrue(result.IsFailure ? result.Error.Description : string.Empty);
        result.Value.StateOfOrigin.ShouldBe("Anambra");
    }

    [Fact]
    public void Create_WithAnInvalidSurnameCharacter_Rejects()
    {
        var result = Pupil.Create(
            Guid.CreateVersion7(), "Okafor3", "Chidera", null, PupilSex.Female, new DateOnly(2020, 5, 3), Today,
            null, "Anambra", "Awka South", "14 Zik Avenue", null, null, null);

        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe("pupil.surname_invalid_characters");
    }

    [Fact]
    public void UpdateBiographical_WithRegistrationNumberNotOfferedOnTheEntity_HasNoPublicSetter()
    {
        // There is no method on Pupil that ever assigns RegistrationNumber after Create — spec
        // 6.5.10's "no ordinary edit path exists" is enforced by the type simply not offering one.
        // Reflective proof, not merely an absence of a method call in this test file.
        var setter = typeof(Pupil).GetProperty(nameof(Pupil.RegistrationNumber))!.GetSetMethod(nonPublic: true);
        setter.ShouldNotBeNull();
        setter.IsPrivate.ShouldBeTrue();
    }

    [Fact]
    public void UpdateBiographical_ChangingOnlyHomeAddress_LeavesEveryOtherFieldUnchanged()
    {
        var pupil = CreateValid().Value;

        var result = pupil.UpdateBiographical(
            surname: null, firstName: null, middleName: null, sex: null, dateOfBirth: null, Today,
            nationality: null, stateOfOrigin: null, lga: null, homeAddress: "22 New Address",
            previousSchool: null, previousClass: null, otherInformation: null);

        result.IsSuccess.ShouldBeTrue();
        pupil.HomeAddress.ShouldBe("22 New Address");
        pupil.Surname.ShouldBe("Okafor");
        pupil.StateOfOrigin.ShouldBe("Anambra");
        pupil.Lga.ShouldBe("Awka South");
    }

    [Fact]
    public void UpdateBiographical_ClearingMiddleNameWithAnEmptyString_SetsItToNull()
    {
        var pupil = CreateValid().Value;

        var result = pupil.UpdateBiographical(
            null, null, middleName: "", null, null, Today, null, null, null, null, null, null, null);

        result.IsSuccess.ShouldBeTrue();
        pupil.MiddleName.ShouldBeNull();
    }

    [Fact]
    public void UpdateBiographical_ChangingStateWithoutANewLga_RevalidatesTheExistingLgaAgainstTheNewState()
    {
        var pupil = CreateValid(stateOfOrigin: "Anambra", lga: "Awka South").Value;

        var result = pupil.UpdateBiographical(
            null, null, null, null, null, Today, null, stateOfOrigin: "Lagos", lga: null, null, null, null, null);

        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe("pupil.lga_invalid_after_state_change");
    }

    [Fact]
    public void UpdateBiographical_ChangingStateAndLgaTogether_Succeeds()
    {
        var pupil = CreateValid(stateOfOrigin: "Anambra", lga: "Awka South").Value;

        var result = pupil.UpdateBiographical(
            null, null, null, null, null, Today, null, stateOfOrigin: "Lagos", lga: "Ikeja", null, null, null, null);

        result.IsSuccess.ShouldBeTrue(result.IsFailure ? result.Error.Description : string.Empty);
        pupil.StateOfOrigin.ShouldBe("Lagos");
        pupil.Lga.ShouldBe("Ikeja");
    }

    [Fact]
    public void UpdateBiographical_WithAnOutOfRangeDateOfBirth_RejectsAndLeavesTheOriginalUnchanged()
    {
        var pupil = CreateValid().Value;
        var originalDateOfBirth = pupil.DateOfBirth;

        var result = pupil.UpdateBiographical(
            null, null, null, null, dateOfBirth: Today, Today, null, null, null, null, null, null, null);

        result.IsFailure.ShouldBeTrue();
        pupil.DateOfBirth.ShouldBe(originalDateOfBirth);
    }

    // TASK-0051: admission approval and decline.

    [Fact]
    public void Approve_OnAPendingPupil_TransitionsToActive()
    {
        var pupil = CreateValid().Value;

        var result = pupil.Approve();

        result.IsSuccess.ShouldBeTrue();
        pupil.Status.ShouldBe(PupilStatus.Active);
    }

    [Fact]
    public void Approve_DoesNotItselfSetARegistrationNumber()
    {
        var pupil = CreateValid().Value;

        pupil.Approve();

        pupil.RegistrationNumber.ShouldBeNull();
    }

    [Fact]
    public void Approve_OnAnAlreadyApprovedPupil_FailsWithConflict()
    {
        var pupil = CreateValid().Value;
        pupil.Approve();

        var result = pupil.Approve();

        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe("pupil.already_approved");
    }

    [Fact]
    public void IssueRegistrationNumber_WritesTheSuppliedValue()
    {
        var pupil = CreateValid().Value;
        pupil.Approve();

        pupil.IssueRegistrationNumber("GRAS/2026/0001");

        pupil.RegistrationNumber.ShouldBe("GRAS/2026/0001");
    }

    [Fact]
    public void IssueRegistrationNumber_CalledTwice_TheSecondCallOverwritesTheFirst()
    {
        // The retry-on-conflict loop's own precondition: a failed attempt's number must be
        // replaceable by a fresh one against the SAME tracked entity, with no guard blocking it.
        var pupil = CreateValid().Value;
        pupil.Approve();
        pupil.IssueRegistrationNumber("GRAS/2026/0001");

        pupil.IssueRegistrationNumber("GRAS/2026/0002");

        pupil.RegistrationNumber.ShouldBe("GRAS/2026/0002");
    }

    [Fact]
    public void CorrectRegistrationNumber_OnAPupilHoldingANumber_OverwritesIt()
    {
        var pupil = CreateValid().Value;
        pupil.Approve();
        pupil.IssueRegistrationNumber("GRAS/2025/0041");

        var result = pupil.CorrectRegistrationNumber("GRAS/2026/0041");

        result.IsSuccess.ShouldBeTrue();
        pupil.RegistrationNumber.ShouldBe("GRAS/2026/0041");
    }

    [Fact]
    public void CorrectRegistrationNumber_OnAPendingPupilWithNoNumberYet_Fails()
    {
        var pupil = CreateValid().Value;

        var result = pupil.CorrectRegistrationNumber("GRAS/2026/0041");

        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe("pupil.registration_number_not_issued");
        pupil.RegistrationNumber.ShouldBeNull();
    }

    [Fact]
    public void DeclineAdmission_OnAPendingPupil_TransitionsToWithdrawnAndIssuesNoNumber()
    {
        var pupil = CreateValid().Value;

        var result = pupil.DeclineAdmission();

        result.IsSuccess.ShouldBeTrue();
        pupil.Status.ShouldBe(PupilStatus.Withdrawn);
        pupil.RegistrationNumber.ShouldBeNull();
    }

    [Fact]
    public void DeclineAdmission_OnAnAlreadyApprovedPupil_Fails()
    {
        var pupil = CreateValid().Value;
        pupil.Approve();

        var result = pupil.DeclineAdmission();

        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe("pupil.not_pending");
    }

    [Theory]
    [InlineData(PupilStatus.Transferred)]
    [InlineData(PupilStatus.Withdrawn)]
    [InlineData(PupilStatus.Graduated)]
    public void Leave_FromActive_SetsTheLeavingStatus(PupilStatus target)
    {
        var pupil = CreateValid().Value;
        pupil.Approve();

        pupil.Leave(target).IsSuccess.ShouldBeTrue();
        pupil.Status.ShouldBe(target);
    }

    [Fact]
    public void Leave_FromPending_Fails()
    {
        var pupil = CreateValid().Value;

        var result = pupil.Leave(PupilStatus.Withdrawn);

        result.Error.Code.ShouldBe("pupil.status_transition_not_allowed");
        pupil.Status.ShouldBe(PupilStatus.Pending);
    }

    [Fact]
    public void Reactivate_AfterLeaving_ReturnsToActiveKeepingTheNumber()
    {
        var pupil = CreateValid().Value;
        pupil.Approve();
        pupil.IssueRegistrationNumber("GRAS/2026/0041");
        pupil.Leave(PupilStatus.Withdrawn);

        pupil.Reactivate().IsSuccess.ShouldBeTrue();
        pupil.Status.ShouldBe(PupilStatus.Active);
        pupil.RegistrationNumber.ShouldBe("GRAS/2026/0041");
    }

    [Fact]
    public void Reactivate_ADeclinedApplication_Fails()
    {
        var pupil = CreateValid().Value;
        pupil.DeclineAdmission();

        var result = pupil.Reactivate();

        result.Error.Code.ShouldBe("pupil.reactivation_needs_admission");
        pupil.Status.ShouldBe(PupilStatus.Withdrawn);
    }
}
