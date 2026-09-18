using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SchoolManagement.Domain.Settings;

namespace SchoolManagement.Infrastructure.Persistence.Configurations;

/// <summary>Mapping for spec 6.5.10's <see cref="RegistrationCounter"/> table (TASK-0005c).</summary>
internal sealed class RegistrationCounterConfiguration : IEntityTypeConfiguration<RegistrationCounter>
{
    /// <summary>Generous headroom over both shapes the key ever takes: a 4-digit year, or the fixed "ALL" sentinel.</summary>
    private const int CounterKeyMaxLength = 10;

    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<RegistrationCounter> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("registration_counter");

        builder.HasKey(counter => counter.Id);

        builder.Property(counter => counter.Id)
            .HasColumnName("counter_key")
            .HasMaxLength(CounterKeyMaxLength)
            .ValueGeneratedNever();

        builder.Property(counter => counter.LastSerial)
            .HasColumnName("last_serial")
            .IsRequired();

        // No HasData: unlike school_profile, this table starts genuinely empty — no register exists
        // yet (TASK-0005c's own scope line), so there is no seed row to insert.
    }
}
