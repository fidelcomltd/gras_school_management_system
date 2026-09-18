using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SchoolManagement.Domain.Sessions;

namespace SchoolManagement.Infrastructure.Persistence.Configurations;

/// <summary>Mapping for <see cref="Term"/> (spec 6.3.4).</summary>
internal sealed class TermConfiguration : IEntityTypeConfiguration<Term>
{
    /// <summary>Maximum length of the stored audit-actor column, matching <c>RoleConfiguration</c>.</summary>
    private const int AuditActorMaxLength = 128;

    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<Term> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("terms");

        builder.HasKey(term => term.Id);

        builder.Property(term => term.Id).ValueGeneratedNever();

        builder.Property(term => term.SessionId).IsRequired();
        builder.Property(term => term.Ordinal).IsRequired();

        builder.Property(term => term.Name)
            .IsRequired()
            .HasMaxLength(Term.NameMaxLength);

        builder.Property(term => term.StartDate).IsRequired();
        builder.Property(term => term.EndDate).IsRequired();
        builder.Property(term => term.TimesSchoolOpened);
        builder.Property(term => term.NextResumptionDate);

        builder.Property(term => term.State)
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(20);

        builder.Property(term => term.ClosedAtUtc);
        builder.Property(term => term.ClosedBy).HasMaxLength(AuditActorMaxLength);

        builder.Property(term => term.CreatedBy).HasMaxLength(AuditActorMaxLength);
        builder.Property(term => term.ModifiedBy).HasMaxLength(AuditActorMaxLength);

        // Exactly three terms per session (spec 6.3.4); ordinal is unique within the session so a
        // sibling lookup by (session, ordinal) is always at most one row.
        builder.HasIndex(term => new { term.SessionId, term.Ordinal })
            .IsUnique()
            .HasDatabaseName("ix_terms_session_ordinal_unique");

        // Spec 6.3.9: "Two terms marked active by a data error [is] prevented by a partial unique
        // index across all terms where state is active. The database, not the application, enforces
        // one active term." Same technique as AcademicSessionConfiguration's own singleness index.
        builder.HasIndex(term => term.State)
            .IsUnique()
            .HasFilter("state = 'Active'")
            .HasDatabaseName("ix_terms_single_active");
    }
}
