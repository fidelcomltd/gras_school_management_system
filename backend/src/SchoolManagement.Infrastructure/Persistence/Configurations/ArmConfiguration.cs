using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SchoolManagement.Domain.Classes;

namespace SchoolManagement.Infrastructure.Persistence.Configurations;

/// <summary>Mapping for <see cref="Arm"/> (spec 6.4.3, 6.4.7, 6.4.9).</summary>
internal sealed class ArmConfiguration : IEntityTypeConfiguration<Arm>
{
    /// <summary>Maximum length of the stored audit-actor column, matching <c>RoleConfiguration</c>.</summary>
    private const int AuditActorMaxLength = 128;

    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<Arm> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("arms");

        builder.HasKey(arm => arm.Id);
        builder.Property(arm => arm.Id).ValueGeneratedNever();

        builder.Property(arm => arm.ClassLevelId).IsRequired();
        builder.Property(arm => arm.SessionId).IsRequired();

        builder.Property(arm => arm.Label)
            .IsRequired()
            .HasMaxLength(Arm.LabelMaxLength);

        builder.Property(arm => arm.LabelKey)
            .IsRequired()
            .HasMaxLength(Arm.LabelMaxLength)
            .HasColumnName("label_key");

        builder.Property(arm => arm.Capacity).IsRequired();
        builder.Property(arm => arm.FormTeacherAdminId);

        builder.Property(arm => arm.Status)
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(20);

        builder.Property(arm => arm.CreatedBy).HasMaxLength(AuditActorMaxLength);
        builder.Property(arm => arm.ModifiedBy).HasMaxLength(AuditActorMaxLength);

        // Spec 6.4.3, 6.4.8: "Unique within the same level and session, case-insensitive" — the
        // database, not only application validation, backstops it (this card's own concurrency AC).
        builder.HasIndex(arm => new { arm.ClassLevelId, arm.SessionId, arm.LabelKey })
            .IsUnique()
            .HasDatabaseName("ix_arms_level_session_label_unique");

        builder.HasIndex(arm => arm.SessionId)
            .HasDatabaseName("ix_arms_session_id");

        // RESTRICT: a level with any arm cannot be deleted (DeleteLevelHandler's own friendly check
        // runs first; this is the database backstop, same shape as ClassLevelConfiguration's
        // self-referencing FK).
        builder.HasOne<ClassLevel>()
            .WithMany()
            .HasForeignKey(arm => arm.ClassLevelId)
            .OnDelete(DeleteBehavior.Restrict);

        // No delete route exists for a session, so this never fires today; RESTRICT is still the
        // correct default rather than an implicit CASCADE nobody decided on.
        builder.HasOne<Domain.Sessions.AcademicSession>()
            .WithMany()
            .HasForeignKey(arm => arm.SessionId)
            .OnDelete(DeleteBehavior.Restrict);

        // Spec 6.4.8: "Form teacher's account deactivated mid-term: the arm keeps the reference" — no
        // delete route exists for an admin account either (status changes only), so RESTRICT never
        // fires in practice; it is still the honest default.
        builder.HasOne<Domain.Auth.AdminAccount>()
            .WithMany()
            .HasForeignKey(arm => arm.FormTeacherAdminId)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
