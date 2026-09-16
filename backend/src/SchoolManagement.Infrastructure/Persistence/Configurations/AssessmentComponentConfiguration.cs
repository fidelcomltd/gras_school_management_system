using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SchoolManagement.Domain.Settings;

namespace SchoolManagement.Infrastructure.Persistence.Configurations;

/// <summary>Mapping for <see cref="AssessmentComponent"/> (spec 6.2.6; seed replaced by amendment 6.2.13; TASK-0069).</summary>
internal sealed class AssessmentComponentConfiguration : IEntityTypeConfiguration<AssessmentComponent>
{
    // Fixed, documented seed ids — same convention as SeededClassLevels/GradingBandConfiguration.
    private static readonly Guid FirstCaId = new("00000000-0000-0000-0000-000000000501");
    private static readonly Guid SecondCaId = new("00000000-0000-0000-0000-000000000502");
    private static readonly Guid ExamId = new("00000000-0000-0000-0000-000000000503");

    /// <summary>
    /// The same fixed ids as <see cref="SeedComponents"/> inserts, in the same order as
    /// <see cref="AssessmentStructureSeed.GrasDefaultComponents"/> — <c>internal</c>, not private, so
    /// <c>ApiTestFixture.ResetDatabaseAsync</c> can reinsert the MIGRATION'S OWN rows after a TRUNCATE.
    /// See <c>GradingBandConfiguration.SeededIds</c>'s remarks for why this must not be re-derived any
    /// other way.
    /// </summary>
    internal static readonly IReadOnlyList<Guid> SeededIds = [FirstCaId, SecondCaId, ExamId];

    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<AssessmentComponent> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("assessment_component");

        builder.HasKey(component => component.Id);
        builder.Property(component => component.Id).ValueGeneratedNever();

        builder.Property(component => component.Name)
            .IsRequired()
            .HasMaxLength(AssessmentComponent.NameMaxLength);

        builder.Property(component => component.ShortLabel)
            .IsRequired()
            .HasMaxLength(AssessmentComponent.ShortLabelMaxLength);

        builder.Property(component => component.MaxMark).IsRequired();
        builder.Property(component => component.IsExamination).IsRequired();
        builder.Property(component => component.DisplayOrder).IsRequired();

        SeedComponents(builder);
    }

    /// <summary>Spec 6.2.13: the <c>gras_default</c> profile (1st CA 20, 2nd CA 20, Exam 60) — see <c>AssessmentStructureSeed</c>.</summary>
    private static void SeedComponents(EntityTypeBuilder<AssessmentComponent> builder)
    {
        var seedRows = AssessmentStructureSeed.GrasDefaultComponents
            .Select((component, index) => new
            {
                Id = SeededIds[index],
                component.Name,
                component.ShortLabel,
                component.MaxMark,
                component.IsExamination,
                DisplayOrder = index + 1,
            });

        builder.HasData(seedRows);
    }
}
