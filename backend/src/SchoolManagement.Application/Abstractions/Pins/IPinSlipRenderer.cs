namespace SchoolManagement.Application.Abstractions.Pins;

/// <summary>One printed slip: the pin grouped in fives, and how many uses it carries.</summary>
/// <param name="FormattedPin">e.g. <c>H7K2M-QRW4T</c>.</param>
/// <param name="MaxUses">Printed in the use sentence.</param>
public sealed record PinSlip(string FormattedPin, int MaxUses);

/// <summary>Everything a print run shows besides the pins (spec 6.8.9 step 6). No pupil name: pins have no pupil.</summary>
/// <param name="SchoolShortName">Printed at the top of each slip.</param>
/// <param name="Logo">The current logo's 200 px rendition, or empty when none has been uploaded.</param>
/// <param name="SessionName">The session the pins are valid for.</param>
/// <param name="BatchName">Printed small, for the office.</param>
/// <param name="Slips">One per pin.</param>
public sealed record PinSlipSheet(string SchoolShortName, ReadOnlyMemory<byte> Logo, string SessionName, string BatchName, IReadOnlyList<PinSlip> Slips);

/// <summary>The numbered hand-over sheet (spec 6.8.9 step 7): prefixes only, with blank columns filled in by hand.</summary>
/// <param name="SchoolShortName">Heading.</param>
/// <param name="SessionName">Heading.</param>
/// <param name="BatchName">Heading.</param>
/// <param name="Prefixes">Each slip's first four characters, in print order.</param>
public sealed record DistributionSheet(string SchoolShortName, string SessionName, string BatchName, IReadOnlyList<string> Prefixes);

/// <summary>Renders pin slips and the distribution list as PDF. Infrastructure implements it with QuestPDF.</summary>
public interface IPinSlipRenderer
{
    /// <summary>Four slips to an A4 page, with cut lines.</summary>
    byte[] RenderSlips(PinSlipSheet sheet);

    /// <summary>The numbered hand-over sheet.</summary>
    byte[] RenderDistributionList(DistributionSheet sheet);
}
