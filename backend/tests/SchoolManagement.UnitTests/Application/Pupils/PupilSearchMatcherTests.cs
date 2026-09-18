using SchoolManagement.Application.Pupils;

namespace SchoolManagement.UnitTests.Application.Pupils;

/// <summary>Spec 6.5.15: "Results show which field matched."</summary>
public sealed class PupilSearchMatcherTests
{
    [Fact]
    public void Resolve_WithNoSearchTerm_ReturnsNull() =>
        PupilSearchMatcher.Resolve("Okafor", "Chidera", null, null, null).ShouldBeNull();

    [Fact]
    public void Resolve_MatchingSurname_ReturnsSurname() =>
        PupilSearchMatcher.Resolve("Okafor", "Chidera", null, null, "kaf").ShouldBe("Surname");

    [Fact]
    public void Resolve_MatchingFirstNameOnly_ReturnsFirstName() =>
        PupilSearchMatcher.Resolve("Okafor", "Chidera", null, null, "ider").ShouldBe("FirstName");

    [Fact]
    public void Resolve_MatchingMiddleNameOnly_ReturnsMiddleName() =>
        PupilSearchMatcher.Resolve("Okafor", "Chidera", "Ngozi", null, "ngoz").ShouldBe("MiddleName");

    // The card's own named example: typing "41" finds "GRAS/2026/0041" via ordinary substring
    // matching — no separate serial-extraction step is needed.
    [Fact]
    public void Resolve_WithTheSerialAlone_MatchesTheFullRegistrationNumber() =>
        PupilSearchMatcher.Resolve("Okafor", "Chidera", null, "GRAS/2026/0041", "41").ShouldBe("RegistrationNumber");

    [Fact]
    public void Resolve_IsCaseInsensitive() =>
        PupilSearchMatcher.Resolve("Okafor", "Chidera", null, null, "OKAFOR").ShouldBe("Surname");

    [Fact]
    public void Resolve_WithNoMatch_ReturnsNull() =>
        PupilSearchMatcher.Resolve("Okafor", "Chidera", null, "GRAS/2026/0041", "zzz").ShouldBeNull();

    [Fact]
    public void Resolve_SurnameTakesPrecedenceOverFirstName_WhenBothMatch() =>
        PupilSearchMatcher.Resolve("Ade", "Adewale", null, null, "ade").ShouldBe("Surname");
}
