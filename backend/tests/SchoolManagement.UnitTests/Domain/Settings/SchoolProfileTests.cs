using SchoolManagement.Domain.Settings;

namespace SchoolManagement.UnitTests.Domain.Settings;

/// <summary>Tests <see cref="SchoolProfile.UpdateIdentity"/> (spec 6.2.3).</summary>
public sealed class SchoolProfileTests
{
    [Fact]
    public void UpdateIdentity_TrimsFieldsAndLowercasesEmail()
    {
        var profile = SchoolProfile.CreateForTesting(Guid.CreateVersion7());

        profile.UpdateIdentity(
            "  Golden Royal Ark School  ",
            "  GRAS  ",
            "  12 Ark Crescent  ",
            "08012345678",
            "  Info@GoldenRoyalArk.Example  ",
            "  Excellence Through Character  ",
            "  Chisom Maxwell  ");

        profile.SchoolName.ShouldBe("Golden Royal Ark School");
        profile.ShortName.ShouldBe("GRAS");
        profile.Address.ShouldBe("12 Ark Crescent");
        profile.Email.ShouldBe("info@goldenroyalark.example");
        profile.Motto.ShouldBe("Excellence Through Character");
        profile.HeadTeacherName.ShouldBe("Chisom Maxwell");
    }

    [Fact]
    public void UpdateIdentity_NormalizesThePhoneToPlus234Form()
    {
        var profile = SchoolProfile.CreateForTesting(Guid.CreateVersion7());

        profile.UpdateIdentity("Name", "Short", "Address", "08012345678", "a@b.com", null, "Head Teacher");

        profile.Phone.ShouldBe("+2348012345678");
    }

    [Fact]
    public void UpdateIdentity_BlankMottoBecomesNull()
    {
        var profile = SchoolProfile.CreateForTesting(Guid.CreateVersion7());

        profile.UpdateIdentity("Name", "Short", "Address", "08012345678", "a@b.com", "   ", "Head Teacher");

        profile.Motto.ShouldBeNull();
    }

    [Fact]
    public void UpdateIdentity_IncrementsTheIdentityVersionNumberByExactlyOne()
    {
        var profile = SchoolProfile.CreateForTesting(Guid.CreateVersion7(), identityVersionNumber: 4);

        profile.UpdateIdentity("Name", "Short", "Address", "08012345678", "a@b.com", null, "Head Teacher");

        profile.IdentityVersionNumber.ShouldBe(5);
    }

    [Fact]
    public void UpdateIdentity_NeverTouchesAbbreviationOrTimezoneOrTheirVersionPointer()
    {
        var profile = SchoolProfile.CreateForTesting(
            Guid.CreateVersion7(),
            abbreviation: "GRAS",
            timezone: SchoolProfile.FixedTimezone,
            abbreviationVersionNumber: 7);

        profile.UpdateIdentity("Name", "Short", "Address", "08012345678", "a@b.com", null, "Head Teacher");

        // The command has no fields for these — this test proves the domain method itself never
        // reaches for them, which is the second half of "a PATCH cannot change the timezone": the
        // first half is that the wire command has no such property to bind at all.
        profile.Abbreviation.ShouldBe("GRAS");
        profile.Timezone.ShouldBe(SchoolProfile.FixedTimezone);
        profile.AbbreviationVersionNumber.ShouldBe(7);
    }
}
