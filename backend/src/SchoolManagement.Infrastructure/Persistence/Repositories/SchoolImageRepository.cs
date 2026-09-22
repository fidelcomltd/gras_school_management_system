using Microsoft.EntityFrameworkCore;
using SchoolManagement.Application.Abstractions.Settings;
using SchoolManagement.Application.Settings;
using SchoolManagement.Domain.Settings;
using DomainSizeVariant = SchoolManagement.Domain.Settings.SchoolImageSizeVariant;

namespace SchoolManagement.Infrastructure.Persistence.Repositories;

/// <summary>EF Core implementation of <see cref="ISchoolImageRepository"/>.</summary>
internal sealed class SchoolImageRepository(ApplicationDbContext context) : ISchoolImageRepository
{
    /// <inheritdoc />
    public Task AddRangeAsync(IReadOnlyList<SchoolImage> images, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(images);

        context.SchoolImages.AddRange(images);
        return Task.CompletedTask;
    }

    /// <inheritdoc />
    public async Task<SchoolImageDto?> FindCurrentDtoAsync(Guid? uploadGroupId, CancellationToken cancellationToken)
    {
        if (uploadGroupId is null)
        {
            return null;
        }

        var original = await context.SchoolImages
            .AsNoTracking()
            .Where(image => image.UploadGroupId == uploadGroupId && image.SizeVariant == DomainSizeVariant.Original)
            .Select(image => new { image.WidthPixels, image.HeightPixels, image.CreatedAtUtc, image.CreatedBy })
            .FirstOrDefaultAsync(cancellationToken)
            .ConfigureAwait(false);

        if (original is null)
        {
            return null;
        }

        string? uploadedByName = null;

        if (original.CreatedBy is { } actorIdText && Guid.TryParse(actorIdText, out var actorId))
        {
            uploadedByName = await context.AdminAccounts
                .AsNoTracking()
                .Where(account => account.Id == actorId)
                .Select(account => account.StaffName)
                .FirstOrDefaultAsync(cancellationToken)
                .ConfigureAwait(false);
        }

        return new SchoolImageDto(original.WidthPixels, original.HeightPixels, original.CreatedAtUtc, uploadedByName);
    }
}
