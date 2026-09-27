using Microsoft.Extensions.Options;
using SchoolManagement.Application.Abstractions.Pins;
using SchoolManagement.Infrastructure.Pins;

namespace SchoolManagement.UnitTests.Infrastructure.Pins;

/// <summary>Slips and the distribution list render at the fields' maximum lengths, where a fixed card height could overflow.</summary>
public sealed class QuestPdfPinSlipRendererTests
{
    private static readonly QuestPdfPinSlipRenderer Renderer = new(Options.Create(new PortalOptions { PublicUrl = "results.goldenroyalark.sch.ng" }));

    [Fact]
    public void Slips_TheLongestAllowedNamesStillFitTheCard()
    {
        // A school name of 160 (the fallback when no short name is set), a batch name of 80, a 16-character pin, an empty session.
        var sheet = new PinSlipSheet(
            new string('G', 160), ReadOnlyMemory<byte>.Empty, string.Empty, new string('B', 80),
            [.. Enumerable.Range(0, 7).Select(_ => new PinSlip("MWMW-MWMW-MWMW-MWMW", 3))]);

        Renderer.RenderSlips(sheet).AsSpan(0, 5).SequenceEqual("%PDF-"u8).ShouldBeTrue();
    }

    [Fact]
    public void DistributionList_Renders()
    {
        var sheet = new DistributionSheet(new string('G', 160), "2026/2027", new string('B', 80), ["MWMW", "K7Q2", "H3PA"]);

        Renderer.RenderDistributionList(sheet).AsSpan(0, 5).SequenceEqual("%PDF-"u8).ShouldBeTrue();
    }
}
