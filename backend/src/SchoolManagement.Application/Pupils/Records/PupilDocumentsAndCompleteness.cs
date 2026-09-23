using FluentValidation;
using SchoolManagement.Application.Abstractions.Admissions;
using SchoolManagement.Application.Abstractions.Audit;
using SchoolManagement.Application.Abstractions.Identity;
using SchoolManagement.Application.Abstractions.Messaging;
using SchoolManagement.Application.Abstractions.Pupils;
using SchoolManagement.Domain.Common;
using SchoolManagement.Domain.Pupils;
using SchoolManagement.Domain.Security;

namespace SchoolManagement.Application.Pupils.Records;

/// <summary><c>GET /api/v1/pupils/{pupilId}/documents</c> (spec 6.5.8): always the five checklist rows.</summary>
/// <param name="PupilId">From the route.</param>
public sealed record GetPupilDocumentsQuery(Guid PupilId) : IQuery<Result<PupilDocumentListDto>>;

/// <summary>Nothing structural to check beyond the route.</summary>
internal sealed class GetPupilDocumentsQueryValidator : AbstractValidator<GetPupilDocumentsQuery>;

/// <summary>Handles <see cref="GetPupilDocumentsQuery"/>.</summary>
internal sealed class GetPupilDocumentsHandler(PupilRecordAccess access, IPupilRecordRepository records)
    : IRequestHandler<GetPupilDocumentsQuery, Result<PupilDocumentListDto>>
{
    /// <inheritdoc />
    public async Task<Result<PupilDocumentListDto>> HandleAsync(GetPupilDocumentsQuery request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var allowed = await access.CheckAsync(request.PupilId, Privileges.Pupil.View, cancellationToken).ConfigureAwait(false);
        return allowed.IsFailure
            ? Result.Failure<PupilDocumentListDto>(allowed.Error)
            : Result.Success(DocumentMapper.ToDto(request.PupilId, await records.ListDocumentsAsync(request.PupilId, track: false, cancellationToken).ConfigureAwait(false)));
    }
}

/// <summary><c>PUT /api/v1/pupils/{pupilId}/documents/{documentType}</c>: tick or untick one row, with remarks.</summary>
/// <param name="PupilId">From the route.</param>
/// <param name="DocumentType">From the route.</param>
/// <param name="Received">The checkbox.</param>
/// <param name="ReceivedDate">Defaults to today when ticked.</param>
/// <param name="Remarks">Optional.</param>
/// <param name="OtherLabel">Required when ticking the "Other" row.</param>
public sealed record SavePupilDocumentCommand(Guid PupilId, PupilDocumentType DocumentType, bool Received, DateOnly? ReceivedDate, string? Remarks, string? OtherLabel)
    : ICommand<Result<PupilDocumentListDto>>;

/// <summary>Enum member only; the rest is the entity's rule.</summary>
internal sealed class SavePupilDocumentCommandValidator : AbstractValidator<SavePupilDocumentCommand>
{
    public SavePupilDocumentCommandValidator() => RuleFor(command => command.DocumentType).IsInEnum();
}

/// <summary>Handles <see cref="SavePupilDocumentCommand"/>.</summary>
internal sealed class SavePupilDocumentHandler(
    PupilRecordAccess access, IPupilRecordRepository records, ICurrentUser currentUser, ISystemAuditSink auditSink, TimeProvider timeProvider)
    : IRequestHandler<SavePupilDocumentCommand, Result<PupilDocumentListDto>>
{
    /// <inheritdoc />
    public async Task<Result<PupilDocumentListDto>> HandleAsync(SavePupilDocumentCommand request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var allowed = await access.CheckAsync(request.PupilId, Privileges.Pupil.DocumentManage, cancellationToken).ConfigureAwait(false);
        if (allowed.IsFailure)
        {
            return Result.Failure<PupilDocumentListDto>(allowed.Error);
        }

        var documents = (await records.ListDocumentsAsync(request.PupilId, track: true, cancellationToken).ConfigureAwait(false)).ToList();
        var document = documents.FirstOrDefault(candidate => candidate.DocumentType == request.DocumentType);
        var isNew = document is null;
        document ??= PupilDocument.Create(Guid.CreateVersion7(), request.PupilId, request.DocumentType);
        var today = Weekly.WeeklyProjection.LagosToday(timeProvider.GetUtcNow());
        var applied = document.Apply(request.Received, request.ReceivedDate, request.Remarks, request.OtherLabel, today, currentUser.UserId);
        if (applied.IsFailure)
        {
            return Result.Failure<PupilDocumentListDto>(applied.Error);
        }

        if (isNew)
        {
            await records.AddAsync(document, cancellationToken).ConfigureAwait(false);
            documents.Add(document);
        }

        await auditSink.RecordAsync(
            Privileges.Pupil.DocumentManage, "pupil_document", PupilContactsMapper.Id(request.PupilId),
            new Dictionary<string, object?>(StringComparer.Ordinal) { ["documentType"] = request.DocumentType.ToString(), ["received"] = request.Received },
            currentUser.UserId, cancellationToken)
            .ConfigureAwait(false);

        return Result.Success(DocumentMapper.ToDto(request.PupilId, documents));
    }
}

/// <summary><c>GET /api/v1/admissions/{id}/completeness</c> (spec 6.5.12): what is missing, blocking and chased, by step.</summary>
/// <param name="PupilId">From the route.</param>
public sealed record GetAdmissionCompletenessQuery(Guid PupilId) : IQuery<Result<AdmissionCompletenessDto>>;

/// <summary>Nothing structural to check beyond the route.</summary>
internal sealed class GetAdmissionCompletenessQueryValidator : AbstractValidator<GetAdmissionCompletenessQuery>;

/// <summary>Handles <see cref="GetAdmissionCompletenessQuery"/>. Reports whether health is answered, never what it says.</summary>
internal sealed class GetAdmissionCompletenessHandler(PupilRecordAccess access, AdmissionCompleteness completeness)
    : IRequestHandler<GetAdmissionCompletenessQuery, Result<AdmissionCompletenessDto>>
{
    /// <inheritdoc />
    public async Task<Result<AdmissionCompletenessDto>> HandleAsync(GetAdmissionCompletenessQuery request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var allowed = await access.CheckAsync(request.PupilId, Privileges.Pupil.View, cancellationToken).ConfigureAwait(false);
        return allowed.IsFailure
            ? Result.Failure<AdmissionCompletenessDto>(allowed.Error)
            : Result.Success(await completeness.EvaluateAsync(allowed.Value, cancellationToken).ConfigureAwait(false));
    }
}

/// <summary>
/// Spec 6.5.12's two lists. Blocking: one responsible adult, the primary emergency contact, a primary contact, the
/// barred-persons answer, the three health answers, the declaration, and a required assessment's outcome. Chased: previous
/// school, the pickup list, preferred hospital, blood group, genotype, other information, and each checklist document.
/// </summary>
internal sealed class AdmissionCompleteness(IPupilRecordRepository records, IAdmissionRecordRepository admissions)
{
    /// <summary>
    /// Only the blocking items of steps 3 to 5 (contacts, the barred answer, health) — what approval checks on top of its
    /// own declaration and assessment rules, without reading the chased set it would discard.
    /// </summary>
    public async Task<IReadOnlyList<CompletenessItemDto>> BlockingSectionsAsync(Guid pupilId, CancellationToken cancellationToken) =>
        SectionsBlocking(
            await records.ListContactsAsync(pupilId, track: false, cancellationToken).ConfigureAwait(false),
            await records.FindBarredAnswerAsync(pupilId, track: false, cancellationToken).ConfigureAwait(false),
            await records.FindHealthAsync(pupilId, track: false, cancellationToken).ConfigureAwait(false));

    /// <summary>Both lists for <paramref name="pupil"/>.</summary>
    public async Task<AdmissionCompletenessDto> EvaluateAsync(Pupil pupil, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(pupil);
        var contacts = await records.ListContactsAsync(pupil.Id, track: false, cancellationToken).ConfigureAwait(false);
        var barred = await records.FindBarredAnswerAsync(pupil.Id, track: false, cancellationToken).ConfigureAwait(false);
        var health = await records.FindHealthAsync(pupil.Id, track: false, cancellationToken).ConfigureAwait(false);
        var pickup = await records.ListPickupPersonsAsync(pupil.Id, track: false, cancellationToken).ConfigureAwait(false);
        var documents = await records.ListDocumentsAsync(pupil.Id, track: false, cancellationToken).ConfigureAwait(false);
        var record = await admissions.FindReadOnlyByPupilIdAsync(pupil.Id, cancellationToken).ConfigureAwait(false);

        var blocking = SectionsBlocking(contacts, barred, health);
        void Block(bool missing, int step, string code, string message)
        {
            if (missing)
            {
                blocking.Add(new CompletenessItemDto(step, code, message));
            }
        }

        Block(record is not { DeclarationSigned: true }, 8, "declaration.unsigned", "Record the parent's declaration.");
        Block(record is { AssessmentRequired: true } && string.IsNullOrWhiteSpace(record.AssessmentResultRemarks),
            9, "assessment.outcome", "Record the assessment outcome.");

        var chased = new List<CompletenessItemDto>();
        var chasedTotal = 0;
        void Chase(bool missing, int step, string code, string message)
        {
            chasedTotal++;
            if (missing)
            {
                chased.Add(new CompletenessItemDto(step, code, message));
            }
        }

        Chase(pupil.PreviousSchool is null, 2, "pupil.previous_school", "Previous school.");
        Chase(pickup.Count == 0, 4, "collection.pickup", "Authorised pickup persons.");
        Chase(health?.PreferredHospital is null, 5, "health.hospital", "Preferred hospital.");
        Chase(health?.BloodGroup is null, 5, "health.blood_group", "Blood group.");
        Chase(health?.Genotype is null, 5, "health.genotype", "Genotype.");
        Chase(pupil.OtherInformation is null, 6, "pupil.other_information", "Other important information.");
        foreach (var type in Enum.GetValues<PupilDocumentType>().Where(type => type != PupilDocumentType.Other))
        {
            Chase(!documents.Any(document => document.DocumentType == type && document.Received), 7, $"documents.{type}", DocumentMapper.Label(type));
        }

        var percent = chasedTotal == 0 ? 100 : (int)Math.Round(100.0 * (chasedTotal - chased.Count) / chasedTotal);
        return new AdmissionCompletenessDto(PupilContactsMapper.Id(pupil.Id), blocking, chased, percent);
    }

    private static List<CompletenessItemDto> SectionsBlocking(IReadOnlyList<PupilContact> contacts, BarredPersonAnswer? barred, PupilHealth? health)
    {
        var blocking = new List<CompletenessItemDto>();
        void Block(bool missing, int step, string code, string message)
        {
            if (missing)
            {
                blocking.Add(new CompletenessItemDto(step, code, message));
            }
        }

        var hasAdult = contacts.Any(contact => PupilContact.IsResponsibleAdult(contact.Role));
        Block(!hasAdult, 3, "contacts.responsible_adult", "Add the father, mother or guardian.");
        Block(!contacts.Any(contact => contact.Role == ContactRole.EmergencyPrimary), 3, "contacts.emergency_primary", "Add the primary emergency contact.");
        Block(hasAdult && !contacts.Any(contact => contact.IsPrimaryContact), 3, "contacts.primary", "Mark one parent or guardian as the primary contact.");
        Block(barred is null, 4, "collection.barred_unanswered", "Answer whether anyone must not collect the child.");
        Block(health is not { IsAnswered: true }, 5, "health.unanswered", "Answer all three health questions: allergy, medical condition, medication.");
        return blocking;
    }
}

/// <summary>Document mapping: every type appears, in form order.</summary>
internal static class DocumentMapper
{
    public static string Label(PupilDocumentType type) => type switch
    {
        PupilDocumentType.BirthCertificate => "Birth certificate.",
        PupilDocumentType.PassportPhotograph => "Passport photograph.",
        PupilDocumentType.PreviousSchoolResult => "Previous school result.",
        PupilDocumentType.TransferLetter => "Transfer letter.",
        _ => "Other document.",
    };

    public static PupilDocumentListDto ToDto(Guid pupilId, IEnumerable<PupilDocument> documents)
    {
        var byType = documents.ToDictionary(document => document.DocumentType);
        return new PupilDocumentListDto(PupilContactsMapper.Id(pupilId), Enum.GetValues<PupilDocumentType>()
            .Select(type => byType.GetValueOrDefault(type) is { } document
                ? new PupilDocumentDto(type, document.OtherLabel, document.Received, document.ReceivedDate, document.Remarks)
                : new PupilDocumentDto(type, null, false, null, null))
            .ToList());
    }
}
