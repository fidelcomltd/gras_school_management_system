namespace SchoolManagement.Application.Abstractions.Pupils;

/// <summary>
/// Renders the admission slip (spec 6.5.11, 9.x print rules): half an A4, printed one to a page with the lower half blank,
/// since the school files them. Implemented in Infrastructure over QuestPDF.
/// </summary>
public interface IAdmissionSlipRenderer
{
    /// <summary>One A4 page whose upper half is the slip.</summary>
    byte[] Render(AdmissionSlipDocument slip);
}

/// <summary>What the slip prints.</summary>
/// <param name="SchoolName">The school, for the heading.</param>
/// <param name="SchoolAddress">Under the name.</param>
/// <param name="PupilName">Surname first.</param>
/// <param name="RegistrationNumber">Printed large: the office is asked for it at once.</param>
/// <param name="Sex">Male or Female.</param>
/// <param name="DateOfBirth">The pupil's date of birth.</param>
/// <param name="ClassName">The arm the pupil was admitted into, e.g. "Primary 2 Gold".</param>
/// <param name="SessionName">The session admitted into.</param>
/// <param name="DateAdmitted">Section A's admission date.</param>
/// <param name="PrintedAt">When it was printed (every printed artefact carries it).</param>
/// <param name="PrintedBy">The account that printed it.</param>
/// <param name="SchoolMotto">Under the name, when the school has one.</param>
/// <param name="SchoolContact">The school's phone and email, one line.</param>
/// <param name="HeadTeacherName">Under the signature line.</param>
/// <param name="Logo">The current logo (the 200 pixel rendition), or empty for none.</param>
/// <param name="Signature">The head teacher's current signature (strokes only, transparent), or empty for none.</param>
public sealed record AdmissionSlipDocument(
    string SchoolName,
    string SchoolAddress,
    string PupilName,
    string RegistrationNumber,
    string Sex,
    DateOnly DateOfBirth,
    string ClassName,
    string SessionName,
    DateOnly DateAdmitted,
    DateTimeOffset PrintedAt,
    string PrintedBy,
    string? SchoolMotto = null,
    string? SchoolContact = null,
    string? HeadTeacherName = null,
    ReadOnlyMemory<byte> Logo = default,
    ReadOnlyMemory<byte> Signature = default);
