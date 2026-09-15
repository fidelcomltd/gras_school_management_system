using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SchoolManagement.Domain.Pupils;

namespace SchoolManagement.Infrastructure.Persistence.Configurations;

/// <summary>Mapping for <see cref="PupilRegNumberHistory"/> (TASK-0063, spec 6.5.10).</summary>
internal sealed class PupilRegNumberHistoryConfiguration : IEntityTypeConfiguration<PupilRegNumberHistory>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<PupilRegNumberHistory> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("pupil_reg_number_history");

        builder.HasKey(row => row.Id);
        builder.Property(row => row.Id).ValueGeneratedNever();

        builder.Property(row => row.PupilId).IsRequired();

        builder.Property(row => row.OldRegistrationNumber)
            .IsRequired()
            .HasMaxLength(PupilRegNumberHistory.OldRegistrationNumberMaxLength);

        builder.Property(row => row.Reason)
            .IsRequired()
            .HasMaxLength(PupilRegNumberHistory.ReasonMaxLength);

        builder.Property(row => row.CorrectedBy);

        builder.Property(row => row.CorrectedAtUtc).IsRequired();

        // RESTRICT, matching AdmissionRecordConfiguration's own reasoning: no delete route exists
        // for a pupil once a correction has ever touched them, and this row must survive anyway
        // (spec 6.5.10: "stays there permanently as a lookup alias").
        builder.HasOne<Pupil>()
            .WithMany()
            .HasForeignKey(row => row.PupilId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(row => row.PupilId)
            .HasDatabaseName("ix_pupil_reg_number_history_pupil_id");

        // Unique, the same backstop PupilConfiguration's own index gives the live column: two
        // corrections must never alias the same old number to two different lookups.
        builder.HasIndex(row => row.OldRegistrationNumber)
            .IsUnique()
            .HasDatabaseName("ix_pupil_reg_number_history_old_registration_number_unique");

        // Deliberately NO HasOne/foreign key to AdminAccount for CorrectedBy — the same reasoning
        // AuditEventConfiguration gives for ActorAdminId: this row must stay readable after the
        // acting account is later renamed or removed.
    }
}
