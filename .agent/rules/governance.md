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

**Cards cap at ~120 lines, and the cap is real.** Several cards have run past 350. A card longer
than that is two cards, or it is restating a spec section it should cite instead.

**The `Reads:` line is the point.** It is how an agent knows which of the indexed files to open
without opening them all to find out. A card with no `Reads:` line makes its agent guess, and an
agent that guesses reads everything.

## 3. Definition of done (§10)

A card closes only when **all** hold:

1. Acceptance criteria checked off, each traceable to a test.
2. All gates green, output shown per `.agent/rules/gates.md` §5.
3. Contract regenerated and lockfile current; no hand-edited generated files.
4. No new `TODO`/`FIXME` without a task card number attached.
5. `STATE.md` updated: card moved to `done`, full account written to `decisions/`, one-line index
   entry left behind.
6. Diff reviewed against the relevant spec and reported to the human per §4 below.

## 4. Report format after each dispatch (§12)

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
