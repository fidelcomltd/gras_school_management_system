using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SchoolManagement.Domain.Settings;

namespace SchoolManagement.Infrastructure.Persistence.Configurations;

/// <summary>Mapping for the <see cref="SchoolProfile"/> singleton (spec 6.2.2, 6.2.3; TASK-0005a).</summary>
internal sealed class SchoolProfileConfiguration : IEntityTypeConfiguration<SchoolProfile>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<SchoolProfile> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("school_profile");

        builder.HasKey(profile => profile.Id);

        builder.Property(profile => profile.Id)
            .ValueGeneratedNever();

        builder.Property(profile => profile.SchoolName)
            .IsRequired()
            .HasMaxLength(SchoolProfile.SchoolNameMaxLength);

        builder.Property(profile => profile.ShortName)
            .IsRequired()
            .HasMaxLength(SchoolProfile.ShortNameMaxLength);

        builder.Property(profile => profile.Abbreviation)
            .IsRequired()
            .HasMaxLength(SchoolProfile.AbbreviationMaxLength);

        builder.Property(profile => profile.Address)
            .IsRequired()
            .HasMaxLength(SchoolProfile.AddressMaxLength);

        builder.Property(profile => profile.Phone)
            .IsRequired()
            .HasMaxLength(SchoolProfile.PhoneMaxLength);

        builder.Property(profile => profile.Email)
            .IsRequired()
            .HasMaxLength(SchoolProfile.EmailMaxLength);

        builder.Property(profile => profile.Motto)
            .HasMaxLength(SchoolProfile.MottoMaxLength);

        builder.Property(profile => profile.HeadTeacherName)
            .IsRequired()
            .HasMaxLength(SchoolProfile.HeadTeacherNameMaxLength);

        builder.Property(profile => profile.Timezone)
            .IsRequired()
            .HasMaxLength(32);

        builder.Property(profile => profile.IdentityVersionNumber)
            .IsRequired();

        builder.Property(profile => profile.AbbreviationVersionNumber)
            .IsRequired();

        builder.Property(profile => profile.Separator)
            .IsRequired()
            .HasMaxLength(1);

        builder.Property(profile => profile.SerialWidth)
            .IsRequired();

        builder.Property(profile => profile.SerialReset)
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(20);

        builder.Property(profile => profile.RegNumberVersionNumber)
            .IsRequired();

        builder.Property(profile => profile.GradingVersionNumber)
            .IsRequired();

        builder.Property(profile => profile.AssessmentVersionNumber)
            .IsRequired();

        // Spec 6.2.2: the abbreviation is seeded `GRAS`; every other identity field is explicitly NOT
        // seeded ("Admin must supply") and so starts empty rather than a placeholder value that would
        // look like real data. Both version pointers start at 0 — "installed, never yet saved through
        // a PATCH" — the first successful save on each group takes it to 1. Reg-number fields are
        // seeded at spec 6.2.4's own stated defaults (TASK-0005c).
        builder.HasData(new
        {
            Id = SchoolProfile.SingletonId,
            SchoolName = string.Empty,
            ShortName = string.Empty,
            Abbreviation = SchoolProfile.SeededAbbreviation,
            Address = string.Empty,
            Phone = string.Empty,
            Email = string.Empty,
            Motto = (string?)null,
            HeadTeacherName = string.Empty,
            Timezone = SchoolProfile.FixedTimezone,
            IdentityVersionNumber = 0,
            AbbreviationVersionNumber = 0,
            Separator = SchoolProfile.DefaultSeparator,
            SerialWidth = SchoolProfile.DefaultSerialWidth,
            SerialReset = SchoolProfile.DefaultSerialReset,
            RegNumberVersionNumber = 0,
            GradingVersionNumber = 0,
            AssessmentVersionNumber = 0,
        });
    }
}
