using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SchoolManagement.Domain.Auth;
using SchoolManagement.Domain.Security;
using SchoolManagement.Domain.Sessions;

namespace SchoolManagement.Infrastructure.Persistence.Configurations;

/// <summary>Mapping for <see cref="RoleAssignment"/> (spec 6.1.5).</summary>
internal sealed class RoleAssignmentConfiguration : IEntityTypeConfiguration<RoleAssignment>
{
    /// <summary>Maximum length of the stored audit-actor column, matching <c>RoleConfiguration</c>.</summary>
    private const int AuditActorMaxLength = 128;

    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<RoleAssignment> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("role_assignments");

        builder.HasKey(assignment => assignment.Id);
        builder.Property(assignment => assignment.Id).ValueGeneratedNever();

        builder.Property(assignment => assignment.AdminAccountId).IsRequired();
        builder.Property(assignment => assignment.RoleId).IsRequired();

        // Nullable per spec 6.1.5's literal field table ("Required unless the role is Super Admin, in
        // which case it must be null") — TASK-0030 never actually produces a null row here (see the
        // entity's own remarks), but the column shape already matches the spec rather than needing a
        // later ALTER.
        builder.Property(assignment => assignment.SessionId);

        builder.Property(assignment => assignment.ScopeType)
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(20);

        // Comma-joined text, same technique as RoleConfiguration.Privileges — a fixed-shape GUID list
        // needs no array value-conversion support from the provider, and this table's arm lists are
        // small (admin-configuration-sized, never pupil-scale).
        var armIdsProperty = builder
            .Property(assignment => assignment.ArmIds)
            .HasField("_armIds")
            .UsePropertyAccessMode(PropertyAccessMode.Field)
            .HasColumnName("arm_ids")
            .HasColumnType("text")
            .HasConversion(
                armIds => string.Join(',', armIds.Select(armId => armId.ToString("D"))),
                text => string.IsNullOrEmpty(text)
                    ? new List<Guid>()
                    : text.Split(',', StringSplitOptions.RemoveEmptyEntries).Select(Guid.Parse).ToList());

        armIdsProperty.Metadata.SetValueComparer(
            new ValueComparer<IReadOnlyList<Guid>>(
                (left, right) => (left ?? Array.Empty<Guid>()).SequenceEqual(right ?? Array.Empty<Guid>()),
                value => value.Aggregate(0, (hash, item) => HashCode.Combine(hash, item.GetHashCode())),
                value => value.ToList()));

        builder.Property(assignment => assignment.GrantedBy).IsRequired();

        builder.Property(assignment => assignment.Status)
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(20);

        builder.Property(assignment => assignment.CreatedBy).HasMaxLength(AuditActorMaxLength);
        builder.Property(assignment => assignment.ModifiedBy).HasMaxLength(AuditActorMaxLength);

        builder.HasIndex(assignment => assignment.AdminAccountId)
            .HasDatabaseName("ix_role_assignments_admin_account_id");

        builder.HasIndex(assignment => assignment.RoleId)
            .HasDatabaseName("ix_role_assignments_role_id");

        // RESTRICT everywhere: no route deletes an admin account, a role, or a session — status
        // transitions and archival only — so none of these ever fire in practice, but RESTRICT is the
        // honest default rather than an implicit CASCADE nobody decided on (same reasoning
        // ArmConfiguration already gives for its own foreign keys).
        builder.HasOne<AdminAccount>()
            .WithMany()
            .HasForeignKey(assignment => assignment.AdminAccountId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<Role>()
            .WithMany()
            .HasForeignKey(assignment => assignment.RoleId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasOne<AcademicSession>()
            .WithMany()
            .HasForeignKey(assignment => assignment.SessionId)
            .OnDelete(DeleteBehavior.Restrict);

        // A second relationship to AdminAccount (the acting/granting account) — EF Core supports more
        // than one FK from the same table to the same target type as long as each names its own
        // foreign key, which this does.
        builder.HasOne<AdminAccount>()
            .WithMany()
            .HasForeignKey(assignment => assignment.GrantedBy)
            .OnDelete(DeleteBehavior.Restrict);
    }
}
