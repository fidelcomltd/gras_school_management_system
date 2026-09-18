using SchoolManagement.Domain.Common;

namespace SchoolManagement.Domain.Settings;

/// <summary>
/// One trait (spec 6.2.7's trait configuration, carried into 6.2.13's "Trait lists replaced"): a
/// single rated row in the affective or psychomotor block on the primary result sheet, for example
/// <c>Punctuality</c>. NOT section-scoped, unlike <see cref="DevelopmentDomain"/> — see
/// <see cref="TraitDomain"/>'s remarks.
/// </summary>
/// <remarks>
/// PERSISTED WHOLE, REPLACED WHOLE, id preserved across a save — the SAME convention
/// <see cref="DevelopmentIndicator"/> established from the start: <c>PUT /settings/traits</c>
/// (<c>ITraitRepository.ReplaceAllAsync</c>) diffs the submitted set against what is persisted, so an
/// existing trait's id never changes under an unrelated edit. This entity carries no optimistic
/// concurrency token of its own — the group's single <see cref="SchoolProfile.TraitsVersionNumber"/>
/// pointer is what a client echoes back.
/// </remarks>
public sealed class Trait : Entity<Guid>
{
    /// <summary>Spec 6.2.7: <c>name</c>, String 60.</summary>
    public const int NameMaxLength = 60;

    // EF Core materialisation constructor.
    private Trait()
        : base()
    {
        Name = null!;
    }

    private Trait(Guid id, TraitDomain domain, string name, int displayOrder, TraitStatus status)
        : base(id)
    {
        Domain = domain;
        Name = name;
        DisplayOrder = displayOrder;
        Status = status;
    }

    /// <summary>Affective or psychomotor (spec 6.2.7). Not a section — see <see cref="TraitDomain"/>.</summary>
    public TraitDomain Domain { get; private set; }

    /// <summary>Up to <see cref="NameMaxLength"/> characters, trimmed. Unique within its domain. For example <c>Punctuality</c>.</summary>
    public string Name { get; private set; }

    /// <summary>Printed row order within its block (spec 6.2.7: "Controls the row order in the trait block on the result sheet").</summary>
    public int DisplayOrder { get; private set; }

    /// <summary>Active or archived. An archived trait leaves new entry screens but stays on historical sheets.</summary>
    public TraitStatus Status { get; private set; }

    /// <summary>
    /// Builds one trait. Trusts its input — the whole-set validator (<see cref="TraitRules"/>) runs
    /// before any trait is constructed, the same convention <see cref="DevelopmentIndicator.Create"/>
    /// follows.
    /// </summary>
    public static Trait Create(Guid id, TraitDomain domain, string name, int displayOrder, TraitStatus status) =>
        new(id, domain, name.Trim(), displayOrder, status);

    /// <summary>
    /// Updates an EXISTING, tracked trait in place, id preserved — same convention as
    /// <see cref="DevelopmentIndicator.Update"/>. Used only by <c>TraitRepository.ReplaceAllAsync</c>
    /// against a row already loaded from the database, whose id a submitted trait echoed back.
    /// </summary>
    public void Update(TraitDomain domain, string name, int displayOrder, TraitStatus status)
    {
        Domain = domain;
        Name = name.Trim();
        DisplayOrder = displayOrder;
        Status = status;
    }
}
