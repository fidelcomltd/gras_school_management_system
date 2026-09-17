using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SchoolManagement.Domain.Subjects;

namespace SchoolManagement.Infrastructure.Persistence.Configurations;

/// <summary>Mapping for <see cref="Subject"/> (spec 6.6.2; TASK-0070).</summary>
internal sealed class SubjectConfiguration : IEntityTypeConfiguration<Subject>
{
    /// <summary>Maximum length of the stored audit-actor column, matching <c>ArmConfiguration</c>.</summary>
    private const int AuditActorMaxLength = 128;

    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<Subject> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("subjects");

        builder.HasKey(subject => subject.Id);
        builder.Property(subject => subject.Id).ValueGeneratedNever();

        builder.Property(subject => subject.Name)
            .IsRequired()
            .HasMaxLength(Subject.NameMaxLength);

        builder.Property(subject => subject.NameKey)
            .IsRequired()
            .HasMaxLength(Subject.NameMaxLength)
            .HasColumnName("name_key");

        // Nullable — TASK-0070 delta amendment 1 departs from spec 6.6.2's Req: Yes. Seeded NULL.
        builder.Property(subject => subject.Code).HasMaxLength(Subject.CodeMaxLength);

        builder.Property(subject => subject.CodeKey)
            .HasMaxLength(Subject.CodeMaxLength)
            .HasColumnName("code_key");

        builder.Property(subject => subject.Description).HasMaxLength(Subject.DescriptionMaxLength);

        builder.Property(subject => subject.Status)
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(20);

        builder.Property(subject => subject.CreatedBy).HasMaxLength(AuditActorMaxLength);
        builder.Property(subject => subject.ModifiedBy).HasMaxLength(AuditActorMaxLength);

        // Spec 6.6.2: "Unique, case-insensitive" — no status carve-out, same reasoning as
        // ClassLevelConfiguration's own name_key index.
        builder.HasIndex(subject => subject.NameKey)
            .IsUnique()
            .HasDatabaseName("ix_subjects_name_key_unique");

        // Spec 6.6.2: "Unique" where supplied. A PARTIAL index (code_key IS NOT NULL) so any number
        // of subjects may share the unseeded NULL code — the same partial-index technique as
        // ClassLevelConfiguration's progression_order index, applied here to a nullable-and-optional
        // column instead of a status carve-out.
        builder.HasIndex(subject => subject.CodeKey)
            .IsUnique()
            .HasFilter("code_key IS NOT NULL")
            .HasDatabaseName("ix_subjects_code_key_unique");

        SeedSubjects(builder);
    }

    /// <summary>
    /// Spec 6.6.2's 28 seeded rows (corrected against the was/now table — see <see cref="SeededSubjects"/>).
    /// No <c>code</c> is seeded (delta amendment 1).
    /// </summary>
    private static void SeedSubjects(EntityTypeBuilder<Subject> builder)
    {
        var seedRows = SeededSubjects.All.Select(subject => new
        {
            subject.Id,
            subject.Name,
            NameKey = subject.Name.ToLowerInvariant(),
            Code = (string?)null,
            CodeKey = (string?)null,
            Description = (string?)null,
            Status = SubjectStatus.Active,
            CreatedAtUtc = SeededSubjects.SeedTimestamp,
            CreatedBy = (string?)null,
            ModifiedAtUtc = (DateTimeOffset?)null,
            ModifiedBy = (string?)null,
            subject.Version,
        });

        builder.HasData(seedRows);
    }
}
