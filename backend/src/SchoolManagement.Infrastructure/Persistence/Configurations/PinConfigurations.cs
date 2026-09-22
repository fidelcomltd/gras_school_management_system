using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SchoolManagement.Domain.Pins;

namespace SchoolManagement.Infrastructure.Persistence.Configurations;

/// <summary>EF Core mapping for <see cref="PinBatch"/>.</summary>
internal sealed class PinBatchConfiguration : IEntityTypeConfiguration<PinBatch>
{
    public void Configure(EntityTypeBuilder<PinBatch> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("pin_batch");
        builder.HasKey(batch => batch.Id);
        builder.Property(batch => batch.Id).ValueGeneratedNever();
        builder.Property(batch => batch.Name).HasMaxLength(PinBatch.NameMaxLength).IsRequired();
        builder.Property(batch => batch.PurposeNote).HasMaxLength(PinBatch.PurposeNoteMaxLength);
        builder.Property(batch => batch.State).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(batch => batch.RevokeReason).HasMaxLength(Pin.ReasonMaxLength);
        builder.Property(batch => batch.CreatedBy).HasMaxLength(128);
        builder.Property(batch => batch.ModifiedBy).HasMaxLength(128);

        builder.HasOne<Domain.Sessions.AcademicSession>()
            .WithMany()
            .HasForeignKey(batch => batch.SessionId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(batch => batch.SessionId).HasDatabaseName("ix_pin_batch_session_id");
    }
}

/// <summary>EF Core mapping for <see cref="Pin"/>. <c>lookup_key</c> is unique: global uniqueness (spec 6.8.6).</summary>
internal sealed class PinConfiguration : IEntityTypeConfiguration<Pin>
{
    public void Configure(EntityTypeBuilder<Pin> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("pin");
        builder.HasKey(pin => pin.Id);
        builder.Property(pin => pin.Id).ValueGeneratedNever();
        builder.Property(pin => pin.PinHash).HasMaxLength(200).IsRequired();
        builder.Property(pin => pin.LookupKey).HasMaxLength(Pin.LookupKeyLength).IsRequired();
        builder.Property(pin => pin.Prefix).HasMaxLength(Pin.PrefixLength).IsRequired();
        builder.Property(pin => pin.State).HasConversion<string>().HasMaxLength(20).IsRequired();
        builder.Property(pin => pin.StateReason).HasMaxLength(Pin.ReasonMaxLength);
        builder.Property(pin => pin.Ciphertext).HasMaxLength(128);

        builder.HasOne<PinBatch>()
            .WithMany()
            .HasForeignKey(pin => pin.BatchId)
            .OnDelete(DeleteBehavior.Restrict);

        builder.HasIndex(pin => pin.LookupKey).IsUnique().HasDatabaseName("ux_pin_lookup_key");
        builder.HasIndex(pin => pin.BatchId).HasDatabaseName("ix_pin_batch_id");
        builder.HasIndex(pin => pin.Prefix).HasDatabaseName("ix_pin_prefix");
    }
}
