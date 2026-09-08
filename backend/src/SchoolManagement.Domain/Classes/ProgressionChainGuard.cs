using SchoolManagement.Domain.Common;

namespace SchoolManagement.Domain.Classes;

/// <summary>
/// The minimal shape <see cref="ProgressionChainGuard"/> needs — never a full <see cref="ClassLevel"/>,
/// so a caller can validate a HYPOTHETICAL post-mutation set without persisting anything first. A
/// top-level type (not nested inside the guard) because CA1034 forbids a nested public type.
/// </summary>
public sealed record ChainLevel(Guid Id, string Name, int ProgressionOrder, Guid? NextLevelId);

/// <summary>
/// The eight progression-chain integrity rules (spec 6.4.2), as a pure function over a set of levels
/// — unit-testable without Postgres, the shape <c>TermTransitionGuard</c> established. Every rule
/// below runs over ACTIVE levels only, as one set (spec 6.4.2's own framing); inactive levels are
/// excluded from the set the caller passes in, and never fail a rule.
/// </summary>
/// <remarks>
/// Rules run in the order spec 6.4.2 lists them (1, 2, 3, 3b, 4, 4b, 5, 6, 7, 8) and the FIRST
/// violation found is returned — spec 6.4.2: "A save that breaks any rule is rejected whole, with ONE
/// message naming the rule and the offending levels."
/// </remarks>
public static class ProgressionChainGuard
{
    /// <summary>
    /// Validates <paramref name="activeLevels"/> against all eight rules.
    /// </summary>
    /// <param name="activeLevels">Every currently-active level, as one set, INCLUDING any not-yet-persisted change.</param>
    /// <param name="allLevelNamesById">
    /// Every level's name (active or inactive), keyed by id — needed only to name the inactive target
    /// in rule 6's rejection message; an id absent here falls back to a generic description.
    /// </param>
    public static Result Validate(
        IReadOnlyList<ChainLevel> activeLevels,
        IReadOnlyDictionary<Guid, string> allLevelNamesById)
    {
        ArgumentNullException.ThrowIfNull(activeLevels);
        ArgumentNullException.ThrowIfNull(allLevelNamesById);

        var byId = activeLevels.ToDictionary(level => level.Id);

        // Rule 1: no level points at itself.
        foreach (var level in activeLevels)
        {
            if (level.NextLevelId == level.Id)
            {
                return Result.Failure(Error.Validation(
                    "level.chain_self_reference",
                    $"{level.Name} cannot be its own next level."));
            }
        }

        // Rule 2: no cycle exists anywhere in the chain.
        var cycle = FindCycle(activeLevels, byId);

        if (cycle is not null)
        {
            return Result.Failure(Error.Validation(
                "level.chain_cycle",
                $"{cycle.Value.From.Name} leads to {cycle.Value.To.Name}, which leads back to " +
                $"{cycle.Value.From.Name}. The progression chain cannot loop."));
        }

        // Rule 3 / 3b: exactly one entry level (nothing active points at it).
        var pointedAt = activeLevels
            .Where(level => level.NextLevelId is not null)
            .Select(level => level.NextLevelId!.Value)
            .ToHashSet();
        var entryCandidates = activeLevels
            .Where(level => !pointedAt.Contains(level.Id))
            .OrderBy(level => level.Name, StringComparer.Ordinal)
            .ToArray();

        if (entryCandidates.Length == 0)
        {
            return Result.Failure(Error.Validation(
                "level.chain_no_entry_level",
                "Every level is pointed at by another, so there is no entry level. Clear the next " +
                "level on whichever level should be last, or remove a pointer into the level that " +
                "should be first."));
        }

        if (entryCandidates.Length > 1)
        {
            return Result.Failure(Error.Validation(
                "level.chain_multiple_entry_levels",
                $"Two levels have nothing leading into them: {JoinNames(entryCandidates)}. Exactly " +
                "one level can be the entry level. Point one of them at from another level, or make " +
                "it follow one."));
        }

        var entry = entryCandidates[0];

        // Rule 4 / 4b: exactly one graduating level (null next level).
        var graduatingCandidates = activeLevels
            .Where(level => level.NextLevelId is null)
            .OrderBy(level => level.Name, StringComparer.Ordinal)
            .ToArray();

        if (graduatingCandidates.Length == 0)
        {
            return Result.Failure(Error.Validation(
                "level.chain_no_graduating_level",
                "Every level has a next level, so there is no graduating level. Clear the next level " +
                "on the final level."));
        }

        if (graduatingCandidates.Length > 1)
        {
            return Result.Failure(Error.Validation(
                "level.chain_multiple_graduating_levels",
                $"Two levels have no next level: {JoinNames(graduatingCandidates)}. Exactly one level " +
                $"can be the graduating level. Point {graduatingCandidates[0].Name} at the level that " +
                "follows it."));
        }

        // Rule 5: every active level is reachable from the entry level.
        var reached = new HashSet<Guid> { entry.Id };
        var cursor = entry;

        while (cursor.NextLevelId is { } nextId && byId.TryGetValue(nextId, out var next) && reached.Add(next.Id))
        {
            cursor = next;
        }

        var unreached = activeLevels
            .Where(level => !reached.Contains(level.Id))
            .OrderBy(level => level.Name, StringComparer.Ordinal)
            .FirstOrDefault();

        if (unreached is not null)
        {
            return Result.Failure(Error.Validation(
                "level.chain_unreachable",
                $"{unreached.Name} cannot be reached from {entry.Name} by following the chain. Every " +
                "level must be reachable from the entry level."));
        }

        // Rule 6: an active level's next_level_id references an active level.
        foreach (var level in activeLevels)
        {
            if (level.NextLevelId is { } targetId && !byId.ContainsKey(targetId))
            {
                var targetName = allLevelNamesById.TryGetValue(targetId, out var name) ? name : "an inactive level";

                return Result.Failure(Error.Validation(
                    "level.chain_points_to_inactive",
                    $"{level.Name} points at {targetName}, which is inactive. Point {level.Name} at " +
                    $"an active level, or reactivate {targetName}."));
            }
        }

        // Rule 7: progression_order is unique across active levels.
        var duplicateOrderGroup = activeLevels
            .GroupBy(level => level.ProgressionOrder)
            .FirstOrDefault(group => group.Count() > 1);

        if (duplicateOrderGroup is not null)
        {
            var pair = duplicateOrderGroup.OrderBy(level => level.Name, StringComparer.Ordinal).ToArray();

            return Result.Failure(Error.Validation(
                "level.chain_order_duplicate",
                $"{pair[0].Name} and {pair[1].Name} both have progression order {duplicateOrderGroup.Key}. " +
                "Each level needs its own position."));
        }

        // Rule 8: progression_order agrees with the chain direction.
        foreach (var level in activeLevels)
        {
            if (level.NextLevelId is not { } nextId || !byId.TryGetValue(nextId, out var next))
            {
                continue;
            }

            if (next.ProgressionOrder <= level.ProgressionOrder)
            {
                return Result.Failure(Error.Validation(
                    "level.chain_order_disagrees_with_direction",
                    $"{next.Name} has progression order {next.ProgressionOrder} but follows " +
                    $"{level.Name}, which has order {level.ProgressionOrder}. Reorder the levels so " +
                    "the numbering matches the chain."));
            }
        }

        return Result.Success();
    }

    /// <summary>
    /// Spec 6.4.2's dedicated deactivation precondition: "Deactivating Primary 3 would leave Primary 4
    /// unreachable. Point Primary 2 at Primary 4 first, then deactivate Primary 3." A SEPARATE check
    /// from <see cref="Validate"/>, run BEFORE it, for the same reason <c>TermTransitionGuard</c> is
    /// separate from <c>TermChronologyGuard</c>: deactivating a genuine middle link and removing a
    /// FRESH level that happens to share no predecessor with anyone are topologically IDENTICAL
    /// invalid states (two disjoint components, each rooted at its own unpointed-at node) — a pure
    /// structural re-check over the resulting set alone cannot tell them apart, and would report
    /// <see cref="Validate"/>'s "multiple entry levels" for both. This check instead asks the
    /// NARROWER, action-specific question deactivation actually needs answered: does
    /// <paramref name="target"/> currently sit between a real predecessor and a real successor that
    /// has no OTHER active predecessor? If so, removing it strands that successor, which is exactly
    /// what spec 6.4.2 asks the message to name. Deactivating an ENTRY level (no predecessor) never
    /// trips this — spec 6.4.2's own worked case (deactivating Nursery 1, 2, 3 in turn) relies on
    /// exactly that: the next level in line simply becomes the new entry.
    /// </summary>
    /// <param name="target">The level being deactivated. Must be present in <paramref name="activeLevels"/>.</param>
    /// <param name="activeLevels">Every currently-active level, INCLUDING <paramref name="target"/> (not yet deactivated).</param>
    public static Result CanDeactivate(ChainLevel target, IReadOnlyList<ChainLevel> activeLevels)
    {
        ArgumentNullException.ThrowIfNull(target);
        ArgumentNullException.ThrowIfNull(activeLevels);

        var predecessor = activeLevels.FirstOrDefault(level => level.Id != target.Id && level.NextLevelId == target.Id);

        if (predecessor is null || target.NextLevelId is not { } successorId)
        {
            return Result.Success();
        }

        var successor = activeLevels.FirstOrDefault(level => level.Id == successorId);

        if (successor is null)
        {
            // target's own successor is already inactive/nonexistent — Validate() (rule 6) is the
            // right place to reject THAT, independent of whether target itself is being deactivated.
            return Result.Success();
        }

        var successorHasAnotherPredecessor = activeLevels
            .Any(level => level.Id != target.Id && level.NextLevelId == successorId);

        if (successorHasAnotherPredecessor)
        {
            return Result.Success();
        }

        return Result.Failure(Error.Conflict(
            "level.deactivate_would_strand_successor",
            $"Deactivating {target.Name} would leave {successor.Name} unreachable. Point " +
            $"{predecessor.Name} at {successor.Name} first, then deactivate {target.Name}."));
    }

    /// <summary>
    /// Standard cycle detection over a functional graph (at most one outgoing edge per node): walks
    /// forward from every not-yet-visited node, tracking the CURRENT walk's node positions separately
    /// from the globally-visited set, so re-entering a node already seen on an earlier, disjoint walk
    /// is never mistaken for a cycle.
    /// </summary>
    private static (ChainLevel From, ChainLevel To)? FindCycle(
        IReadOnlyList<ChainLevel> activeLevels,
        Dictionary<Guid, ChainLevel> byId)
    {
        var visited = new HashSet<Guid>();

        foreach (var start in activeLevels)
        {
            if (visited.Contains(start.Id))
            {
                continue;
            }

            var positionsInWalk = new HashSet<Guid>();
            var path = new List<ChainLevel>();
            var current = start;

            while (true)
            {
                if (positionsInWalk.Contains(current.Id))
                {
                    // The walk always steps via `path[^1].NextLevelId`, so whenever we re-enter a node
                    // already on THIS walk, path[^1] is guaranteed to be the edge that closed the loop.
                    return (path[^1], current);
                }

                if (visited.Contains(current.Id))
                {
                    // Joins a walk a previous, disjoint iteration already fully resolved — not a cycle.
                    break;
                }

                positionsInWalk.Add(current.Id);
                path.Add(current);

                if (current.NextLevelId is not { } nextId || !byId.TryGetValue(nextId, out var next))
                {
                    break;
                }

                current = next;
            }

            foreach (var node in path)
            {
                visited.Add(node.Id);
            }
        }

        return null;
    }

    private static string JoinNames(IReadOnlyList<ChainLevel> levels) =>
        string.Join(" and ", levels.Select(level => level.Name));
}
