using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SchoolManagement.Domain.Pupils;

namespace SchoolManagement.Infrastructure.Persistence.Configurations;

/// <summary>Mapping for <see cref="PupilContact"/> (spec 6.5.5). One row per role per pupil.</summary>
internal sealed class PupilContactConfiguration : IEntityTypeConfiguration<PupilContact>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<PupilContact> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        builder.ToTable("pupil_contact");
        builder.HasKey(contact => contact.Id);
        builder.Property(contact => contact.Id).ValueGeneratedNever();
        builder.Property(contact => contact.Role).IsRequired().HasConversion<string>().HasMaxLength(24);
        builder.Property(contact => contact.FullName).IsRequired().HasMaxLength(PersonFields.FullNameMaxLength);
        builder.Property(contact => contact.Relationship).HasMaxLength(PersonFields.RelationshipMaxLength);
        builder.Property(contact => contact.Phone).IsRequired().HasMaxLength(20);
        builder.Property(contact => contact.WhatsappNumber).HasMaxLength(20);
        builder.Property(contact => contact.Occupation).HasMaxLength(PupilContact.OccupationMaxLength);
        builder.Property(contact => contact.Email).HasMaxLength(PupilContact.EmailMaxLength);
        builder.Property(contact => contact.CreatedBy).HasMaxLength(128);
        builder.Property(contact => contact.ModifiedBy).HasMaxLength(128);
        builder.HasIndex(contact => new { contact.PupilId, contact.Role }).IsUnique().HasDatabaseName("ix_pupil_contact_pupil_role_unique");

        // Spec 6.5.15: a shared phone finds families.
        builder.HasIndex(contact => contact.Phone).HasDatabaseName("ix_pupil_contact_phone");
        builder.HasOne<Pupil>().WithMany().HasForeignKey(contact => contact.PupilId).OnDelete(DeleteBehavior.Restrict);
    }
}

/// <summary>Mapping for <see cref="AuthorisedPickupPerson"/> (spec 6.5.6).</summary>
internal sealed class AuthorisedPickupPersonConfiguration : IEntityTypeConfiguration<AuthorisedPickupPerson>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<AuthorisedPickupPerson> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        builder.ToTable("authorised_pickup_person");
        builder.HasKey(person => person.Id);
        builder.Property(person => person.Id).ValueGeneratedNever();
        builder.Property(person => person.FullName).IsRequired().HasMaxLength(PersonFields.FullNameMaxLength);
        builder.Property(person => person.Relationship).IsRequired().HasMaxLength(PersonFields.RelationshipMaxLength);
        builder.Property(person => person.Phone).IsRequired().HasMaxLength(20);
        builder.Property(person => person.CreatedBy).HasMaxLength(128);
        builder.Property(person => person.ModifiedBy).HasMaxLength(128);
        builder.HasIndex(person => person.PupilId).HasDatabaseName("ix_authorised_pickup_person_pupil");
        builder.HasOne<Pupil>().WithMany().HasForeignKey(person => person.PupilId).OnDelete(DeleteBehavior.Restrict);
    }
}

/// <summary>Mapping for <see cref="BarredPersonAnswer"/> (spec 6.5.6). Keyed by pupil.</summary>
internal sealed class BarredPersonAnswerConfiguration : IEntityTypeConfiguration<BarredPersonAnswer>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<BarredPersonAnswer> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        builder.ToTable("barred_person_answer");
        builder.HasKey(answer => answer.PupilId);
        builder.Property(answer => answer.PupilId).ValueGeneratedNever();
        builder.Property(answer => answer.CreatedBy).HasMaxLength(128);
        builder.Property(answer => answer.ModifiedBy).HasMaxLength(128);
        builder.HasOne<Pupil>().WithOne().HasForeignKey<BarredPersonAnswer>(answer => answer.PupilId).OnDelete(DeleteBehavior.Restrict);
    }
}

/// <summary>Mapping for <see cref="BarredPerson"/> (spec 6.5.6).</summary>
internal sealed class BarredPersonConfiguration : IEntityTypeConfiguration<BarredPerson>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<BarredPerson> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        builder.ToTable("barred_person");
        builder.HasKey(person => person.Id);
        builder.Property(person => person.Id).ValueGeneratedNever();
        builder.Property(person => person.FullName).IsRequired().HasMaxLength(PersonFields.FullNameMaxLength);
        builder.Property(person => person.Details).HasMaxLength(BarredPerson.DetailsMaxLength);
        builder.Property(person => person.CreatedBy).HasMaxLength(128);
        builder.Property(person => person.ModifiedBy).HasMaxLength(128);
        builder.HasIndex(person => person.PupilId).HasDatabaseName("ix_barred_person_pupil");
        builder.HasOne<Pupil>().WithMany().HasForeignKey(person => person.PupilId).OnDelete(DeleteBehavior.Restrict);
    }
}

/// <summary>Mapping for <see cref="PupilHealth"/> (spec 6.5.7). Keyed by pupil.</summary>
internal sealed class PupilHealthConfiguration : IEntityTypeConfiguration<PupilHealth>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<PupilHealth> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        builder.ToTable("pupil_health");
        builder.HasKey(health => health.PupilId);
        builder.Property(health => health.PupilId).ValueGeneratedNever();
        builder.Property(health => health.AllergyDetails).HasMaxLength(PupilHealth.ShortDetailsMaxLength);
        builder.Property(health => health.MedicalConditionDetails).HasMaxLength(PupilHealth.LongDetailsMaxLength);
        builder.Property(health => health.MedicationDetails).HasMaxLength(PupilHealth.ShortDetailsMaxLength);
        builder.Property(health => health.SpecialInstructions).HasMaxLength(PupilHealth.LongDetailsMaxLength);
        builder.Property(health => health.PreferredHospital).HasMaxLength(PupilHealth.HospitalMaxLength);
        builder.Property(health => health.HospitalPhone).HasMaxLength(20);
        builder.Property(health => health.BloodGroup).HasConversion<string>().HasMaxLength(16);
        builder.Property(health => health.Genotype).HasConversion<string>().HasMaxLength(8);
        builder.Property(health => health.CreatedBy).HasMaxLength(128);
        builder.Property(health => health.ModifiedBy).HasMaxLength(128);
        builder.HasOne<Pupil>().WithOne().HasForeignKey<PupilHealth>(health => health.PupilId).OnDelete(DeleteBehavior.Restrict);
    }
}

/// <summary>Mapping for <see cref="PupilDocument"/> (spec 6.5.8). One row per type per pupil.</summary>
internal sealed class PupilDocumentConfiguration : IEntityTypeConfiguration<PupilDocument>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<PupilDocument> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);
        builder.ToTable("pupil_document");
        builder.HasKey(document => document.Id);
        builder.Property(document => document.Id).ValueGeneratedNever();
        builder.Property(document => document.DocumentType).IsRequired().HasConversion<string>().HasMaxLength(32);
        builder.Property(document => document.OtherLabel).HasMaxLength(PupilDocument.OtherLabelMaxLength);
        builder.Property(document => document.Remarks).HasMaxLength(PupilDocument.RemarksMaxLength);
        builder.Property(document => document.ReceivedBy).HasMaxLength(128);
        builder.Property(document => document.FileAssetId).HasMaxLength(Domain.Settings.SchoolImage.AssetIdMaxLength);
        builder.Property(document => document.FileContentType).HasMaxLength(Domain.Settings.SchoolImage.ContentTypeMaxLength);
        builder.Property(document => document.FileUploadedBy).HasMaxLength(128);
        builder.Property(document => document.CreatedBy).HasMaxLength(128);
        builder.Property(document => document.ModifiedBy).HasMaxLength(128);
        builder.HasIndex(document => new { document.PupilId, document.DocumentType }).IsUnique().HasDatabaseName("ix_pupil_document_pupil_type_unique");
        builder.HasOne<Pupil>().WithMany().HasForeignKey(document => document.PupilId).OnDelete(DeleteBehavior.Restrict);
    }
}
