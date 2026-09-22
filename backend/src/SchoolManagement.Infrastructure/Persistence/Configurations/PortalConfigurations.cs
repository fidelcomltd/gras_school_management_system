using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SchoolManagement.Domain.Pins;
using SchoolManagement.Domain.Portal;

namespace SchoolManagement.Infrastructure.Persistence.Configurations;

/// <summary>EF Core mapping for <see cref="PinUse"/>: one row per viewing session.</summary>
internal sealed class PinUseConfiguration : IEntityTypeConfiguration<PinUse>
{
    public void Configure(EntityTypeBuilder<PinUse> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("pin_use");
        builder.HasKey(use => use.Id);
        builder.Property(use => use.Id).ValueGeneratedNever();
        builder.Property(use => use.TokenHash).HasMaxLength(64).IsRequired();
        builder.Property(use => use.SourceAddress).HasMaxLength(64);
        builder.Property(use => use.UserAgent).HasMaxLength(120);
        builder.Property(use => use.ViewedJson).HasColumnType("jsonb").IsRequired();

        builder.HasOne<Pin>().WithMany().HasForeignKey(use => use.PinId).OnDelete(DeleteBehavior.Restrict);
        builder.HasOne<Domain.Pupils.Pupil>().WithMany().HasForeignKey(use => use.PupilId).OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(use => use.TokenHash).IsUnique().HasDatabaseName("ux_pin_use_token_hash");
        builder.HasIndex(use => new { use.PinId, use.OpenedAtUtc }).HasDatabaseName("ix_pin_use_pin_id_opened_at_utc");
        builder.HasIndex(use => use.PupilId).HasDatabaseName("ix_pin_use_pupil_id");
    }
}

/// <summary>EF Core mapping for <see cref="PortalAttempt"/>.</summary>
internal sealed class PortalAttemptConfiguration : IEntityTypeConfiguration<PortalAttempt>
{
    public void Configure(EntityTypeBuilder<PortalAttempt> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("portal_attempt");
        builder.HasKey(attempt => attempt.Id);
        builder.Property(attempt => attempt.Id).ValueGeneratedNever();
        builder.Property(attempt => attempt.RegistrationNumber).HasMaxLength(40).IsRequired();
        builder.Property(attempt => attempt.PinPrefix).HasMaxLength(Pin.PrefixLength);
        builder.Property(attempt => attempt.Outcome).HasConversion<string>().HasMaxLength(32).IsRequired();
        builder.Property(attempt => attempt.SourceAddress).HasMaxLength(64);

        builder.HasIndex(attempt => new { attempt.SourceAddress, attempt.AttemptedAtUtc }).HasDatabaseName("ix_portal_attempt_source_address_attempted_at_utc");
        builder.HasIndex(attempt => new { attempt.RegistrationNumber, attempt.AttemptedAtUtc }).HasDatabaseName("ix_portal_attempt_registration_number_attempted_at_utc");
        builder.HasIndex(attempt => attempt.PinId).HasDatabaseName("ix_portal_attempt_pin_id");
    }
}
