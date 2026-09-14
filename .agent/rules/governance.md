# Governance — ledger, cards, closure, reporting

**This file is CLAUDE.md §4.1, §4.2, §10 and §12.** Binding in full.

**Reader: the orchestrator only.** A dev agent never needs this file — it is told what to do by
its card, and it appends to STATE.md in the shape STATE.md's own header describes. Do not load it
into a dev dispatch.

---

## 1. The ledger — `.agent/STATE.md` (§4.1)

Single shared file. Every agent reads it at the start of its session and appends to it at the end.
The orchestrator reconciles it after each dispatch.

Sections and what belongs in each:

| Section | Holds | Does NOT hold |
|---|---|---|
| `## Product` | What is being built, authoritative spec pointer | Requirements text |
| `## Layout` | Real paths, package manager, framework versions | Conventions |
| `## Toolchain` | Versions present on this machine, and the pinned set | Install instructions |
| `## Contract` | The CURRENT hash, path count, schema count, and what is stale | Hash history → `contract-history.md` |
| `## In flight` | **Open** cards only: id, title, owner, status | Closed cards (list by ID) |
| `## Decisions` | Dated **one-line** index entries | Full text → `decisions/YYYY-QN.md` |
| `## Known drift` | Dated **one-line** entries, each with **trigger** and **owner** | Full text → `drift/YYYY-QN.md` |
| `## Open questions` | Live ones only | Resolved ones → `decisions/` |
| `## Index` | The router: every rule/spec file, who reads it, when | Any rule itself |

**No size cap** on the file. There is no byte or line budget — never defer an append, compress an
entry, or skip reconciliation to hit a number. The ledger being complete beats it being short.

**But an INDEX ENTRY IS ONE LINE.** That is not a size cap, it is the format. Added 2026-09-14
after `## Decisions` reached 187 KB — 53 essay-length entries covering six days — which every
agent on every dispatch loaded in full to learn state that fit in 8 KB. The archive discipline
existed and was correct; what failed is that it only fired quarterly, so "archive later" meant
"carry the whole quarter in every context window".

**The rule now: full text goes to the archive AT CARD CLOSE, not at quarter end.** Write the
account in `decisions/YYYY-QN.md`, leave one indexed line behind:

```
- 2026-09-10 TASK-0054 audit-log client seam typed; first non-JSON response body forced a
  `ResponseBodyOf` helper. → `decisions/2026-Q3.md`
```

Two physical lines is fine. What is not fine is a paragraph. If the entry needs a paragraph to be
understood, the paragraph belongs in the archive and the index line points at it.

**Archive, never delete.** An entry that is superseded or struck is moved, not dropped.

### Reconciling card status — grep the TREE, not just the archive

Added 2026-09-14, the same day the rule it amends was written. The 2026-09-14 reconciliation
resolved every disputed card header against `decisions/YYYY-QN.md`, on the principle that
**closure is real only where a closure entry exists**. That principle is correct and stays. It is
also only half a check, and the missing half cost a near-miss the same afternoon.

It catches headers that **over**-claim — `Status: done` with no closure entry behind it. It cannot
see a header that **under**-claims. TASK-0045 read `queued`, had no closure entry, and was
therefore reinstated as "written in full and dispatchable" — while `features/arms/` sat complete
on disk, ~1,800 tested lines, with the card's own id cited in eight places. The next dispatch
would have rebuilt it on top of itself.

**A card's `Status:` is a claim about the working tree, and only the tree can refute it.** Before
trusting `queued` or `blocked` on any card, grep the code for the card's id and for the paths its
scope names:

```
grep -rn "TASK-00NN" backend/ frontend/ contracts/   # cards get cited in the code they produce
git log --oneline --all -- <the paths the card's scope names>
```

Both directions, every reconciliation. An orphaned card — one no other file references — is the
signature to watch for: it means nothing has been maintaining its header, so the header is
evidence of nothing at all, in either direction.

## 2. Task card format — `.agent/tasks/TASK-####.md` (§4.2)

```markdown
# TASK-0042 — <imperative title>

Owner: backend-dev | frontend-dev | both (sequenced)
Depends on: TASK-0039
Contract impact: none | additive | BREAKING
Status: queued
Reads: <the exact rule/spec/archive files this card's agent must open — see STATE.md `## Index`>

## Goal
One paragraph. What the user can do when this is done.

## Contract delta
Exact endpoint(s), request/response shapes, status codes, error codes. "None" if unchanged.

## Acceptance criteria
- [ ] Testable statement

## Out of scope
Explicit list — this is how you stop scope creep.

## Notes for the implementing agent
Existing patterns to follow, files to read first, gotchas.

## Log
- 2026-07-27 dispatched to backend-dev
```

**The card BODY caps at ~120 lines** — everything above `## Log`. Measured 2026-09-14, nearly every
card already meets it; what overruns is the `## Log`, which is history and archives to
`.agent/tasks/logs/TASK-####.log.md` at close. A *body* longer than 120 lines is two cards, or it
is restating a spec section it should cite instead.

## 3. Card sufficiency — the pre-dispatch check

**This is the counterweight to the context budget, and it outranks it.** Lazy loading only works
if the card tells its agent what to load. An agent that has been given a thin card does not fail
loudly — it guesses, or it reads everything, and the second one costs more than the whole budget
saved. Added 2026-09-14 alongside the budget pass, because the budget pass created this risk.

**Before dispatching, the card must answer all six. If one is unanswered, the card is not ready.**

| # | The agent must be able to answer | Where it comes from |
|---|---|---|
| 1 | *What am I building, and how will a human use it?* | `## Goal` |
| 2 | *What exactly crosses the wire?* | `## Contract delta`, or "None" stated explicitly — never blank |
| 3 | *How does THIS repo already do this?* | `## Notes`: name the nearest existing file of the same shape, by path. Not "follow existing patterns" |
| 4 | *What will bite me?* | `## Notes`: the drift entries and decisions that constrain this card, named so they can be grepped |
| 5 | *When am I done, and how is each claim proven?* | `## Acceptance criteria`, each one testable |
| 6 | *What must I NOT do?* | `## Out of scope` |

**The mechanical check for row 4 — run it, do not rely on remembering.** Accidental discovery used
to happen because every agent read a 250 KB ledger and tripped over the relevant entry. That no
longer happens, so replace it with a grep:

```
grep -in "<the card's subject>" .agent/STATE.md      # drift + decision index lines
grep -in "TASK-00NN" .agent/STATE.md                 # anything already pointing at this card
```

Every drift line whose *trigger* names this card, or this card's subject, goes into `## Notes` and
its archive location into `Reads:`. A drift entry whose trigger has arrived and was not carried
into the card is the failure this check exists to prevent — it is how a known landmine gets
stepped on twice.

**`Reads:` must be sufficient, not minimal.** It is a budget for the agent to spend, not a cap to
squeeze. Under-naming costs far more than over-naming: an agent that cannot find what it needs
reads the whole archive, or invents a shape. When unsure, name the file.

**The agent may bounce the card.** An implementing agent that cannot answer one of the six from
the card plus its `Reads:` is required to STOP and say which row is unanswered, rather than guess
or go reading. A bounced card costs one re-dispatch. A guessed shape costs a contract delta, a
regeneration on both sides, and a review that has to catch it.

## 4. Definition of done (§10)

A card closes only when **all** hold:

1. Acceptance criteria checked off, each traceable to a test.
2. All gates green, output shown per `.agent/rules/gates.md` §5.
3. Contract regenerated and lockfile current; no hand-edited generated files.
4. No new `TODO`/`FIXME` without a task card number attached.
5. `STATE.md` updated: card moved to `done`, full account written to `decisions/`, one-line index
   entry left behind.
6. Diff reviewed against the relevant spec and reported to the human per section 5 below.

## 5. Report format after each dispatch (§12)

```
TASK-0042 — <title>            [done | blocked | needs human]

Changed:      <files, grouped by area, one line each>
Contract:     unchanged | additive (<summary>) | BREAKING (<summary>) — hash <short>
Gates:        backend ✓  frontend ✓  contract ✓     (or the exact failure)
Deviations:   spec rules bent, with reason, each now in `## Known drift`
Verify by hand: <what a human should actually click>
Next:         <the single next task card, or the question blocking you>
```

Keep it to that. No restating the task, no summarising your own process.
