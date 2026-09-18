using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.ChangeTracking;
using Microsoft.EntityFrameworkCore.Metadata.Builders;
using SchoolManagement.Domain.Settings;

namespace SchoolManagement.Infrastructure.Persistence.Configurations;

/// <summary>Mapping for the <see cref="ResultRules"/> singleton (spec 6.2.8; TASK-0077).</summary>
internal sealed class ResultRulesConfiguration : IEntityTypeConfiguration<ResultRules>
{
    /// <inheritdoc />
    public void Configure(EntityTypeBuilder<ResultRules> builder)
    {
        ArgumentNullException.ThrowIfNull(builder);

        builder.ToTable("result_rules");

        builder.HasKey(resultRules => resultRules.Id);
        builder.Property(resultRules => resultRules.Id).ValueGeneratedNever();

        builder.Property(resultRules => resultRules.AnnualMethod)
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(20);

        builder.Property(resultRules => resultRules.WeightFirst);
        builder.Property(resultRules => resultRules.WeightSecond);
        builder.Property(resultRules => resultRules.WeightThird);

        builder.Property(resultRules => resultRules.PrimaryPositionScope)
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(10);

        builder.Property(resultRules => resultRules.ShowLevelPosition).IsRequired();

        builder.Property(resultRules => resultRules.TieBreakRule)
            .IsRequired()
            .HasConversion<string>()
            .HasMaxLength(30);

        builder.Property(resultRules => resultRules.PassMark).IsRequired();
        builder.Property(resultRules => resultRules.PromotionThreshold).IsRequired();
        builder.Property(resultRules => resultRules.RequireCorePass).IsRequired();

        // Comma-joined text, same technique as RoleAssignmentConfiguration.ArmIds — a fixed-shape
        // GUID list needs no array value-conversion support from the provider, and this list is
        // admin-configuration-sized (a handful of core subjects at most).
        var coreSubjectIdsProperty = builder
            .Property(resultRules => resultRules.CoreSubjectIds)
            .HasField("_coreSubjectIds")
            .UsePropertyAccessMode(PropertyAccessMode.Field)
            .HasColumnName("core_subject_ids")
            .HasColumnType("text")
            .HasConversion(
                coreSubjectIds => string.Join(',', coreSubjectIds.Select(id => id.ToString("D"))),
                text => string.IsNullOrEmpty(text)
                    ? new List<Guid>()
                    : text.Split(',', StringSplitOptions.RemoveEmptyEntries).Select(Guid.Parse).ToList());

        coreSubjectIdsProperty.Metadata.SetValueComparer(
            new ValueComparer<IReadOnlyList<Guid>>(
                (left, right) => (left ?? Array.Empty<Guid>()).SequenceEqual(right ?? Array.Empty<Guid>()),
                value => value.Aggregate(0, (hash, item) => HashCode.Combine(hash, item.GetHashCode())),
                value => value.ToList()));

        builder.Property(resultRules => resultRules.MinSubjectsForPosition).IsRequired();

        // Spec 6.2.8: the seeded defaults. coreSubjectIds seeds EMPTY — "set by the administrator
        // once subjects exist" (see the entity's own remarks for why this is never guessed).
        builder.HasData(new
        {
            Id = ResultRules.SingletonId,
            AnnualMethod = ResultRules.DefaultAnnualMethod,
            WeightFirst = (int?)null,
            WeightSecond = (int?)null,
            WeightThird = (int?)null,
            PrimaryPositionScope = ResultRules.DefaultPrimaryPositionScope,
            ShowLevelPosition = ResultRules.DefaultShowLevelPosition,
            TieBreakRule = ResultRules.DefaultTieBreakRule,
            PassMark = ResultRules.DefaultPassMark,
            PromotionThreshold = ResultRules.DefaultPromotionThreshold,
            RequireCorePass = ResultRules.DefaultRequireCorePass,
            // Cast to the CLR property type, not the raw stored column value — HasData applies the
            // conversion above internally; a bare `new List<Guid>()` mismatches the property's declared
            // IReadOnlyList<Guid> type and EF throws building the model.
            CoreSubjectIds = (IReadOnlyList<Guid>)new List<Guid>(),
            MinSubjectsForPosition = ResultRules.DefaultMinSubjectsForPosition,
        });
    }
}
