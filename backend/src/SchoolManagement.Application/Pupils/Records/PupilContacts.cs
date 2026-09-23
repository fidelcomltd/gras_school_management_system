using System.Globalization;
using FluentValidation;
using SchoolManagement.Application.Abstractions.Audit;
using SchoolManagement.Application.Abstractions.Identity;
using SchoolManagement.Application.Abstractions.Messaging;
using SchoolManagement.Application.Abstractions.Pupils;
using SchoolManagement.Domain.Common;
using SchoolManagement.Domain.Pupils;
using SchoolManagement.Domain.Security;

namespace SchoolManagement.Application.Pupils.Records;

/// <summary><c>GET /api/v1/pupils/{pupilId}/contacts</c> (spec 6.5.5): the five contact slots, as recorded.</summary>
/// <param name="PupilId">From the route.</param>
public sealed record GetPupilContactsQuery(Guid PupilId) : IQuery<Result<PupilContactListDto>>;

/// <summary>Nothing structural to check beyond the route.</summary>
internal sealed class GetPupilContactsQueryValidator : AbstractValidator<GetPupilContactsQuery>;

/// <summary>Handles <see cref="GetPupilContactsQuery"/>.</summary>
internal sealed class GetPupilContactsHandler(PupilRecordAccess access, IPupilRecordRepository records)
    : IRequestHandler<GetPupilContactsQuery, Result<PupilContactListDto>>
{
    /// <inheritdoc />
    public async Task<Result<PupilContactListDto>> HandleAsync(GetPupilContactsQuery request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var allowed = await access.CheckAsync(request.PupilId, Privileges.Contact.View, cancellationToken).ConfigureAwait(false);
        if (allowed.IsFailure)
        {
            return Result.Failure<PupilContactListDto>(allowed.Error);
        }

        var contacts = await records.ListContactsAsync(request.PupilId, track: false, cancellationToken).ConfigureAwait(false);
        return Result.Success(PupilContactsMapper.ToDto(request.PupilId, contacts));
    }
}

/// <summary>
/// <c>PUT /api/v1/pupils/{pupilId}/contacts</c> (spec 6.5.5): the whole set of contacts, one per role. A role left out is
/// removed — except that an active pupil must always keep a father, mother or guardian (spec 9.4: the last responsible
/// adult is edited, never deleted). Saving a partial set is allowed while the admission is pending; approval enforces
/// the minimum (spec 6.5.12).
/// </summary>
/// <param name="PupilId">From the route.</param>
/// <param name="Contacts">At most one per role.</param>
public sealed record SavePupilContactsCommand(Guid PupilId, IReadOnlyList<PupilContactInput> Contacts) : ICommand<Result<PupilContactListDto>>;

/// <summary>Set-level rules: one row per role, and at most one primary.</summary>
internal sealed class SavePupilContactsCommandValidator : AbstractValidator<SavePupilContactsCommand>
{
    public SavePupilContactsCommandValidator()
    {
        RuleFor(command => command.Contacts).NotNull();

        // Guarded, so a null list or a null entry is a 422 rather than a NullReferenceException in the set rules.
        When(command => command.Contacts is not null && command.Contacts.All(contact => contact is not null), () =>
        {
            RuleForEach(command => command.Contacts).ChildRules(contact => contact.RuleFor(entry => entry.Role).IsInEnum());
            RuleFor(command => command.Contacts)
                .Must(contacts => contacts.Select(contact => contact.Role).Distinct().Count() == contacts.Count)
                .WithMessage("Each contact role can be recorded only once.")
                .Must(contacts => contacts.Count(contact => contact.IsPrimaryContact) <= 1)
                .WithMessage("Only one contact can be the primary contact.")
                .Must(contacts => !contacts.Any(contact => PupilContact.IsResponsibleAdult(contact.Role)) || contacts.Count(contact => contact.IsPrimaryContact) == 1)
                .WithMessage("Mark one of the father, mother or guardian as the primary contact.");
        }).Otherwise(() => RuleForEach(command => command.Contacts).NotNull().WithMessage("A contact entry is missing.").When(command => command.Contacts is not null));
    }
}

/// <summary>Handles <see cref="SavePupilContactsCommand"/>.</summary>
internal sealed class SavePupilContactsHandler(
    PupilRecordAccess access, IPupilRecordRepository records, ICurrentUser currentUser, ISystemAuditSink auditSink)
    : IRequestHandler<SavePupilContactsCommand, Result<PupilContactListDto>>
{
    /// <inheritdoc />
    public async Task<Result<PupilContactListDto>> HandleAsync(SavePupilContactsCommand request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        var allowed = await access.CheckAsync(request.PupilId, Privileges.Contact.Update, cancellationToken).ConfigureAwait(false);
        if (allowed.IsFailure)
        {
            return Result.Failure<PupilContactListDto>(allowed.Error);
        }

        var existing = (await records.ListContactsAsync(request.PupilId, track: true, cancellationToken).ConfigureAwait(false))
            .ToDictionary(contact => contact.Role);

        // Spec 9.4: the last responsible adult of an active pupil is edited, never removed. A pupil who never had one (an
        // older record) may still have other contacts added.
        if (allowed.Value.Status != PupilStatus.Pending
            && existing.Keys.Any(PupilContact.IsResponsibleAdult)
            && !request.Contacts.Any(contact => PupilContact.IsResponsibleAdult(contact.Role)))
        {
            return Result.Failure<PupilContactListDto>(Error.Conflict(
                "contact.last_responsible_adult", "A pupil must keep at least one father, mother or guardian. Edit the contact instead of removing it."));
        }

        var before = existing.Values.Select(Snapshot).ToList();

        var failures = new Dictionary<string, string[]>(StringComparer.Ordinal);
        for (var index = 0; index < request.Contacts.Count; index++)
        {
            var input = request.Contacts[index];
            if (!existing.TryGetValue(input.Role, out var contact))
            {
                contact = PupilContact.Create(Guid.CreateVersion7(), request.PupilId, input.Role);
                await records.AddAsync(contact, cancellationToken).ConfigureAwait(false);
            }

            var applied = contact.Apply(input.FullName, input.Relationship, input.Phone, input.WhatsappNumber, input.Occupation, input.Email, input.IsPrimaryContact);
            if (applied.IsFailure)
            {
                failures[$"Contacts[{index}]"] = [applied.Error.Description];
            }

            existing.Remove(input.Role);
            existing[input.Role] = contact;
        }

        if (failures.Count > 0)
        {
            return Result.Failure<PupilContactListDto>(new ValidationError(failures));
        }

        var kept = request.Contacts.Select(contact => contact.Role).ToHashSet();
        foreach (var removed in existing.Values.Where(contact => !kept.Contains(contact.Role)).ToList())
        {
            await records.RemoveAsync(removed, cancellationToken).ConfigureAwait(false);
            existing.Remove(removed.Role);
        }

        var after = existing.Values.OrderBy(contact => contact.Role).ToList();
        await auditSink.RecordAsync(
            Privileges.Contact.Update, "pupil_contact", PupilContactsMapper.Id(request.PupilId),
            new Dictionary<string, object?>(StringComparer.Ordinal) { ["contacts"] = after.Select(Snapshot).ToList() },
            currentUser.UserId, cancellationToken,
            beforeMetadata: new Dictionary<string, object?>(StringComparer.Ordinal) { ["contacts"] = before })
            .ConfigureAwait(false);

        return Result.Success(PupilContactsMapper.ToDto(request.PupilId, after));
    }

    private static Dictionary<string, object?> Snapshot(PupilContact contact) => new(StringComparer.Ordinal)
    {
        ["role"] = contact.Role.ToString(),
        ["fullName"] = contact.FullName,
        ["phone"] = contact.Phone,
        ["primary"] = contact.IsPrimaryContact,
    };
}

/// <summary>Contact mapping shared by the reads and the save.</summary>
internal static class PupilContactsMapper
{
    public static string Id(Guid id) => id.ToString("D", CultureInfo.InvariantCulture);

    public static PupilContactListDto ToDto(Guid pupilId, IEnumerable<PupilContact> contacts) =>
        new(Id(pupilId), contacts.OrderBy(contact => contact.Role).Select(contact => new PupilContactDto(
            Id(contact.Id), contact.Role, contact.FullName, contact.Relationship, contact.Phone, contact.WhatsappNumber, contact.Occupation,
            contact.Email, contact.IsPrimaryContact)).ToList());
}
