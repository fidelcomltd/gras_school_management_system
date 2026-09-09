using SchoolManagement.Domain.Pupils;

namespace SchoolManagement.UnitTests.Domain.Pupils;

/// <summary>The closed state/LGA reference lists spec 6.5.4 requires — free text must never validate.</summary>
public sealed class NigerianGeographyTests
{
    [Fact]
    public void States_HasThirtySevenEntries() =>
        NigerianGeography.States.Count.ShouldBe(37); // 36 states + the Federal Capital Territory.

    [Fact]
    public void TryNormalizeState_WithFreeText_Fails()
    {
        var result = NigerianGeography.TryNormalizeState("Not A Real State", out _);
        result.ShouldBeFalse();
    }

    [Theory]
    [InlineData("lagos", "Lagos")]
    [InlineData("LAGOS", "Lagos")]
    [InlineData(" Lagos ", "Lagos")]
    public void TryNormalizeState_IsCaseAndWhitespaceInsensitive(string typed, string expected)
    {
        var result = NigerianGeography.TryNormalizeState(typed, out var canonical);

        result.ShouldBeTrue();
        canonical.ShouldBe(expected);
    }

    [Fact]
    public void TryNormalizeLga_WithAnLgaFromAnotherState_Fails()
    {
        var result = NigerianGeography.TryNormalizeLga("Lagos", "Awka South", out _);
        result.ShouldBeFalse();
    }

    [Fact]
    public void TryNormalizeLga_WithTheCorrectLga_Succeeds()
    {
        var result = NigerianGeography.TryNormalizeLga("Lagos", "ikeja", out var canonical);

        result.ShouldBeTrue();
        canonical.ShouldBe("Ikeja");
    }

    // Every state must have at least one real LGA an admission form can select — a state with none
    // configured would silently make every child from it unable to be registered. Proven by asserting
    // Pupil.Create succeeds for one representative LGA per state, catching a state that was declared
    // in States but never given an entry in the LGA table.
    [Theory]
    [MemberData(nameof(EveryState))]
    public void EveryState_HasAtLeastOneValidLga(string state)
    {
        Pupil.Create(
            Guid.CreateVersion7(), "Test", "Pupil", null, PupilSex.Male, new DateOnly(2020, 1, 1),
            new DateOnly(2026, 1, 1), null, state, RepresentativeLgas[state], "1 Test Street",
            null, null, null).IsSuccess.ShouldBeTrue($"{state} has no valid LGA configured.");
    }

    public static TheoryData<string> EveryState() => new(NigerianGeography.States);

    /// <summary>One real LGA per state, used only to prove <see cref="NigerianGeography"/> has SOME entry for each.</summary>
    private static readonly Dictionary<string, string> RepresentativeLgas = new()
    {
        ["Abia"] = "Umuahia North",
        ["Adamawa"] = "Yola North",
        ["Akwa Ibom"] = "Uyo",
        ["Anambra"] = "Awka South",
        ["Bauchi"] = "Bauchi",
        ["Bayelsa"] = "Yenagoa",
        ["Benue"] = "Makurdi",
        ["Borno"] = "Maiduguri",
        ["Cross River"] = "Calabar Municipal",
        ["Delta"] = "Warri South",
        ["Ebonyi"] = "Abakaliki",
        ["Edo"] = "Oredo",
        ["Ekiti"] = "Ado Ekiti",
        ["Enugu"] = "Enugu North",
        ["Gombe"] = "Gombe",
        ["Imo"] = "Owerri Municipal",
        ["Jigawa"] = "Dutse",
        ["Kaduna"] = "Kaduna North",
        ["Kano"] = "Kano Municipal",
        ["Katsina"] = "Katsina",
        ["Kebbi"] = "Birnin Kebbi",
        ["Kogi"] = "Lokoja",
        ["Kwara"] = "Ilorin West",
        ["Lagos"] = "Ikeja",
        ["Nasarawa"] = "Lafia",
        ["Niger"] = "Chanchaga",
        ["Ogun"] = "Abeokuta South",
        ["Ondo"] = "Akure South",
        ["Osun"] = "Osogbo",
        ["Oyo"] = "Ibadan North",
        ["Plateau"] = "Jos North",
        ["Rivers"] = "Port Harcourt",
        ["Sokoto"] = "Sokoto North",
        ["Taraba"] = "Jalingo",
        ["Yobe"] = "Damaturu",
        ["Zamfara"] = "Gusau",
        ["Federal Capital Territory"] = "Abuja Municipal",
    };
}
