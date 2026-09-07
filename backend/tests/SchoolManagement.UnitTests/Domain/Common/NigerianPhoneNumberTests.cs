using SchoolManagement.Domain.Common;

namespace SchoolManagement.UnitTests.Domain.Common;

/// <summary>Tests <see cref="NigerianPhoneNumber"/> (spec 6.1.3, reused by 6.2.3).</summary>
public sealed class NigerianPhoneNumberTests
{
    [Theory]
    [InlineData("08012345678", "+2348012345678")]
    [InlineData("+2348012345678", "+2348012345678")]
    [InlineData("  08012345678  ", "+2348012345678")]
    public void TryNormalize_AcceptsBothForms(string raw, string expected)
    {
        var accepted = NigerianPhoneNumber.TryNormalize(raw, out var normalized);

        accepted.ShouldBeTrue();
        normalized.ShouldBe(expected);
    }

    [Theory]
    [InlineData("0801234567")] // 10 digits in national form — one short of the required 11.
    [InlineData("080123456789")] // 12 digits — one over.
    [InlineData("+234801234567")] // 9 digits after +234 — one short.
    [InlineData("+23480123456789")] // 11 digits after +234 — one over.
    [InlineData("2348012345678")] // Missing the leading +.
    [InlineData("+1234567890123")] // Wrong country code.
    [InlineData("")]
    [InlineData("not-a-phone-number")]
    public void TryNormalize_RejectsAnythingOutsideTheTwoAcceptedForms(string raw)
    {
        var accepted = NigerianPhoneNumber.TryNormalize(raw, out var normalized);

        accepted.ShouldBeFalse();
        normalized.ShouldBe(string.Empty);
    }
}
