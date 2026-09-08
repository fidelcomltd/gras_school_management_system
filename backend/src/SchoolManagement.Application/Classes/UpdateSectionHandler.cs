using System.Globalization;
using SchoolManagement.Application.Abstractions.Audit;
using SchoolManagement.Application.Abstractions.Classes;
using SchoolManagement.Application.Abstractions.Identity;
using SchoolManagement.Application.Abstractions.Messaging;
using SchoolManagement.Domain.Common;
using SchoolManagement.Domain.Security;

namespace SchoolManagement.Application.Classes;

/// <summary>Handles <see cref="UpdateSectionCommand"/>.</summary>
internal sealed class UpdateSectionHandler(
    ISectionRepository sections,
    ICurrentUser currentUser,
    ISystemAuditSink auditSink)
    : IRequestHandler<UpdateSectionCommand, Result<SectionDto>>
{
    private const string EntityType = "section";

    /// <inheritdoc />
    public async Task<Result<SectionDto>> HandleAsync(UpdateSectionCommand request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var section = await sections.FindTrackedByIdAsync(request.Id, cancellationToken).ConfigureAwait(false);

        if (section is null)
        {
            return Result.Failure<SectionDto>(Error.NotFound("section.not_found", "No section was found with that id."));
        }

        var trimmedKey = request.Name.Trim().ToLowerInvariant();

        if (!string.Equals(trimmedKey, section.NameKey, StringComparison.Ordinal) &&
            await sections.NameExistsAsync(trimmedKey, section.Id, cancellationToken).ConfigureAwait(false))
        {
            return Result.Failure<SectionDto>(Error.Conflict(
                "section.name_duplicate",
                $"A section named {request.Name.Trim()} already exists."));
        }

        var rename = section.Rename(request.Name);

        if (rename.IsFailure)
        {
            return Result.Failure<SectionDto>(rename.Error);
        }

        await auditSink.RecordAsync(
            Privileges.Level.Update,
            EntityType,
            section.Id.ToString("D", CultureInfo.InvariantCulture),
            metadata: null,
            actorAdminId: currentUser.UserId,
            cancellationToken).ConfigureAwait(false);

        return Result.Success(new SectionDto(section.Id.ToString("D", CultureInfo.InvariantCulture), section.Name));
    }
}
