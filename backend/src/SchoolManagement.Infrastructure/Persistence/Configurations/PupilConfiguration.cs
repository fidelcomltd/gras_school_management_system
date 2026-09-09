using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SchoolManagement.Domain.Pupils;

namespace SchoolManagement.Infrastructure.Persistence.Configurations;

/// <summary>Mapping for <see cref="Pupil"/> (spec 6.5.4).</summary>
internal sealed class PupilConfiguration : IEntityTypeConfiguration<Pupil>
{
    /// <summary>Maximum length of the stored audit-actor column, matching <c>ArmConfiguration</c>.</summary>
    private const int AuditActorMaxLength = 128;

    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<Pupil> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("pupils");

        builder.HasKey(pupil => pupil.Id);
        builder.Property(pupil => pupil.Id).ValueGeneratedNever();

        builder.Property(pupil => pupil.RegistrationNumber)
            .HasMaxLength(Pupil.RegistrationNumberMaxLength)
            .HasColumnName("registration_number");

        builder.Property(pupil => pupil.Surname).IsRequired().HasMaxLength(Pupil.NameMaxLength);
        builder.Property(pupil => pupil.FirstName).IsRequired().HasMaxLength(Pupil.NameMaxLength);
        builder.Property(pupil => pupil.MiddleName).HasMaxLength(Pupil.NameMaxLength);

        builder.Property(pupil => pupil.Sex).IsRequired().HasConversion<string>().HasMaxLength(10);

        builder.Property(pupil => pupil.DateOfBirth).IsRequired().HasColumnType("date");

        builder.Property(pupil => pupil.Nationality).IsRequired().HasMaxLength(Pupil.NationalityMaxLength);
        builder.Property(pupil => pupil.StateOfOrigin).IsRequired().HasMaxLength(Pupil.NationalityMaxLength);
        builder.Property(pupil => pupil.Lga).IsRequired().HasMaxLength(Pupil.LgaMaxLength);
        builder.Property(pupil => pupil.HomeAddress).IsRequired().HasMaxLength(Pupil.HomeAddressMaxLength);

        builder.Property(pupil => pupil.PreviousSchool).HasMaxLength(Pupil.PreviousSchoolMaxLength);
        builder.Property(pupil => pupil.PreviousClass).HasMaxLength(Pupil.PreviousClassMaxLength);

        builder.Property(pupil => pupil.Status).IsRequired().HasConversion<string>().HasMaxLength(20);

        builder.Property(pupil => pupil.OtherInformation).HasMaxLength(Pupil.OtherInformationMaxLength);

        builder.Property(pupil => pupil.CreatedBy).HasMaxLength(AuditActorMaxLength);
        builder.Property(pupil => pupil.ModifiedBy).HasMaxLength(AuditActorMaxLength);

        // Spec 6.5.4: "Unique." Backstops TASK-0051's issuance so that card needs no migration of
        // its own (the task card's own instruction). Nullable + unique: Postgres treats every NULL
        // as distinct, so any number of pending (null) rows coexist under this index.
        builder.HasIndex(pupil => pupil.RegistrationNumber)
            .IsUnique()
            .HasDatabaseName("ix_pupils_registration_number_unique");

        // The pending-exclusion invariant (spec 6.5.14), enforced ONCE here rather than by every
        // caller: every ordinary query through this DbSet excludes a Pending row by construction. A
        // caller that genuinely needs one (a direct id lookup, the admissions queue, duplicate
        // detection, or GET /pupils?status=pending) calls IgnoreQueryFilters() explicitly — the same,
        // already-reviewed technique ApplicationDbContext.ApplySoftDeleteQueryFilters uses for
        // ISoftDeletable, applied here to one named entity instead of by reflection over an
        // interface (Pupil is the only entity this invariant applies to in this card).
        builder.HasQueryFilter(pupil => pupil.Status != PupilStatus.Pending);

        // Surname-first list order (spec 6.5.15, minus the class-progression half this card cannot
        // build — see PupilListCursor's remarks) is the keyset the list query sorts and pages by.
        builder.HasIndex(pupil => new { pupil.Surname, pupil.Id })
            .HasDatabaseName("ix_pupils_surname_id");
    }
}
