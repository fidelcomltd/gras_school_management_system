using SchoolManagement.Domain.Classes;

namespace SchoolManagement.UnitTests.Domain.Classes;

/// <summary>
/// Spec 6.4.2's eight progression-chain rules, as pure decisions — no database, mirroring
/// <c>TermTransitionGuardTests</c>'s shape.
/// </summary>
/// <remarks>
/// <para>
/// Rules 4 (the PLURAL "multiple graduating levels" case), 5 and 6 are PROVEN, not merely observed,
/// to be logically IMPLIED by rule 3 in this pointer model (each level carries AT MOST one outgoing
/// <c>NextLevelId</c>). The counting argument: with <c>k</c> graduating (null-next) levels among
/// <c>N</c> total, exactly <c>N-k</c> levels emit an outgoing pointer; for EVERY non-entry level to
/// have at least one incoming pointer (the "exactly one entry" requirement, rule 3), those <c>N-k</c>
/// edges must cover all <c>N-1</c> non-entry levels, which needs <c>N-k &gt;= N-1</c>, i.e.
/// <c>k &lt;= 1</c>. So <c>k &gt;= 2</c> (rule 4's plural case) ALWAYS leaves at least one level
/// uncovered — a second entry candidate — meaning rule 3 fires first, every time. The same counting
/// argument, run the other direction (every node's backward-predecessor chain must terminate at the
/// UNIQUE entry, or it would itself be a second entry or a cycle), proves rule 5 (reachability) and
/// rule 6 (an active pointer must target an active level) also always coincide with a rule-3
/// violation the moment they would otherwise fire. A genuinely isolated failure of any of these three
/// is therefore NOT constructible through this pure function's public contract; every attempt below
/// is instead caught by rule 3, and the three "IsImpliedByRule3" tests prove that finding with
/// evidence rather than asserting it. All three checks remain in <see cref="ProgressionChainGuard"/>
/// as defense-in-depth and to match spec 6.4.2's literal enumeration;
/// <see cref="ValidChain_WithNoViolations_Succeeds"/> proves none of them ever FALSELY rejects a
/// genuinely valid chain.
/// </para>
/// <para>
/// Rule 4b (the SINGULAR "no graduating level" case, <c>k = 0</c>) is NOT subject to this proof —
/// with zero graduating levels, ALL <c>N</c> levels emit a pointer, a surplus over the <c>N-1</c>
/// needed, so one pointer can point OUTSIDE the active set (simulating a dangling reference to a
/// removed successor) without leaving anything uncovered. <see cref="Rule4b_NoGraduatingLevel_Rejected"/>
/// demonstrates exactly that, genuinely isolated.
/// </para>
/// </remarks>
public sealed class ProgressionChainGuardTests
{
    private static readonly IReadOnlyDictionary<Guid, string> NoInactiveContext = new Dictionary<Guid, string>();

    [Fact]
    public void ValidChain_WithNoViolations_Succeeds()
    {
        var a = Guid.CreateVersion7();
        var b = Guid.CreateVersion7();
        var c = Guid.CreateVersion7();

        var levels = new[]
        {
            new ChainLevel(a, "Nursery 1", 1, b),
            new ChainLevel(b, "Nursery 2", 2, c),
            new ChainLevel(c, "Nursery 3", 3, null),
        };

        var result = ProgressionChainGuard.Validate(levels, NoInactiveContext);

        result.IsSuccess.ShouldBeTrue(result.IsFailure ? result.Error.Description : string.Empty);
    }

    [Fact]
    public void Rule1_SelfReference_Rejected()
    {
        var a = Guid.CreateVersion7();
        var levels = new[] { new ChainLevel(a, "Primary 4", 1, a) };

        var result = ProgressionChainGuard.Validate(levels, NoInactiveContext);

        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe("level.chain_self_reference");
        result.Error.Description.ShouldBe("Primary 4 cannot be its own next level.");
    }

    // Spec 6.4.2's own example: "Primary 3 leads to Primary 4, which leads back to Primary 3."
    [Fact]
    public void Rule2_Cycle_Rejected()
    {
        var p3 = Guid.CreateVersion7();
        var p4 = Guid.CreateVersion7();

        // Declared p4-first deliberately: FindCycle walks in array order, and the walk that starts
        // at p4 is the one whose closing edge (p3 -> p4) matches spec 6.4.2's own worded example
        // below — starting at p3 instead would produce the equally-correct but differently-worded
        // "Primary 4 leads to Primary 3, which leads back to Primary 4."
        var levels = new[]
        {
            new ChainLevel(p4, "Primary 4", 2, p3),
            new ChainLevel(p3, "Primary 3", 1, p4),
        };

        var result = ProgressionChainGuard.Validate(levels, NoInactiveContext);

        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe("level.chain_cycle");
        result.Error.Description.ShouldBe(
            "Primary 3 leads to Primary 4, which leads back to Primary 3. The progression chain cannot loop.");
    }

    [Fact]
    public void Rule3_MultipleEntryLevels_Rejected()
    {
        var nursery1 = Guid.CreateVersion7();
        var reception = Guid.CreateVersion7();
        var primary1 = Guid.CreateVersion7();

        // Nothing points at Nursery 1 or Reception — both are entry candidates.
        var levels = new[]
        {
            new ChainLevel(nursery1, "Nursery 1", 1, primary1),
            new ChainLevel(reception, "Reception", 2, primary1),
            new ChainLevel(primary1, "Primary 1", 3, null),
        };

        var result = ProgressionChainGuard.Validate(levels, NoInactiveContext);

        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe("level.chain_multiple_entry_levels");
        result.Error.Description.ShouldContain("Nursery 1 and Reception");
    }

    // The empty set is the only construction that reaches rule 3b through the public Validate()
    // contract without ALSO tripping rule 2 first: a non-empty "everybody is pointed at" topology is,
    // by construction, a union of cycles (every node in-degree >= 1 with out-degree <= 1, over a
    // finite set, must decompose into cycles), which rule 2 always catches first.
    [Fact]
    public void Rule3b_NoEntryLevel_Rejected()
    {
        var result = ProgressionChainGuard.Validate([], NoInactiveContext);

        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe("level.chain_no_entry_level");
    }

    // See the class remarks for the counting proof. The simplest natural attempt at "two graduating
    // levels" — two isolated single levels, each with nothing pointing at it and no next level of its
    // own — is ALSO, simultaneously, "two entry levels." Rule 3 runs first and wins.
    [Fact]
    public void Rule4_MultipleGraduatingLevels_IsImpliedByRule3()
    {
        var a = Guid.CreateVersion7();
        var b = Guid.CreateVersion7();

        var levels = new[]
        {
            new ChainLevel(a, "Nursery 3", 1, null),
            new ChainLevel(b, "Primary 6", 2, null),
        };

        var result = ProgressionChainGuard.Validate(levels, NoInactiveContext);

        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe("level.chain_multiple_entry_levels");
    }

    // A non-null pointer to something OUTSIDE the active set (simulating a level whose successor was
    // removed without updating this pointer) is the only way to reach 4b without also tripping 3 —
    // see the class remarks for why 4b, unlike plural rule 4, has a surplus of edges available.
    [Fact]
    public void Rule4b_NoGraduatingLevel_Rejected()
    {
        var a = Guid.CreateVersion7();
        var b = Guid.CreateVersion7();
        var escapesOutsideActiveSet = Guid.CreateVersion7();

        var levels = new[]
        {
            new ChainLevel(a, "Nursery 1", 1, b),
            new ChainLevel(b, "Nursery 2", 2, escapesOutsideActiveSet),
        };

        var result = ProgressionChainGuard.Validate(levels, NoInactiveContext);

        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe("level.chain_no_graduating_level");
    }

    // See the class remarks: the natural real-world way rule 5/6 violations arise — deactivating a
    // level without rewiring what pointed at it, spec 6.4.2's own worked example ("Primary 3 points
    // at Reception, which is inactive") — orphans the level Reception WOULD have led to (here,
    // Primary 1), which loses its only incoming pointer and becomes a SECOND entry candidate. Rule 3
    // fires first, every time, for both the unreachability (5) and the points-to-inactive (6) framing
    // of the identical broken edge.
    [Fact]
    public void Rule5And6_UnreachableAndPointsToInactive_AreImpliedByRule3()
    {
        var nursery1 = Guid.CreateVersion7();
        var nursery2 = Guid.CreateVersion7();
        var nursery3 = Guid.CreateVersion7();
        var reception = Guid.CreateVersion7(); // deactivated — deliberately NOT in `levels` below.
        var primary1 = Guid.CreateVersion7();

        var levels = new[]
        {
            new ChainLevel(nursery1, "Nursery 1", 1, nursery2),
            new ChainLevel(nursery2, "Nursery 2", 2, nursery3),
            // Still points at the now-inactive Reception instead of Primary 1 — nobody rewired it.
            new ChainLevel(nursery3, "Nursery 3", 3, reception),
            new ChainLevel(primary1, "Primary 1", 4, null),
        };

        var namesById = new Dictionary<Guid, string> { [reception] = "Reception" };

        var result = ProgressionChainGuard.Validate(levels, namesById);

        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe("level.chain_multiple_entry_levels");
    }

    [Fact]
    public void Rule7_DuplicateProgressionOrder_Rejected()
    {
        var a = Guid.CreateVersion7();
        var b = Guid.CreateVersion7();
        var c = Guid.CreateVersion7();

        var levels = new[]
        {
            new ChainLevel(a, "Primary 1", 1, b),
            new ChainLevel(b, "Primary 2", 5, c),
            new ChainLevel(c, "Primary 3", 5, null),
        };

        var result = ProgressionChainGuard.Validate(levels, NoInactiveContext);

        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe("level.chain_order_duplicate");
        result.Error.Description.ShouldBe(
            "Primary 2 and Primary 3 both have progression order 5. Each level needs its own position.");
    }

    // Spec 6.4.2's own example: "Primary 4 has progression order 6 but follows Primary 5, which has order 8."
    [Fact]
    public void Rule8_OrderDisagreesWithChainDirection_Rejected()
    {
        var primary5 = Guid.CreateVersion7();
        var primary4 = Guid.CreateVersion7();

        var levels = new[]
        {
            new ChainLevel(primary5, "Primary 5", 8, primary4),
            new ChainLevel(primary4, "Primary 4", 6, null),
        };

        var result = ProgressionChainGuard.Validate(levels, NoInactiveContext);

        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe("level.chain_order_disagrees_with_direction");
        result.Error.Description.ShouldBe(
            "Primary 4 has progression order 6 but follows Primary 5, which has order 8. Reorder the " +
            "levels so the numbering matches the chain.");
    }

    // Spec 6.4.2's own worked deactivation example, verbatim: "Deactivating Primary 3 would leave
    // Primary 4 unreachable. Point Primary 2 at Primary 4 first, then deactivate Primary 3." This is
    // the scenario Rule5And6_UnreachableAndPointsToInactive_AreImpliedByRule3 shows Validate() cannot
    // give this exact wording for — CanDeactivate is the dedicated fix; see its own remarks.
    [Fact]
    public void CanDeactivate_MidChainLevel_RejectsNamingPredecessorAndSuccessor()
    {
        var primary2 = Guid.CreateVersion7();
        var primary3 = Guid.CreateVersion7();
        var primary4 = Guid.CreateVersion7();

        var levels = new[]
        {
            new ChainLevel(primary2, "Primary 2", 1, primary3),
            new ChainLevel(primary3, "Primary 3", 2, primary4),
            new ChainLevel(primary4, "Primary 4", 3, null),
        };

        var target = levels.Single(level => level.Id == primary3);
        var result = ProgressionChainGuard.CanDeactivate(target, levels);

        result.IsFailure.ShouldBeTrue();
        result.Error.Code.ShouldBe("level.deactivate_would_strand_successor");
        result.Error.Description.ShouldBe(
            "Deactivating Primary 3 would leave Primary 4 unreachable. Point Primary 2 at Primary 4 " +
            "first, then deactivate Primary 3.");
    }

    // Spec 6.4.2: deactivating the ENTRY level (Nursery 1) is explicitly allowed — the next level in
    // line simply becomes the new entry. CanDeactivate must not trip on "no predecessor."
    [Fact]
    public void CanDeactivate_TheEntryLevel_Succeeds()
    {
        var nursery1 = Guid.CreateVersion7();
        var nursery2 = Guid.CreateVersion7();

        var levels = new[]
        {
            new ChainLevel(nursery1, "Nursery 1", 1, nursery2),
            new ChainLevel(nursery2, "Nursery 2", 2, null),
        };

        var target = levels.Single(level => level.Id == nursery1);
        var result = ProgressionChainGuard.CanDeactivate(target, levels);

        result.IsSuccess.ShouldBeTrue();
    }

    // Spec 6.4.2: deactivating the GRADUATING level (nothing downstream to strand) is allowed.
    [Fact]
    public void CanDeactivate_TheGraduatingLevel_Succeeds()
    {
        var nursery1 = Guid.CreateVersion7();
        var nursery2 = Guid.CreateVersion7();

        var levels = new[]
        {
            new ChainLevel(nursery1, "Nursery 1", 1, nursery2),
            new ChainLevel(nursery2, "Nursery 2", 2, null),
        };

        var target = levels.Single(level => level.Id == nursery2);
        var result = ProgressionChainGuard.CanDeactivate(target, levels);

        result.IsSuccess.ShouldBeTrue();
    }

    // The successor has ANOTHER active predecessor besides target — deactivating target strands
    // nobody, so this must succeed even though target itself has both a predecessor and a successor.
    [Fact]
    public void CanDeactivate_WhenSuccessorHasAnotherPredecessor_Succeeds()
    {
        var a = Guid.CreateVersion7();
        var target = Guid.CreateVersion7();
        var successor = Guid.CreateVersion7();

        var levels = new[]
        {
            new ChainLevel(a, "A", 1, successor), // also feeds `successor` directly
            new ChainLevel(target, "Target", 2, successor),
            new ChainLevel(successor, "Successor", 3, null),
        };

        var result = ProgressionChainGuard.CanDeactivate(
            levels.Single(level => level.Id == target), levels);

        result.IsSuccess.ShouldBeTrue();
    }
}
