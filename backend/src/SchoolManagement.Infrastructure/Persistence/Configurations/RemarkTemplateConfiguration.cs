using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SchoolManagement.Domain.Results;

namespace SchoolManagement.Infrastructure.Persistence.Configurations;

/// <summary>Mapping for <see cref="RemarkTemplate"/> (spec 09 §6.7.7 delta item 4; TASK-0086 stage B).</summary>
internal sealed class RemarkTemplateConfiguration : IEntityTypeConfiguration<RemarkTemplate>
{
    private const int AuditActorMaxLength = 128;
    private const int KindMaxLength = 20;

    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<RemarkTemplate> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("remark_template");

        builder.HasKey(template => template.Id);
        builder.Property(template => template.Id).ValueGeneratedNever();

        builder.Property(template => template.Kind).HasConversion<string>().HasMaxLength(KindMaxLength).IsRequired();
        builder.Property(template => template.Text).HasMaxLength(RemarkTemplate.TextMaxLength).IsRequired();
        builder.Property(template => template.TextKey).HasMaxLength(RemarkTemplate.TextMaxLength).IsRequired();

        builder.Property(template => template.CreatedBy).HasMaxLength(AuditActorMaxLength);
        builder.Property(template => template.ModifiedBy).HasMaxLength(AuditActorMaxLength);

        // Delta item 4: "A duplicate within a kind (trimmed, case-insensitive) is 409" — TextKey is
        // the stored lower-invariant comparison key, so this index enforces it at the database level
        // too, not only at the application check.
        builder.HasIndex(template => new { template.Kind, template.TextKey })
            .IsUnique()
            .HasDatabaseName("ix_remark_template_kind_text_key_unique");

        // No foreign keys: a template's text is COPIED when picked, never referenced (delta item 4)
        // — this is what makes the hard delete safe.
    }
}
