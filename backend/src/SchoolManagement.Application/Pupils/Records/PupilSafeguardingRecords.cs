using FluentValidation;
using SchoolManagement.Application.Abstractions.Audit;
using SchoolManagement.Application.Abstractions.Identity;
using SchoolManagement.Application.Abstractions.Messaging;
using SchoolManagement.Application.Abstractions.Pupils;
using SchoolManagement.Domain.Common;
using SchoolManagement.Domain.Pupils;
using SchoolManagement.Domain.Security;

namespace SchoolManagement.Application.Pupils.Records;

/// <summary><c>GET /api/v1/pupils/{pupilId}/pickup-persons</c> (spec 6.5.6).</summary>
/// <param name="PupilId">From the route.</param>
public sealed record GetPickupPersonsQuery(Guid PupilId) : IQuery<Result<PickupPersonListDto>>;

/// <summary>Nothing structural to check beyond the route.</summary>
internal sealed class GetPickupPersonsQueryValidator : AbstractValidator<GetPickupPersonsQuery>;

/// <summary>Handles <see cref="GetPickupPersonsQuery"/>.</summary>
internal sealed class GetPickupPersonsHandler(PupilRecordAccess access, IPupilRecordRepository records)
    : IRequestHandler<GetPickupPersonsQuery, Result<PickupPersonListDto>>
{
    /// <inheritdoc />
    public async Task<Result<PickupPersonListDto>> HandleAsync(GetPickupPersonsQuery request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var allowed = await access.CheckAsync(request.PupilId, Privileges.Contact.View, cancellationToken).ConfigureAwait(false);
        return allowed.IsFailure
            ? Result.Failure<PickupPersonListDto>(allowed.Error)
            : Result.Success(PickupMapper.ToDto(request.PupilId, await records.ListPickupPersonsAsync(request.PupilId, track: false, cancellationToken).ConfigureAwait(false)));
    }
}

/// <summary><c>PUT /api/v1/pupils/{pupilId}/pickup-persons</c>: the whole list, in the parent's order. Empty is allowed.</summary>
/// <param name="PupilId">From the route.</param>
/// <param name="Persons">In order.</param>
public sealed record SavePickupPersonsCommand(Guid PupilId, IReadOnlyList<PickupPersonInput> Persons) : ICommand<Result<PickupPersonListDto>>;

/// <summary>A sane upper bound; the form's table has three rows.</summary>
internal sealed class SavePickupPersonsCommandValidator : AbstractValidator<SavePickupPersonsCommand>
{
    public SavePickupPersonsCommandValidator() => RuleFor(command => command.Persons).NotNull().Must(persons => persons.Count <= 20);
}

/// <summary>Handles <see cref="SavePickupPersonsCommand"/>. The list is replaced whole.</summary>
internal sealed class SavePickupPersonsHandler(PupilRecordAccess access, IPupilRecordRepository records, ICurrentUser currentUser, ISystemAuditSink auditSink)
    : IRequestHandler<SavePickupPersonsCommand, Result<PickupPersonListDto>>
{
    /// <inheritdoc />
    public async Task<Result<PickupPersonListDto>> HandleAsync(SavePickupPersonsCommand request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var allowed = await access.CheckAsync(request.PupilId, Privileges.Contact.Update, cancellationToken).ConfigureAwait(false);
        if (allowed.IsFailure)
        {
            return Result.Failure<PickupPersonListDto>(allowed.Error);
        }

        var created = new List<AuthorisedPickupPerson>();
        var failures = new Dictionary<string, string[]>(StringComparer.Ordinal);
        for (var index = 0; index < request.Persons.Count; index++)
        {
            var input = request.Persons[index];
            var person = AuthorisedPickupPerson.Create(Guid.CreateVersion7(), request.PupilId, input.FullName, input.Relationship, input.Phone, index);
            if (person.IsFailure)
            {
                failures[$"Persons[{index}]"] = [person.Error.Description];
            }
            else
            {
                created.Add(person.Value);
            }
        }

        if (failures.Count > 0)
        {
            return Result.Failure<PickupPersonListDto>(new ValidationError(failures));
        }

        var existing = await records.ListPickupPersonsAsync(request.PupilId, track: true, cancellationToken).ConfigureAwait(false);
        foreach (var old in existing)
        {
            await records.RemoveAsync(old, cancellationToken).ConfigureAwait(false);
        }

        foreach (var person in created)
        {
            await records.AddAsync(person, cancellationToken).ConfigureAwait(false);
        }

        await auditSink.RecordAsync(
            Privileges.Contact.Update, "authorised_pickup_person", PupilContactsMapper.Id(request.PupilId),
            new Dictionary<string, object?>(StringComparer.Ordinal) { ["names"] = created.Select(person => person.FullName).ToList() },
            currentUser.UserId, cancellationToken,
            beforeMetadata: new Dictionary<string, object?>(StringComparer.Ordinal) { ["names"] = existing.Select(person => person.FullName).ToList() })
            .ConfigureAwait(false);

        return Result.Success(PickupMapper.ToDto(request.PupilId, created));
    }
}

/// <summary>
/// <c>GET /api/v1/pupils/{pupilId}/barred-persons</c> (spec 6.5.6). A command, not a query, because every read writes an
/// audit event and the unit of work must commit it (same reason <c>ExportAuditEventsCommand</c> is one).
/// </summary>
/// <param name="PupilId">From the route.</param>
public sealed record ReadBarredPersonsCommand(Guid PupilId) : ICommand<Result<BarredPersonsDto>>;

/// <summary>Nothing structural to check beyond the route.</summary>
internal sealed class ReadBarredPersonsCommandValidator : AbstractValidator<ReadBarredPersonsCommand>;

/// <summary>Handles <see cref="ReadBarredPersonsCommand"/>.</summary>
internal sealed class ReadBarredPersonsHandler(PupilRecordAccess access, IPupilRecordRepository records, ICurrentUser currentUser, ISystemAuditSink auditSink)
    : IRequestHandler<ReadBarredPersonsCommand, Result<BarredPersonsDto>>
{
    /// <summary>Spec 6.5.6: "every read is audited by actor and timestamp".</summary>
    public const string ReadAction = "pupil.safeguarding.read";

    /// <inheritdoc />
    public async Task<Result<BarredPersonsDto>> HandleAsync(ReadBarredPersonsCommand request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var allowed = await access.CheckAsync(request.PupilId, Privileges.Pupil.SafeguardingView, cancellationToken).ConfigureAwait(false);
        if (allowed.IsFailure)
        {
            return Result.Failure<BarredPersonsDto>(allowed.Error);
        }

        await auditSink.RecordAsync(
            ReadAction, "barred_person", PupilContactsMapper.Id(request.PupilId), metadata: null, currentUser.UserId, cancellationToken)
            .ConfigureAwait(false);
        var answer = await records.FindBarredAnswerAsync(request.PupilId, track: false, cancellationToken).ConfigureAwait(false);
        var persons = await records.ListBarredPersonsAsync(request.PupilId, track: false, cancellationToken).ConfigureAwait(false);
        return Result.Success(BarredMapper.ToDto(request.PupilId, answer?.HasBarredPersons, persons));
    }
}

/// <summary>
/// <c>PUT /api/v1/pupils/{pupilId}/barred-persons</c>: the explicit answer and, when yes, the names. A No clears any
/// names previously recorded; a Yes needs at least one.
/// </summary>
/// <param name="PupilId">From the route.</param>
/// <param name="HasBarredPersons">The explicit answer.</param>
/// <param name="Persons">Required when the answer is yes; must be empty when no.</param>
public sealed record SaveBarredPersonsCommand(Guid PupilId, bool HasBarredPersons, IReadOnlyList<BarredPersonInput> Persons) : ICommand<Result<BarredPersonsDto>>;

/// <summary>A Yes needs a name; a No carries none.</summary>
internal sealed class SaveBarredPersonsCommandValidator : AbstractValidator<SaveBarredPersonsCommand>
{
    public SaveBarredPersonsCommandValidator()
    {
        RuleFor(command => command.Persons).NotNull().Must(persons => persons.Count <= 10);
        RuleFor(command => command.Persons).Must(persons => persons.Count > 0).When(command => command.HasBarredPersons)
            .WithMessage("Name at least one person who must not collect the child.");
        RuleFor(command => command.Persons).Must(persons => persons.Count == 0).When(command => !command.HasBarredPersons)
            .WithMessage("Remove the names, or answer Yes.");
    }
}

/// <summary>Handles <see cref="SaveBarredPersonsCommand"/>.</summary>
internal sealed class SaveBarredPersonsHandler(PupilRecordAccess access, IPupilRecordRepository records, ICurrentUser currentUser, ISystemAuditSink auditSink)
    : IRequestHandler<SaveBarredPersonsCommand, Result<BarredPersonsDto>>
{
    /// <inheritdoc />
    public async Task<Result<BarredPersonsDto>> HandleAsync(SaveBarredPersonsCommand request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var allowed = await access.CheckAsync(request.PupilId, Privileges.Pupil.SafeguardingUpdate, cancellationToken).ConfigureAwait(false);
        if (allowed.IsFailure)
        {
            return Result.Failure<BarredPersonsDto>(allowed.Error);
        }

        var created = new List<BarredPerson>();
        for (var index = 0; index < request.Persons.Count; index++)
        {
            var person = BarredPerson.Create(Guid.CreateVersion7(), request.PupilId, request.Persons[index].FullName, request.Persons[index].Details, index);
            if (person.IsFailure)
            {
                return Result.Failure<BarredPersonsDto>(new ValidationError(
                    new Dictionary<string, string[]>(StringComparer.Ordinal) { [$"Persons[{index}]"] = [person.Error.Description] }));
            }

            created.Add(person.Value);
        }

        var answer = await records.FindBarredAnswerAsync(request.PupilId, track: true, cancellationToken).ConfigureAwait(false);
        var previous = answer?.HasBarredPersons;
        if (answer is null)
        {
            await records.AddAsync(BarredPersonAnswer.Create(request.PupilId, request.HasBarredPersons), cancellationToken).ConfigureAwait(false);
        }
        else
        {
            answer.Answer(request.HasBarredPersons);
        }

        foreach (var old in await records.ListBarredPersonsAsync(request.PupilId, track: true, cancellationToken).ConfigureAwait(false))
        {
            await records.RemoveAsync(old, cancellationToken).ConfigureAwait(false);
        }

        foreach (var person in created)
        {
            await records.AddAsync(person, cancellationToken).ConfigureAwait(false);
        }

        // The names themselves never enter the audit log's metadata: the log is readable by audit.view, which is wider
        // than pupil.safeguarding.view.
        await auditSink.RecordAsync(
            Privileges.Pupil.SafeguardingUpdate, "barred_person", PupilContactsMapper.Id(request.PupilId),
            new Dictionary<string, object?>(StringComparer.Ordinal) { ["hasBarredPersons"] = request.HasBarredPersons, ["count"] = created.Count },
            currentUser.UserId, cancellationToken,
            beforeMetadata: new Dictionary<string, object?>(StringComparer.Ordinal) { ["hasBarredPersons"] = previous })
            .ConfigureAwait(false);

        return Result.Success(BarredMapper.ToDto(request.PupilId, request.HasBarredPersons, created));
    }
}

/// <summary><c>GET /api/v1/pupils/{pupilId}/health</c> (spec 6.5.7). A command so its read audit commits.</summary>
/// <param name="PupilId">From the route.</param>
public sealed record ReadPupilHealthCommand(Guid PupilId) : ICommand<Result<PupilHealthDto>>;

/// <summary>Nothing structural to check beyond the route.</summary>
internal sealed class ReadPupilHealthCommandValidator : AbstractValidator<ReadPupilHealthCommand>;

/// <summary>Handles <see cref="ReadPupilHealthCommand"/>.</summary>
internal sealed class ReadPupilHealthHandler(PupilRecordAccess access, IPupilRecordRepository records, ICurrentUser currentUser, ISystemAuditSink auditSink)
    : IRequestHandler<ReadPupilHealthCommand, Result<PupilHealthDto>>
{
    /// <inheritdoc />
    public async Task<Result<PupilHealthDto>> HandleAsync(ReadPupilHealthCommand request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var allowed = await access.CheckAsync(request.PupilId, Privileges.Pupil.SafeguardingView, cancellationToken).ConfigureAwait(false);
        if (allowed.IsFailure)
        {
            return Result.Failure<PupilHealthDto>(allowed.Error);
        }

        await auditSink.RecordAsync(
            ReadBarredPersonsHandler.ReadAction, "pupil_health", PupilContactsMapper.Id(request.PupilId), metadata: null, currentUser.UserId, cancellationToken)
            .ConfigureAwait(false);
        return Result.Success(HealthMapper.ToDto(request.PupilId, await records.FindHealthAsync(request.PupilId, track: false, cancellationToken).ConfigureAwait(false)));
    }
}

/// <summary><c>PUT /api/v1/pupils/{pupilId}/health</c>: the whole of section F. Unanswered questions may stay null until approval.</summary>
/// <param name="PupilId">From the route.</param>
/// <param name="HasAllergy">Yes, no, or not yet asked.</param>
/// <param name="AllergyDetails">Required where yes.</param>
/// <param name="HasMedicalCondition">Yes, no, or not yet asked.</param>
/// <param name="MedicalConditionDetails">Required where yes.</param>
/// <param name="TakesRegularMedication">Yes, no, or not yet asked.</param>
/// <param name="MedicationDetails">Required where yes.</param>
/// <param name="SpecialInstructions">Optional.</param>
/// <param name="PreferredHospital">Optional, prompted.</param>
/// <param name="HospitalPhone">Optional, Nigerian format.</param>
/// <param name="BloodGroup">Optional.</param>
/// <param name="Genotype">Optional.</param>
public sealed record SavePupilHealthCommand(
    Guid PupilId, bool? HasAllergy, string? AllergyDetails, bool? HasMedicalCondition, string? MedicalConditionDetails, bool? TakesRegularMedication,
    string? MedicationDetails, string? SpecialInstructions, string? PreferredHospital, string? HospitalPhone, BloodGroup? BloodGroup, Genotype? Genotype)
    : ICommand<Result<PupilHealthDto>>;

/// <summary>Enum members only; the conditional details are the entity's rule.</summary>
internal sealed class SavePupilHealthCommandValidator : AbstractValidator<SavePupilHealthCommand>
{
    public SavePupilHealthCommandValidator()
    {
        RuleFor(command => command.BloodGroup).IsInEnum().When(command => command.BloodGroup is not null);
        RuleFor(command => command.Genotype).IsInEnum().When(command => command.Genotype is not null);
    }
}

/// <summary>Handles <see cref="SavePupilHealthCommand"/>.</summary>
internal sealed class SavePupilHealthHandler(PupilRecordAccess access, IPupilRecordRepository records, ICurrentUser currentUser, ISystemAuditSink auditSink)
    : IRequestHandler<SavePupilHealthCommand, Result<PupilHealthDto>>
{
    /// <inheritdoc />
    public async Task<Result<PupilHealthDto>> HandleAsync(SavePupilHealthCommand request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var allowed = await access.CheckAsync(request.PupilId, Privileges.Pupil.SafeguardingUpdate, cancellationToken).ConfigureAwait(false);
        if (allowed.IsFailure)
        {
            return Result.Failure<PupilHealthDto>(allowed.Error);
        }

        var health = await records.FindHealthAsync(request.PupilId, track: true, cancellationToken).ConfigureAwait(false);
        var isNew = health is null;
        health ??= PupilHealth.Create(request.PupilId);
        var applied = health.Apply(
            request.HasAllergy, request.AllergyDetails, request.HasMedicalCondition, request.MedicalConditionDetails, request.TakesRegularMedication,
            request.MedicationDetails, request.SpecialInstructions, request.PreferredHospital, request.HospitalPhone, request.BloodGroup, request.Genotype);
        if (applied.IsFailure)
        {
            return Result.Failure<PupilHealthDto>(applied.Error);
        }

        if (isNew)
        {
            await records.AddAsync(health, cancellationToken).ConfigureAwait(false);
        }

        // Which questions were answered, never the medical detail itself: audit.view is wider than safeguarding.
        await auditSink.RecordAsync(
            Privileges.Pupil.SafeguardingUpdate, "pupil_health", PupilContactsMapper.Id(request.PupilId),
            new Dictionary<string, object?>(StringComparer.Ordinal)
            {
                ["hasAllergy"] = request.HasAllergy,
                ["hasMedicalCondition"] = request.HasMedicalCondition,
                ["takesRegularMedication"] = request.TakesRegularMedication,
            },
            currentUser.UserId, cancellationToken)
            .ConfigureAwait(false);

        return Result.Success(HealthMapper.ToDto(request.PupilId, health));
    }
}

/// <summary>Pickup mapping.</summary>
internal static class PickupMapper
{
    public static PickupPersonListDto ToDto(Guid pupilId, IEnumerable<AuthorisedPickupPerson> persons) =>
        new(PupilContactsMapper.Id(pupilId), persons.OrderBy(person => person.DisplayOrder)
            .Select(person => new PickupPersonDto(PupilContactsMapper.Id(person.Id), person.FullName, person.Relationship, person.Phone)).ToList());
}

/// <summary>Barred-persons mapping.</summary>
internal static class BarredMapper
{
    public static BarredPersonsDto ToDto(Guid pupilId, bool? answer, IEnumerable<BarredPerson> persons) =>
        new(PupilContactsMapper.Id(pupilId), answer, persons.OrderBy(person => person.DisplayOrder)
            .Select(person => new BarredPersonDto(PupilContactsMapper.Id(person.Id), person.FullName, person.Details)).ToList());
}

/// <summary>Health mapping; no row reads as every question unanswered.</summary>
internal static class HealthMapper
{
    public static PupilHealthDto ToDto(Guid pupilId, PupilHealth? health) =>
        new(PupilContactsMapper.Id(pupilId), health?.HasAllergy, health?.AllergyDetails, health?.HasMedicalCondition, health?.MedicalConditionDetails,
            health?.TakesRegularMedication, health?.MedicationDetails, health?.SpecialInstructions, health?.PreferredHospital, health?.HospitalPhone,
            health?.BloodGroup, health?.Genotype);
}
