using System.Globalization;
using SchoolManagement.Application.Abstractions.Audit;
using SchoolManagement.Application.Abstractions.Classes;
using SchoolManagement.Application.Abstractions.Identity;
using SchoolManagement.Application.Abstractions.Messaging;
using SchoolManagement.Domain.Classes;
using SchoolManagement.Domain.Common;
using SchoolManagement.Domain.Security;

namespace SchoolManagement.Application.Classes;

/// <summary>Handles <see cref="CreateSectionCommand"/>.</summary>
internal sealed class CreateSectionHandler(
    ISectionRepository sections,
    ICurrentUser currentUser,
    ISystemAuditSink auditSink)
    : IRequestHandler<CreateSectionCommand, Result<SectionDto>>
{
    private const string EntityType = "section";

    /// <inheritdoc />
    public async Task<Result<SectionDto>> HandleAsync(CreateSectionCommand request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var trimmedKey = request.Name.Trim().ToLowerInvariant();

        if (await sections.NameExistsAsync(trimmedKey, excludingId: null, cancellationToken).ConfigureAwait(false))
        {
            return Result.Failure<SectionDto>(Error.Conflict(
                "section.name_duplicate",
                $"A section named {request.Name.Trim()} already exists."));
        }

        var creation = Section.Create(Guid.CreateVersion7(), request.Name, request.RatesTraits ?? false);

        if (creation.IsFailure)
        {
            return Result.Failure<SectionDto>(creation.Error);
        }

        var section = creation.Value;
        await sections.AddAsync(section, cancellationToken).ConfigureAwait(false);

        await auditSink.RecordAsync(
            Privileges.Level.Create,
            EntityType,
            section.Id.ToString("D", CultureInfo.InvariantCulture),
            metadata: null,
            actorAdminId: currentUser.UserId,
            cancellationToken).ConfigureAwait(false);

        return Result.Success(new SectionDto(section.Id.ToString("D", CultureInfo.InvariantCulture), section.Name, section.RatesTraits));
    }
}
