using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SchoolManagement.Domain.Sessions;

namespace SchoolManagement.Infrastructure.Persistence.Configurations;

/// <summary>Mapping for <see cref="AcademicSession"/> (spec 6.3.3).</summary>
internal sealed class AcademicSessionConfiguration : IEntityTypeConfiguration<AcademicSession>
{
    /// <summary>Maximum length of the stored audit-actor column, matching <c>RoleConfiguration</c>.</summary>
    private const int AuditActorMaxLength = 128;

    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<AcademicSession> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("academic_sessions");

        builder.HasKey(session => session.Id);

        builder.Property(session => session.Id).ValueGeneratedNever();

        builder.Property(session => session.Name)
            .IsRequired()
            .HasMaxLength(AcademicSession.NameLength);

        builder.Property(session => session.StartDate).IsRequired();
        builder.Property(session => session.EndDate).IsRequired();

        builder.Property(session => session.State)
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(20);

        builder.Property(session => session.CreatedBy).HasMaxLength(AuditActorMaxLength);
        builder.Property(session => session.ModifiedBy).HasMaxLength(AuditActorMaxLength);

        // Spec 6.3.3: "Unique."
        builder.HasIndex(session => session.Name)
            .IsUnique()
            .HasDatabaseName("ix_academic_sessions_name_unique");

        // Spec 6.3.3/6.3.9: "Only one session may be active" — a partial unique index the DATABASE
        // enforces, not application code. Every row that matches the filter has the same `state`
        // value, so uniqueness on that single column allows at most one such row to exist — the same
        // technique `AdminAccountConfiguration`'s email index and `RoleConfiguration`'s name index use
        // for their own filtered uniqueness rules, applied here to guarantee singleness instead.
        builder.HasIndex(session => session.State)
            .IsUnique()
            .HasFilter("state = 'Active'")
            .HasDatabaseName("ix_academic_sessions_single_active");
    }
}
