using System.Globalization;
using SchoolManagement.Domain.Common;

namespace SchoolManagement.Domain.Settings;

/// <summary>
/// One submitted trait, before persistence. Same "id present means update in place, id preserved; id
/// absent means new" convention <see cref="DevelopmentIndicatorInput"/> established, applied here from
/// the start (TASK-0072 stage 3's own instruction: "Ids on traits are optional and preserved,
/// id-stable from day one").
/// </summary>
/// <param name="Domain">Affective or psychomotor. NOT a section — see <see cref="TraitDomain"/>.</param>
/// <param name="Name">Up to <see cref="Trait.NameMaxLength"/> characters. Unique, case-insensitive, within <paramref name="Domain"/> across the whole submitted set.</param>
/// <param name="DisplayOrder">Printed row order within the block.</param>
/// <param name="Status">Active or archived. Archiving a submitted id is always allowed, never gated.</param>
/// <param name="Id">
/// The existing trait's opaque id, when updating one in place. <see langword="null"/> for a new trait.
/// An id absent from the current set is rejected <c>422 settings.traits.unknown_trait_id</c> (checked
/// against live state, so it lives in <c>UpdateTraitsCommandHandler</c>, not here).
/// </param>
public sealed record TraitInput(TraitDomain Domain, string Name, int DisplayOrder, TraitStatus Status, Guid? Id = null);

/// <summary>
/// A trait rejection carrying WHICH submitted trait failed — same shape as
/// <see cref="RatingScaleValidationError"/>'s <c>ScaleIndex</c>, so the editor can highlight the
/// offending row without re-parsing the message.
/// </summary>
/// <param name="Code">Stable error code.</param>
/// <param name="Description">The rule-specific message.</param>
/// <param name="TraitIndex">0-based position of the offending trait in the submitted array.</param>
public sealed record TraitValidationError(string Code, string Description, int TraitIndex)
    : Error(Code, Description, ErrorType.Validation);

/// <summary>
/// Save-time STRUCTURAL validation rules for the whole submitted trait set (spec 6.2.7: "Unique
/// within its domain"), run as one unit exactly as <see cref="DevelopmentDomainRules"/> validates a
/// whole development-domain set — a partially valid set is never saved. Checks that need live database
/// state (an unknown scale or trait id; the rated-trait removal gate) are NOT here — those live in
/// <c>UpdateTraitsCommandHandler</c>, the same split <see cref="DevelopmentDomainRules"/> keeps from
/// its own handler.
/// </summary>
public static class TraitRules
{
    /// <summary>Duplicate trait name within the same domain.</summary>
    public const string DuplicateNameCode = "settings.traits.duplicate_name";

    /// <summary>
    /// Validates <paramref name="traits"/> against every structural rule. A successful
    /// <see cref="Result"/> means the whole set may proceed to the handler's database-dependent checks.
    /// </summary>
    public static Result ValidateWholeSet(IReadOnlyList<TraitInput> traits)
    {
        ArgumentNullException.ThrowIfNull(traits);

        // Trait names unique WITHIN THE SAME DOMAIN, case-insensitive — the same name is allowed in
        // the other domain (spec 6.2.7 scopes uniqueness to "its domain").
        var seenNamesByDomain = new Dictionary<(TraitDomain Domain, string NameKey), int>();
        for (var index = 0; index < traits.Count; index++)
        {
            var trait = traits[index];
            if (trait.Name is null)
            {
                continue;
            }

            var key = (trait.Domain, trait.Name.ToUpperInvariant());
            if (seenNamesByDomain.ContainsKey(key))
            {
                return Fail(
                    DuplicateNameCode,
                    Invariant($"Two traits in the {trait.Domain} domain are named {trait.Name}. Trait names must be unique within a domain."),
                    index);
            }

            seenNamesByDomain[key] = index;
        }

        return Result.Success();
    }

    private static Result Fail(string code, string description, int traitIndex) =>
        Result.Failure(new TraitValidationError(code, description, traitIndex));

    private static string Invariant(FormattableString formattable) =>
        formattable.ToString(CultureInfo.InvariantCulture);
}
