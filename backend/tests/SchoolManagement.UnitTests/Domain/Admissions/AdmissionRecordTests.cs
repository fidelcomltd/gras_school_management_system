using SchoolManagement.Domain.Admissions;

namespace SchoolManagement.UnitTests.Domain.Admissions;

/// <summary>Entity-local invariants for <see cref="AdmissionRecord"/> (spec 6.5.9, 6.5.11).</summary>
public sealed class AdmissionRecordTests
{
    private static readonly Guid PupilId = Guid.CreateVersion7();
    private static readonly Guid SessionId = Guid.CreateVersion7();
    private static readonly Guid ClassLevelId = Guid.CreateVersion7();
    private static readonly DateOnly Today = new(2026, 9, 15);

    private static AdmissionRecord CreateValid(bool assessmentRequired = false) =>
        AdmissionRecord.Create(
            Guid.CreateVersion7(),
            PupilId,
            SessionId,
            dateApplicationReceived: null,
            dateAdmitted: null,
            Today,
            ClassLevelId,
            AdmissionType.New,
            admissionTypeNote: null,
            assessmentRequired).Value;

    [Fact]
    public void Create_WithNoDateAdmitted_DefaultsToAsOfDate()
    {
        var record = CreateValid();

        record.DateAdmitted.ShouldBe(Today);
        record.DeclarationSigned.ShouldBeFalse();
        record.HeadOfSchoolConfirmed.ShouldBeFalse();
    }

    [Fact]
    public void Create_WithADateAdmittedInTheFuture_Fails()
    {
        var result = AdmissionRecord.Create(
            Guid.CreateVersion7(), PupilId, SessionId, null, Today.AddDays(1), Today,
            ClassLevelId, AdmissionType.New, null, false);

        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe("admission_record.date_admitted_in_future");
    }

    [Fact]
    public void Create_WithADateApplicationReceivedInTheFuture_Fails()
    {
        var result = AdmissionRecord.Create(
            Guid.CreateVersion7(), PupilId, SessionId, Today.AddDays(1), null, Today,
            ClassLevelId, AdmissionType.New, null, false);

        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe("admission_record.date_application_received_in_future");
    }

    // The acceptance criterion's first half: a required assessment with no recorded outcome still
    // SAVES. Nothing in Update enforces the requirement — only EnsureAssessmentResultRecordedIfRequired
    // (proven separately below) does, and no endpoint in this card calls that method.
    [Fact]
    public void Update_WithAssessmentRequiredAndNoRemarks_Saves()
    {
        var record = CreateValid(assessmentRequired: true);

        var result = record.Update(
            null, null, null, null, null, null, null, null, null, null, null, null, null, null, Today);

        result.IsSuccess.ShouldBeTrue();
        record.AssessmentResultRemarks.ShouldBeNull();
    }

    // The acceptance criterion's second half: the SAME state is rejected once approval is attempted.
    [Fact]
    public void EnsureAssessmentResultRecordedIfRequired_WhenRequiredButMissing_Fails()
    {
        var record = CreateValid(assessmentRequired: true);

        var result = record.EnsureAssessmentResultRecordedIfRequired();

        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe("admission_record.assessment_result_remarks_required_for_approval");
    }

    [Fact]
    public void EnsureAssessmentResultRecordedIfRequired_WhenRequiredAndRecorded_Succeeds()
    {
        var record = CreateValid(assessmentRequired: true);
        record.Update(
            null, null, null, null, null, null, null, "Passed the entrance assessment comfortably.",
            null, null, null, null, null, null, Today).IsSuccess.ShouldBeTrue();

        record.EnsureAssessmentResultRecordedIfRequired().IsSuccess.ShouldBeTrue();
    }

    [Fact]
    public void EnsureAssessmentResultRecordedIfRequired_WhenNotRequired_Succeeds()
    {
        var record = CreateValid(assessmentRequired: false);

        record.EnsureAssessmentResultRecordedIfRequired().IsSuccess.ShouldBeTrue();
    }

    [Fact]
    public void Update_SigningTheDeclarationWithNoDate_Fails()
    {
        var record = CreateValid();

        var result = record.Update(
            null, null, null, null, null, null, null, null, null, "Chinwe Okafor", true, null, null, null, Today);

        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe("admission_record.declaration_date_required");
    }

    [Fact]
    public void Update_ADeclarationDateWithoutSigning_Fails()
    {
        var record = CreateValid();

        var result = record.Update(
            null, null, null, null, null, null, null, null, null, null, null, Today, null, null, Today);

        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe("admission_record.declaration_date_not_allowed");
    }

    [Fact]
    public void Update_SigningTheDeclarationWithADate_Succeeds()
    {
        var record = CreateValid();

        var result = record.Update(
            null, null, null, null, null, null, null, null, null, "Chinwe Okafor", true, Today, null, null, Today);

        result.IsSuccess.ShouldBeTrue();
        record.DeclarationSigned.ShouldBeTrue();
        record.DeclarationDate.ShouldBe(Today);
        record.DeclarationName.ShouldBe("Chinwe Okafor");
    }

    [Fact]
    public void Update_ExplicitlyUnsigningTheDeclaration_AlsoClearsItsDate()
    {
        var record = CreateValid();
        record.Update(
            null, null, null, null, null, null, null, null, null, "Chinwe Okafor", true, Today, null, null, Today)
            .IsSuccess.ShouldBeTrue();

        var result = record.Update(
            null, null, null, null, null, null, null, null, null, null, false, null, null, null, Today);

        result.IsSuccess.ShouldBeTrue();
        record.DeclarationSigned.ShouldBeFalse();
        record.DeclarationDate.ShouldBeNull();
    }

    // The card's own acceptance criterion: two disjoint partial payloads, both survive.
    [Fact]
    public void Update_TwoDisjointPartialPayloads_BothSurvive()
    {
        var record = CreateValid();

        record.Update(
            null, null, null, null, null, "Sibling of an existing pupil", null, null, null,
            null, null, null, null, null, Today).IsSuccess.ShouldBeTrue();

        record.Update(
            null, null, null, null, null, null, null, null, null,
            "Chinwe Okafor", true, Today, null, null, Today).IsSuccess.ShouldBeTrue();

        record.AdmissionTypeNote.ShouldBe("Sibling of an existing pupil");
        record.DeclarationName.ShouldBe("Chinwe Okafor");
        record.DeclarationSigned.ShouldBeTrue();
        record.DeclarationDate.ShouldBe(Today);
    }

    [Fact]
    public void Update_AnEmptyStringClearsAnOptionalField()
    {
        var record = CreateValid();
        record.Update(
            null, null, null, null, null, "Sibling of an existing pupil", null, null, null,
            null, null, null, null, null, Today).IsSuccess.ShouldBeTrue();

        record.Update(
            null, null, null, null, null, string.Empty, null, null, null,
            null, null, null, null, null, Today).IsSuccess.ShouldBeTrue();

        record.AdmissionTypeNote.ShouldBeNull();
    }
}
