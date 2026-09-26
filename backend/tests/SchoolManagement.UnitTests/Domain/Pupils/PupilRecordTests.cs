using SchoolManagement.Domain.Pupils;

namespace SchoolManagement.UnitTests.Domain.Pupils;

/// <summary>Spec 6.5.5 to 6.5.8: the rules each admission-form section enforces on its own.</summary>
public sealed class PupilRecordTests
{
    [Fact]
    public void Contact_NormalisesThePhone_AndDropsFieldsTheRoleDoesNotHave()
    {
        var father = PupilContact.Create(Guid.CreateVersion7(), Guid.CreateVersion7(), ContactRole.Father);
        father.Apply("Emeka Okafor", "Father", "08031234567", "08031234567", "Engineer", null, isPrimary: true).IsSuccess.ShouldBeTrue();
        father.Phone.ShouldBe("+2348031234567");
        father.Relationship.ShouldBeNull();

        var emergency = PupilContact.Create(Guid.CreateVersion7(), Guid.CreateVersion7(), ContactRole.EmergencyPrimary);
        emergency.Apply("Ngozi Okafor", "Aunt", "+2348059876543", "08059876543", "Trader", null, isPrimary: false).IsSuccess.ShouldBeTrue();
        emergency.WhatsappNumber.ShouldBeNull();
        emergency.Occupation.ShouldBeNull();
    }

    [Theory]
    [InlineData("Emeka", "08031234567", null, "contact.full_name_invalid")]
    [InlineData("Emeka Okafor", "0803123", null, "contact.phone_invalid")]
    [InlineData("Emeka Okafor", "08031234567", "not-an-email", "contact.email_invalid")]
    public void Contact_RejectsWhatTheFormForbids(string name, string phone, string? email, string code)
    {
        var father = PupilContact.Create(Guid.CreateVersion7(), Guid.CreateVersion7(), ContactRole.Father);
        father.Apply(name, null, phone, null, null, email, isPrimary: true).Error.Code.ShouldBe(code);
    }

    [Fact]
    public void Contact_AGuardianNeedsARelationship_AndAnEmergencyContactCannotBePrimary()
    {
        var guardian = PupilContact.Create(Guid.CreateVersion7(), Guid.CreateVersion7(), ContactRole.Guardian);
        guardian.Apply("Ada Eze", null, "08031234567", null, null, null, isPrimary: true).Error.Code.ShouldBe("contact.relationship_required");

        var emergency = PupilContact.Create(Guid.CreateVersion7(), Guid.CreateVersion7(), ContactRole.EmergencyAlternate);
        emergency.Apply("Ada Eze", "Neighbour", "08031234567", null, null, null, isPrimary: true).Error.Code.ShouldBe("contact.primary_not_adult");
    }

    [Fact]
    public void Health_IsAnsweredOnlyWhenAllThreeQuestionsAre_AndADetailFollowsItsAnswer()
    {
        var health = PupilHealth.Create(Guid.CreateVersion7());
        health.IsAnswered.ShouldBeFalse();

        health.Apply(true, null, false, null, false, null, null, null, null, null, null).Error.Code.ShouldBe("health.allergy_details_required");

        health.Apply(true, "Peanuts", false, null, null, null, null, null, null, null, null).IsSuccess.ShouldBeTrue();
        health.IsAnswered.ShouldBeFalse();

        health.Apply(false, "Peanuts", false, null, false, null, null, null, "0803777666", BloodGroup.OPositive, Genotype.AA).Error.Code
            .ShouldBe("health.hospital_phone_invalid");
        health.Apply(false, "Peanuts", false, null, false, null, null, null, "08037776666", BloodGroup.OPositive, Genotype.AA).IsSuccess.ShouldBeTrue();
        health.IsAnswered.ShouldBeTrue();
        health.AllergyDetails.ShouldBeNull(); // A stale note does not survive a changed answer.
    }

    [Fact]
    public void Document_TickingDefaultsTheDateToToday_AndUntickingClearsIt()
    {
        var today = new DateOnly(2026, 9, 23);
        var document = PupilDocument.Create(Guid.CreateVersion7(), Guid.CreateVersion7(), PupilDocumentType.BirthCertificate);

        document.Apply(received: true, null, "Photocopy", null, today, "admin-1").IsSuccess.ShouldBeTrue();
        document.ReceivedDate.ShouldBe(today);
        document.ReceivedBy.ShouldBe("admin-1");

        document.Apply(received: false, null, null, null, today, "admin-2").IsSuccess.ShouldBeTrue();
        document.ReceivedDate.ShouldBeNull();
        document.ReceivedBy.ShouldBeNull();

        var other = PupilDocument.Create(Guid.CreateVersion7(), Guid.CreateVersion7(), PupilDocumentType.Other);
        other.Apply(received: true, null, null, null, today, "admin-1").Error.Code.ShouldBe("document.other_label_required");
        other.Apply(received: true, today.AddDays(1), null, "Baptism card", today, "admin-1").Error.Code.ShouldBe("document.received_in_future");
    }

    [Fact]
    public void Document_AttachingAScan_TicksTheRow_AndWhileAttachedItCannotBeUntickedOrLoseTheOtherLabel()
    {
        var today = new DateOnly(2026, 9, 26);
        var now = new DateTimeOffset(2026, 9, 26, 9, 0, 0, TimeSpan.Zero);
        var document = PupilDocument.Create(Guid.CreateVersion7(), Guid.CreateVersion7(), PupilDocumentType.BirthCertificate);

        document.AttachFile("asset-1", "application/pdf", 1024, now, today, "admin-1").IsSuccess.ShouldBeTrue();
        document.Received.ShouldBeTrue();
        document.ReceivedDate.ShouldBe(today);
        document.Apply(received: false, null, null, null, today, "admin-1").Error.Code.ShouldBe("document.file_attached");

        document.RemoveFile().IsSuccess.ShouldBeTrue();
        document.Received.ShouldBeTrue();
        document.RemoveFile().Error.Code.ShouldBe("document.file_not_found");

        var other = PupilDocument.Create(Guid.CreateVersion7(), Guid.CreateVersion7(), PupilDocumentType.Other);
        other.CheckAttach().Error.Code.ShouldBe("document.other_label_required");
        other.Apply(received: true, null, null, "Baptism card", today, "admin-1").IsSuccess.ShouldBeTrue();
        other.AttachFile("asset-2", "image/jpeg", 2048, now, today, "admin-1").IsSuccess.ShouldBeTrue();
        other.Apply(received: true, null, null, null, today, "admin-1").Error.Code.ShouldBe("document.other_label_required");
    }
}
