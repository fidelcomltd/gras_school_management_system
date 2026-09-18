using SchoolManagement.Domain.Settings;

namespace SchoolManagement.UnitTests.Domain.Settings;

/// <summary>Tests <see cref="RegNumberFormat"/> (spec 6.2.4, 6.5.10).</summary>
public sealed class RegNumberFormatTests
{
    [Theory]
    [InlineData("/", true)]
    [InlineData("-", true)]
    [InlineData(".", true)]
    [InlineData(",", false)]
    [InlineData("//", false)]
    [InlineData("", false)]
    [InlineData(null, false)]
    public void IsValidSeparator_MatchesSpec624sThreeAllowedCharactersOnly(string? separator, bool expected)
    {
        RegNumberFormat.IsValidSeparator(separator).ShouldBe(expected);
    }

    [Fact]
    public void Compose_MatchesSpec624sWorkedExample()
    {
        // Spec 6.2.4: "With GRAS, /, width 4 and thirty-nine pupils already admitted in 2026, the
        // preview reads GRAS/2026/0040."
        RegNumberFormat.Compose("GRAS", "/", 2026, 4, 40).ShouldBe("GRAS/2026/0040");
    }

    [Fact]
    public void Compose_WideningTheWidthReflowsThePadding()
    {
        // Spec 6.2.4: "Changing the width to 5 updates the preview to GRAS/2026/00040."
        RegNumberFormat.Compose("GRAS", "/", 2026, 5, 40).ShouldBe("GRAS/2026/00040");
    }

    [Fact]
    public void Compose_WithAnEmptyRegister_TheFirstSerialIsOne()
    {
        RegNumberFormat.Compose("GRAS", "/", 2026, 4, 1).ShouldBe("GRAS/2026/0001");
    }

    [Theory]
    [InlineData(1, 1)]
    [InlineData(9, 1)]
    [InlineData(10, 2)]
    [InlineData(999, 3)]
    [InlineData(1043, 4)]
    [InlineData(9999, 4)]
    public void DigitCount_ReturnsTheUnpaddedDigitCount(int serial, int expected)
    {
        RegNumberFormat.DigitCount(serial).ShouldBe(expected);
    }

    // TASK-0063: the correction path's shape check on a TYPED number (spec 6.5.10) — never the
    // composer above, which needs saved settings this validator does not.
    [Theory]
    [InlineData("GRAS/2026/0041", true)]
    [InlineData("GRAS-2026-0041", true)]
    [InlineData("GRAS.2026.0041", true)]
    [InlineData("ABC/2026/1", true)]
    [InlineData("", false)]
    [InlineData(" ", false)]
    [InlineData("GRAS20260041", false)]
    [InlineData("GRAS/26/0041", false)]
    [InlineData("GRAS/2026/", false)]
    [InlineData("/2026/0041", false)]
    [InlineData("GRAS/2026/0041a", false)]
    public void IsWellFormed_MatchesTheGeneralAbbreviationSeparatorYearSeparatorSerialShape(string candidate, bool expected)
    {
        RegNumberFormat.IsWellFormed(candidate).ShouldBe(expected);
    }
}
