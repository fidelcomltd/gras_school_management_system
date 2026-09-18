using System.Globalization;
using SchoolManagement.Domain.Common;

namespace SchoolManagement.Domain.Settings;

/// <summary>
/// One submitted indicator, before persistence. Same "id present means update in place, id
/// preserved; id absent means new" convention <see cref="RatingScalePointInput"/> established
/// (TASK-0072 stage 1 review fix), applied here from the start — see <see cref="DevelopmentIndicator"/>'s
/// remarks for why an indicator's id must never change under an unrelated edit.
/// </summary>
/// <param name="Name">Up to <see cref="DevelopmentIndicator.NameMaxLength"/> characters.</param>
/// <param name="DisplayOrder">Printed order within the domain.</param>
/// <param name="Status">Active or archived. Archiving a submitted id is always allowed, never gated.</param>
/// <param name="Id">
/// The existing indicator's opaque id, when updating one in place. <see langword="null"/> for a new
/// indicator. An id absent from the owning domain's current indicators is rejected
/// <c>422 settings.developmentdomains.unknown_indicator_id</c> (checked against live state, so it
/// lives in <c>UpdateDevelopmentDomainsCommandHandler</c>, not here).
/// </param>
public sealed record DevelopmentIndicatorInput(
    string Name,
    int DisplayOrder,
    DevelopmentIndicatorStatus Status,
    Guid? Id = null);

/// <summary>
/// One submitted domain, before persistence. Same id-stable convention as
/// <see cref="DevelopmentIndicatorInput"/> — see <see cref="DevelopmentDomain"/>'s remarks.
/// </summary>
/// <param name="SectionId">The owning section. Must match an existing <see cref="Classes.Section"/>.</param>
/// <param name="Name">Up to <see cref="DevelopmentDomain.NameMaxLength"/> characters. Unique, case-insensitive, within <paramref name="SectionId"/> across the whole submitted set.</param>
/// <param name="DisplayOrder">Printed block order within the section.</param>
/// <param name="RatingScaleId">The scale this domain's indicators are rated against. Must match an existing <see cref="RatingScale"/>.</param>
/// <param name="AllowsIndicatorComment">Whether the entry screen prints a per-indicator Comments column.</param>
/// <param name="Status">Active or archived. Archiving a submitted id is always allowed, never gated.</param>
/// <param name="Indicators">Every indicator on this domain, in the order they should list.</param>
/// <param name="Id">
/// The existing domain's opaque id, when updating one in place. <see langword="null"/> for a new
/// domain. An id absent from the current set is rejected
/// <c>422 settings.developmentdomains.unknown_domain_id</c> (checked against live state, so it lives
/// in <c>UpdateDevelopmentDomainsCommandHandler</c>, not here).
/// </param>
public sealed record DevelopmentDomainInput(
    Guid SectionId,
    string Name,
    int DisplayOrder,
    Guid RatingScaleId,
    bool AllowsIndicatorComment,
    DevelopmentDomainStatus Status,
    IReadOnlyList<DevelopmentIndicatorInput> Indicators,
    Guid? Id = null);

/// <summary>
/// A development-domain rejection carrying WHICH submitted domain (and, where the failure is about
/// one indicator, which indicator within it) failed — same shape as
/// <see cref="RatingScaleValidationError"/>'s <c>ScaleIndex</c>/<c>PointIndex</c>.
/// </summary>
/// <param name="Code">Stable error code.</param>
/// <param name="Description">The rule-specific message.</param>
/// <param name="DomainIndex">0-based position of the offending domain in the submitted array. <c>-1</c> when the failure names no single domain.</param>
/// <param name="IndicatorIndex">0-based position of the offending indicator within that domain, when the failure is about one indicator. <see langword="null"/> otherwise.</param>
public sealed record DevelopmentDomainValidationError(string Code, string Description, int DomainIndex, int? IndicatorIndex)
    : Error(Code, Description, ErrorType.Validation);

/// <summary>
/// Save-time STRUCTURAL validation rules for the whole submitted development-domain set (spec
/// 6.2.13's "rules, following the trait rules already in 6.2.7"), run as one unit exactly as
/// <see cref="RatingScaleRules"/> validates a whole rating-scale set — a partially valid set is never
/// saved, and the first failure in the order below is the one returned. Checks that need live
/// database state (an unknown section, rating-scale, domain or indicator id; the indicator-rated
/// removal gate) are NOT here — those live in <c>UpdateDevelopmentDomainsCommandHandler</c>, the same
/// split <see cref="RatingScaleRules"/> keeps from its own handler.
/// </summary>
public static class DevelopmentDomainRules
{
    /// <summary>Duplicate domain name within the same section.</summary>
    public const string DuplicateNameCode = "settings.developmentdomains.duplicate_name";

    /// <summary>Duplicate indicator name within a domain.</summary>
    public const string DuplicateIndicatorNameCode = "settings.developmentdomains.duplicate_indicator_name";

    /// <summary>
    /// Validates <paramref name="domains"/> against every structural rule, in order, stopping at the
    /// first failure. A successful <see cref="Result"/> means the whole set may proceed to the
    /// handler's database-dependent checks.
    /// </summary>
    public static Result ValidateWholeSet(IReadOnlyList<DevelopmentDomainInput> domains)
    {
        ArgumentNullException.ThrowIfNull(domains);

        // Rule 1: indicator names unique within a domain, case-insensitive — checked per domain first,
        // same "inner rule before outer rule" ordering RatingScaleRules uses for point-level rules.
        for (var domainIndex = 0; domainIndex < domains.Count; domainIndex++)
        {
            var domain = domains[domainIndex];
            var indicators = domain.Indicators ?? [];
            var seenIndicatorNames = new Dictionary<string, int>(StringComparer.OrdinalIgnoreCase);

            for (var indicatorIndex = 0; indicatorIndex < indicators.Count; indicatorIndex++)
            {
                var name = indicators[indicatorIndex].Name;
                if (name is not null && seenIndicatorNames.ContainsKey(name))
                {
                    return Fail(
                        DuplicateIndicatorNameCode,
                        Invariant($"Domain {domain.Name} uses the indicator name {name} more than once. Indicator names must be unique within a domain."),
                        domainIndex,
                        indicatorIndex);
                }

                if (name is not null)
                {
                    seenIndicatorNames[name] = indicatorIndex;
                }
            }
        }

        // Rule 2: domain names unique WITHIN THE SAME SECTION, case-insensitive — the same name is
        // allowed in different sections (spec 6.2.13: domains are section-scoped).
        var seenNamesBySection = new Dictionary<(Guid SectionId, string NameKey), int>();
        for (var domainIndex = 0; domainIndex < domains.Count; domainIndex++)
        {
            var domain = domains[domainIndex];
            if (domain.Name is null)
            {
                continue;
            }

            var key = (domain.SectionId, domain.Name.ToUpperInvariant());
            if (seenNamesBySection.ContainsKey(key))
            {
                return Fail(
                    DuplicateNameCode,
                    Invariant($"Two domains in the same section are named {domain.Name}. Domain names must be unique within a section."),
                    domainIndex,
                    null);
            }

            seenNamesBySection[key] = domainIndex;
        }

        return Result.Success();
    }

    private static Result Fail(string code, string description, int domainIndex, int? indicatorIndex) =>
        Result.Failure(new DevelopmentDomainValidationError(code, description, domainIndex, indicatorIndex));

    private static string Invariant(FormattableString formattable) =>
        formattable.ToString(CultureInfo.InvariantCulture);
}
