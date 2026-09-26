using SchoolManagement.Domain.Pupils;

namespace SchoolManagement.Application.Pupils.Records;

/// <summary>One contact (spec 6.5.5).</summary>
/// <param name="Id">The contact.</param>
/// <param name="Role">Which of the five slots.</param>
/// <param name="FullName">Two words minimum.</param>
/// <param name="Relationship">Guardian and emergency roles only.</param>
/// <param name="Phone">Canonical <c>+234</c> form.</param>
/// <param name="WhatsappNumber">Father and mother only.</param>
/// <param name="Occupation">Father and mother only.</param>
/// <param name="Email">Optional.</param>
/// <param name="IsPrimaryContact">The person the school telephones first.</param>
public sealed record PupilContactDto(
    string Id, ContactRole Role, string FullName, string? Relationship, string Phone, string? WhatsappNumber, string? Occupation, string? Email,
    bool IsPrimaryContact);

/// <summary>A pupil's contacts, in role order. At most five.</summary>
/// <param name="PupilId">The pupil.</param>
/// <param name="Items">Father, mother, guardian, then the two emergency contacts, as recorded.</param>
public sealed record PupilContactListDto(string PupilId, IReadOnlyList<PupilContactDto> Items);

/// <summary>One contact as submitted. Phones may be typed in either Nigerian form.</summary>
/// <param name="Role">Which slot; each at most once.</param>
/// <param name="FullName">Two words minimum.</param>
/// <param name="Relationship">Required for guardian and the emergency roles; ignored for father and mother.</param>
/// <param name="Phone">Required on every contact.</param>
/// <param name="WhatsappNumber">Father and mother only.</param>
/// <param name="Occupation">Father and mother only.</param>
/// <param name="Email">Optional.</param>
/// <param name="IsPrimaryContact">Exactly one, and a father, mother or guardian, whenever one exists.</param>
public sealed record PupilContactInput(
    ContactRole Role, string FullName, string? Relationship, string Phone, string? WhatsappNumber, string? Occupation, string? Email,
    bool IsPrimaryContact);

/// <summary>One authorised pickup person (spec 6.5.6).</summary>
/// <param name="Id">The row.</param>
/// <param name="FullName">Two words minimum.</param>
/// <param name="Relationship">Free text.</param>
/// <param name="Phone">Canonical <c>+234</c> form.</param>
public sealed record PickupPersonDto(string Id, string FullName, string Relationship, string Phone);

/// <summary>The authorised pickup list, in the parent's order. Empty is allowed.</summary>
/// <param name="PupilId">The pupil.</param>
/// <param name="Items">In order.</param>
public sealed record PickupPersonListDto(string PupilId, IReadOnlyList<PickupPersonDto> Items);

/// <summary>One pickup person as submitted.</summary>
/// <param name="FullName">Two words minimum.</param>
/// <param name="Relationship">Free text.</param>
/// <param name="Phone">Nigerian format.</param>
public sealed record PickupPersonInput(string FullName, string Relationship, string Phone);

/// <summary>One person barred from collecting the child.</summary>
/// <param name="Id">The row.</param>
/// <param name="FullName">Required.</param>
/// <param name="Details">Relevant information.</param>
public sealed record BarredPersonDto(string Id, string FullName, string? Details);

/// <summary>Section E's exclusion question and its answer (spec 6.5.6). Safeguarding data: every read is audited.</summary>
/// <param name="PupilId">The pupil.</param>
/// <param name="HasBarredPersons">Null when never asked, which is not the same as No.</param>
/// <param name="Items">Present only when the answer is yes.</param>
public sealed record BarredPersonsDto(string PupilId, bool? HasBarredPersons, IReadOnlyList<BarredPersonDto> Items);

/// <summary>One barred person as submitted.</summary>
/// <param name="FullName">Required.</param>
/// <param name="Details">Optional, at most 500 characters.</param>
public sealed record BarredPersonInput(string FullName, string? Details);

/// <summary>Section F (spec 6.5.7). Null answers mean "not asked yet". Safeguarding data: every read is audited.</summary>
/// <param name="PupilId">The pupil.</param>
/// <param name="HasAllergy">Explicit yes or no, or null.</param>
/// <param name="AllergyDetails">Present where yes.</param>
/// <param name="HasMedicalCondition">Explicit yes or no, or null.</param>
/// <param name="MedicalConditionDetails">Present where yes.</param>
/// <param name="TakesRegularMedication">Explicit yes or no, or null.</param>
/// <param name="MedicationDetails">Present where yes.</param>
/// <param name="SpecialInstructions">Diet, handling and anything else.</param>
/// <param name="PreferredHospital">Prompted, not required.</param>
/// <param name="HospitalPhone">Canonical <c>+234</c> form.</param>
/// <param name="BloodGroup">Optional.</param>
/// <param name="Genotype">Optional.</param>
public sealed record PupilHealthDto(
    string PupilId, bool? HasAllergy, string? AllergyDetails, bool? HasMedicalCondition, string? MedicalConditionDetails, bool? TakesRegularMedication,
    string? MedicationDetails, string? SpecialInstructions, string? PreferredHospital, string? HospitalPhone, BloodGroup? BloodGroup, Genotype? Genotype);

/// <summary>One checklist row (spec 6.5.8). A type never ticked reads as not received.</summary>
/// <param name="DocumentType">Which document.</param>
/// <param name="OtherLabel">For the "Other" row.</param>
/// <param name="Received">The checkbox.</param>
/// <param name="ReceivedDate">When it was received.</param>
/// <param name="Remarks">The Remarks column.</param>
/// <param name="File">The attached scan, or null; the bytes come from <c>GET /pupils/{id}/documents/{type}/file</c>.</param>
public sealed record PupilDocumentDto(
    PupilDocumentType DocumentType, string? OtherLabel, bool Received, DateOnly? ReceivedDate, string? Remarks, PupilDocumentFileDto? File);

/// <summary>An attached document scan's metadata (spec 6.5.8).</summary>
/// <param name="ContentType"><c>application/pdf</c>, <c>image/jpeg</c> or <c>image/png</c>.</param>
/// <param name="SizeBytes">The stored file's size.</param>
/// <param name="UploadedAtUtc">When it was attached.</param>
public sealed record PupilDocumentFileDto(string ContentType, int SizeBytes, DateTimeOffset UploadedAtUtc);

/// <summary>A pupil's current photograph (spec 6.5.4): where to read each size, through the privilege-checked endpoints.</summary>
/// <param name="PupilId">The pupil.</param>
/// <param name="UpdatedAtUtc">When it was uploaded; matches <c>PupilDto.photoUpdatedAtUtc</c>.</param>
/// <param name="PhotoUrl">The 400 by 400 JPEG.</param>
/// <param name="ThumbnailUrl">The 96 pixel JPEG.</param>
public sealed record PupilPhotoDto(string PupilId, DateTimeOffset UpdatedAtUtc, string PhotoUrl, string ThumbnailUrl);

/// <summary>The whole document checklist, always five rows in form order.</summary>
/// <param name="PupilId">The pupil.</param>
/// <param name="Items">Birth certificate, passport photograph, previous school result, transfer letter, other.</param>
public sealed record PupilDocumentListDto(string PupilId, IReadOnlyList<PupilDocumentDto> Items);

/// <summary>The body of the document route: one row's state.</summary>
/// <param name="Received">The checkbox.</param>
/// <param name="ReceivedDate">Defaults to today when ticked.</param>
/// <param name="Remarks">Optional, at most 200 characters.</param>
/// <param name="OtherLabel">Required when ticking the "Other" row.</param>
public sealed record PupilDocumentInput(bool Received, DateOnly? ReceivedDate, string? Remarks, string? OtherLabel);

/// <summary>One missing item on an admission (spec 6.5.12), keyed to the step that fixes it.</summary>
/// <param name="Step">The admission-flow step, 1 to 9 (spec 6.5.11).</param>
/// <param name="Code">Stable machine code.</param>
/// <param name="Message">What is missing, in the office's words.</param>
public sealed record CompletenessItemDto(int Step, string Code, string Message);

/// <summary>
/// What an admission still lacks (spec 6.5.12): blocking items stop approval; chased items are tracked after the pupil is
/// active. The percentage is across the chased set only.
/// </summary>
/// <param name="PupilId">The pupil.</param>
/// <param name="Blocking">Required for approval.</param>
/// <param name="Chased">Tracked, never blocking.</param>
/// <param name="ChasedPercent">0 to 100: how much of the chased set is present.</param>
public sealed record AdmissionCompletenessDto(string PupilId, IReadOnlyList<CompletenessItemDto> Blocking, IReadOnlyList<CompletenessItemDto> Chased, int ChasedPercent);
