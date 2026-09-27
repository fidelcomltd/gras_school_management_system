namespace SchoolManagement.Application.Abstractions.Pupils;

/// <summary>
/// Renders the class safeguarding sheet (spec 15 section 10.2) as a printable PDF: the one export in the product that carries
/// health data, printed before an excursion and kept at the gate. Implemented in Infrastructure over QuestPDF.
/// </summary>
public interface ISafeguardingSheetRenderer
{
    /// <summary>An A4 landscape table, one row per pupil, with the photograph where there is one.</summary>
    byte[] Render(SafeguardingSheetDocument sheet);
}

/// <summary>What the PDF prints.</summary>
/// <param name="SchoolName">The school, for the heading.</param>
/// <param name="ArmName">The class, e.g. "Primary 2 Gold".</param>
/// <param name="SessionName">The session the roll belongs to.</param>
/// <param name="GeneratedAtLagos">When it was generated, Lagos time, printed so a stale sheet at the gate is recognisable.</param>
/// <param name="Rows">One per pupil, by surname.</param>
public sealed record SafeguardingSheetDocument(
    string SchoolName, string ArmName, string SessionName, DateTime GeneratedAtLagos, IReadOnlyList<SafeguardingSheetRow> Rows);

/// <summary>One pupil's line on the sheet.</summary>
/// <param name="Name">Surname first.</param>
/// <param name="Photo">The 96 pixel JPEG, or null when there is no photograph. ReadOnlyMemory, not byte[], for CA1819.</param>
/// <param name="Allergies">"None", "Not asked", or the detail.</param>
/// <param name="MedicalConditions">"None", "Not asked", or the detail.</param>
/// <param name="Medication">"None", "Not asked", or the detail.</param>
/// <param name="SpecialInstructions">Free text, or empty.</param>
/// <param name="Hospital">Preferred hospital and its phone, or empty.</param>
/// <param name="PickupPersons">"Name (relationship) phone", in the parent's order.</param>
/// <param name="BarredMarker">A marker only, never names: "Yes: see office", "No" or "Not asked".</param>
public sealed record SafeguardingSheetRow(
    string Name,
    ReadOnlyMemory<byte>? Photo,
    string Allergies,
    string MedicalConditions,
    string Medication,
    string SpecialInstructions,
    string Hospital,
    IReadOnlyList<string> PickupPersons,
    string BarredMarker);
