namespace SchoolManagement.Domain.Settings;

/// <summary>
/// Which trait block a <see cref="Trait"/> or <see cref="TraitBlock"/> belongs to (Appendix F.3's two
/// side-by-side blocks, printed with independent E/I/N rating columns). TASK-0072 stage 0 open
/// question 1, human-approved: traits are NOT section-scoped, unlike <see cref="DevelopmentDomain"/> —
/// this enum, not a <c>sectionId</c>, is the whole grouping a trait carries.
/// </summary>
public enum TraitDomain
{
    /// <summary>Appendix F.3's Affective Domain block — Conduct, Punctuality, Honesty, and eight more.</summary>
    Affective = 0,

    /// <summary>Appendix F.3's Psychomotor block — Sports, Social activities, and six more.</summary>
    Psychomotor = 1,
}
