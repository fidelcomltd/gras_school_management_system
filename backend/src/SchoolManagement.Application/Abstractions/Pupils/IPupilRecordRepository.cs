using SchoolManagement.Domain.Admissions;
using SchoolManagement.Domain.Pupils;

namespace SchoolManagement.Application.Abstractions.Pupils;

/// <summary>
/// Persistence port for the admission form's per-pupil sections (spec 6.5.5 to 6.5.8): contacts, the pickup and barred
/// lists, health, and the document checklist. Every list is small (at most a handful of rows), so each is read whole.
/// </summary>
public interface IPupilRecordRepository
{
    /// <summary>The pupil's contacts. Tracked when <paramref name="track"/>.</summary>
    Task<IReadOnlyList<PupilContact>> ListContactsAsync(Guid pupilId, bool track, CancellationToken cancellationToken);

    /// <summary>The authorised pickup list, in the parent's order.</summary>
    Task<IReadOnlyList<AuthorisedPickupPerson>> ListPickupPersonsAsync(Guid pupilId, bool track, CancellationToken cancellationToken);

    /// <summary>The barred-persons answer, or null when never asked.</summary>
    Task<BarredPersonAnswer?> FindBarredAnswerAsync(Guid pupilId, bool track, CancellationToken cancellationToken);

    /// <summary>The barred persons, in entry order.</summary>
    Task<IReadOnlyList<BarredPerson>> ListBarredPersonsAsync(Guid pupilId, bool track, CancellationToken cancellationToken);

    /// <summary>The health row, or null when never saved.</summary>
    Task<PupilHealth?> FindHealthAsync(Guid pupilId, bool track, CancellationToken cancellationToken);

    /// <summary>The checklist rows that exist.</summary>
    Task<IReadOnlyList<PupilDocument>> ListDocumentsAsync(Guid pupilId, bool track, CancellationToken cancellationToken);

    /// <summary>
    /// Every sub-record completeness reads, for many pupils at once and read-only: the incomplete-records report
    /// (spec 6.5.12) in a fixed number of queries rather than several per pupil.
    /// </summary>
    Task<PupilRecordSet> LoadForPupilsAsync(IReadOnlyCollection<Guid> pupilIds, CancellationToken cancellationToken);

    /// <summary>Stages a new row of any of the entities above. Does NOT commit.</summary>
    Task AddAsync(object entity, CancellationToken cancellationToken);

    /// <summary>Stages a delete. Does NOT commit.</summary>
    Task RemoveAsync(object entity, CancellationToken cancellationToken);
}

/// <summary>Read-only sub-records for a set of pupils, keyed by pupil id.</summary>
/// <param name="Contacts">Each pupil's contacts.</param>
/// <param name="Barred">Each pupil's barred-persons answer, where one was given.</param>
/// <param name="Health">Each pupil's health row, where one was saved.</param>
/// <param name="Pickup">Each pupil's authorised pickup persons.</param>
/// <param name="Documents">Each pupil's checklist rows.</param>
/// <param name="Admissions">Each pupil's admission record.</param>
public sealed record PupilRecordSet(
    ILookup<Guid, PupilContact> Contacts,
    IReadOnlyDictionary<Guid, BarredPersonAnswer> Barred,
    IReadOnlyDictionary<Guid, PupilHealth> Health,
    ILookup<Guid, AuthorisedPickupPerson> Pickup,
    ILookup<Guid, PupilDocument> Documents,
    IReadOnlyDictionary<Guid, AdmissionRecord> Admissions);
