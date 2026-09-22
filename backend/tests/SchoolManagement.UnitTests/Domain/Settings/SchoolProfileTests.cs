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

    [Fact]
    public void UpdateAbbreviation_TrimsAndIncrementsOnlyTheAbbreviationVersionNumber()
    {
        var profile = SchoolProfile.CreateForTesting(
            Guid.CreateVersion7(),
            abbreviationVersionNumber: 2,
            identityVersionNumber: 9);

        profile.UpdateAbbreviation("  GRA  ");

        profile.Abbreviation.ShouldBe("GRA");
        profile.AbbreviationVersionNumber.ShouldBe(3);
        profile.IdentityVersionNumber.ShouldBe(9); // Untouched — independent pointers.
    }

    [Fact]
    public void UpdateAbbreviation_AllowsAValueAlreadyUsedHistorically()
    {
        // Spec 6.2.11: "Allowed. Abbreviations are not unique over time..." — this domain method has
        // no uniqueness check to bypass; proven by simply reusing the same value twice in a row.
        var profile = SchoolProfile.CreateForTesting(Guid.CreateVersion7(), abbreviation: "GRAS");

        profile.UpdateAbbreviation("GRA");
        profile.UpdateAbbreviation("GRAS"); // Back to the original value.

        profile.Abbreviation.ShouldBe("GRAS");
        profile.AbbreviationVersionNumber.ShouldBe(2);
    }

    [Fact]
    public void UpdateRegNumber_SetsAllThreeFieldsAndIncrementsOnlyItsOwnVersionNumber()
    {
        var profile = SchoolProfile.CreateForTesting(
            Guid.CreateVersion7(),
            regNumberVersionNumber: 1,
            abbreviationVersionNumber: 5);

        profile.UpdateRegNumber("-", 6, RegNumberSerialReset.Continuous);

        profile.Separator.ShouldBe("-");
        profile.SerialWidth.ShouldBe(6);
        profile.SerialReset.ShouldBe(RegNumberSerialReset.Continuous);
        profile.RegNumberVersionNumber.ShouldBe(2);
        profile.AbbreviationVersionNumber.ShouldBe(5); // Untouched — independent pointers.
    }

    [Fact]
    public void SetCurrentLogo_WithAGroupId_SetsThePointer()
    {
        var profile = SchoolProfile.CreateForTesting(Guid.CreateVersion7());
        var groupId = Guid.CreateVersion7();

        profile.SetCurrentLogo(groupId);

        profile.CurrentLogoGroupId.ShouldBe(groupId);
    }

    [Fact]
    public void SetCurrentLogo_WithNull_RefusesWithAmendment4sVerbatimMessage()
    {
        // Amendment 4 (decisions/2026-Q3-contract-deltas.md): there is no delete route (6.2.12 lists
        // none, 6.2.2 confirms the logo is not seeded), so "removing the current logo with no
        // replacement" is refused HERE, at the domain level, with 6.2.11's exact wording — proven
        // directly, since no route exists to prove it through.
        var profile = SchoolProfile.CreateForTesting(Guid.CreateVersion7());
        profile.SetCurrentLogo(Guid.CreateVersion7());

        var exception = Should.Throw<InvalidOperationException>(() => profile.SetCurrentLogo(null));

        exception.Message.ShouldBe("A logo is required. Upload a replacement before removing the current one.");
        exception.Message.ShouldBe(SchoolProfile.LogoRequiredMessage);
    }

    [Fact]
    public void SetCurrentLogo_WithNull_NeverChangesTheExistingPointer()
    {
        var profile = SchoolProfile.CreateForTesting(Guid.CreateVersion7());
        var groupId = Guid.CreateVersion7();
        profile.SetCurrentLogo(groupId);

        Should.Throw<InvalidOperationException>(() => profile.SetCurrentLogo(null));

        profile.CurrentLogoGroupId.ShouldBe(groupId);
    }

    [Fact]
    public void SetCurrentSignature_WithNull_IsAllowed()
    {
        // Unlike the logo, Amendment 4 names only the logo (spec 6.2.11); a missing signature is a
        // PUBLICATION precondition (out of this card's scope), not a save-time refusal.
        var profile = SchoolProfile.CreateForTesting(Guid.CreateVersion7());
        profile.SetCurrentSignature(Guid.CreateVersion7());

        Should.NotThrow(() => profile.SetCurrentSignature(null));

        profile.CurrentSignatureGroupId.ShouldBeNull();
    }
}
