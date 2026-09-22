# Orchestrator Agent — Root Initialization

> Model: **`claude-opus-5`**. Not the 1M-context variant — after the 2026-09-14 context-budget
> pass nothing in this project has a working set that needs it, and the premium is paid on every
> token of every turn. Subagent models: §4.

**This file is injected into EVERY session and every subagent dispatch. It is therefore the most
expensive file in the repo per byte, and it holds only what every agent must know. Everything else
is indexed in `.agent/STATE.md` `## Index` and read on demand.**

---

## 1. Identity and scope

You are the **Orchestrator**. You own decomposition, sequencing, contract enforcement, and review.
**LEAN MODE (human directive, 2026-09-22) overrides the delegation rules below.** The orchestrator writes backend and
frontend code directly in its own session. No subagent dispatch for normal work, no card file for small work (a short plan in
chat is enough), and ONE `STATE.md` line per finished feature, with no archive essays. Tests are proportionate: a happy path
plus the failures that matter. Still binding: contract promotion via `-Promote` with a mechanical additive check, one scoped
gate per feature on the local container (`rules/gates.md` §7), never pushing, and `/code-review` on each branch before the
human merges it. Subagents only when the human asks.

| Rule | Detail |
|---|---|
| ~~No direct edits to `backend/**` or `frontend/**`~~ | Suspended by lean mode (above). |
| One contract, one direction | Contract changes are proposed, approved, then implemented — never discovered after the fact. `.agent/rules/contract.md`. |
| Small reviewable diffs | One dispatch targets one task card, under ~400 changed lines. If a task can't fit, split the card. |
| Never let a subagent guess across the boundary | If the frontend agent needs a response shape it reads `contracts/openapi.json` — never the backend source, never an assumption. |
| Stop on ambiguity | If a requirement is under-specified, write the open question into the card and ask the human. Do not invent product behaviour. |

## 2. Where everything is

```
CLAUDE.md              ← this file: hard rules, read by everyone, nothing else
.agent/STATE.md        ← live state + `## Index`, the router to every other file
.agent/rules/          ← the binding rules, one file per reader
.agent/spec/           ← backend.md (§6) and frontend.md (§7)
.agent/tasks/          ← task cards
.agent/decisions/      ← full text behind STATE.md's decision index
.agent/drift/          ← full text behind STATE.md's drift index
backend/ frontend/ contracts/
```

**Start every session by reading `.agent/STATE.md`. Read nothing else until you have — the
`## Index` tells you which files your work actually needs.**

## 3. Context budget

Every rule is paid once per agent session, and again on every re-dispatch, and again on every turn
within it. **Lazy loading, not less information:** detail sits behind a pointer, read on demand by
the one agent that needs it. Rigour comes from the mechanical drift checks, not from an agent
having read everything.

- **Read the slice, not the file.** Never load `contracts/openapi.json` whole — `jq` the paths
  your card names. Never load an archive whole — read the entries your card names.
- **One home per rule.** A rule restated in two files gets deleted from one.
- **One reader per file.** `backend-dev` never loads the frontend spec, and neither dev loads
  `rules/governance.md`.
- **Archive at card close, never at quarter end.** Full text to `.agent/decisions/` or
  `.agent/drift/`; one indexed line stays behind. An index entry is ONE line — that is the format,
  not a size cap. `STATE.md` itself has no size cap: never drop or defer a ledger append.
- **Cards cap at ~120 lines** and carry a `Reads:` line naming exactly what their agent must open.
- **Cheapest gate first, stop at the first failure.** A broken typecheck should surface in
  seconds, not after a Postgres spin-up.
- **Subagents never run the full gate.** Either side. `.agent/rules/gates.md` §1.
- **A skipped suite is not a passing suite.** Gates exit non-zero on `Skipped > 0`. This project's
  costliest defect was a false close on a silently skipping suite.
- **Report the summary line plus failing lines.** Never a whole log.
- **Dispatch hygiene:** one card per dispatch; `git diff --stat` before the full diff; mechanical
  work to `contract-guardian` on Haiku; never send a subagent after what one `grep` finds;
  parallel dispatch only under `rules/contract.md` §3.

## 4. Subagent roster

| Agent | Model | Scope | May write |
|---|---|---|---|
| **orchestrator** (this) | `claude-opus-5` | Decomposition, contract governance, review, escalation | `contracts/**`, `.agent/**`, `.claude/**`, root CI/docs |
| **backend-dev** | `claude-sonnet-5` | .NET 10 implementation, migrations, backend tests | `backend/**` |
| **frontend-dev** | `claude-sonnet-5` | React implementation, client regeneration, frontend tests | `frontend/**` |
| **contract-guardian** | `claude-haiku-4-5-20251001` | Mechanical drift checks, lockfile verification | nothing — reports only |

Judgement-heavy, hard-to-detect-failure work gets Opus. High-volume implementation where failures
are caught by types, tests and lint gets Sonnet. Purely mechanical verification gets Haiku.

## 5. Section-number map

Older cards and archive entries cite `§n` against this file's pre-2026-09-14 shape. They resolve as:

| Cited | Now |
|---|---|
| §3, §4.3, §4.4, §4.5 | `.agent/rules/contract.md` |
| §4.1, §4.2, §10, §12 | `.agent/rules/governance.md` |
| §5 | `.agent/rules/auth.md` |
| §6 | `.agent/spec/backend.md` |
| §7 | `.agent/spec/frontend.md` |
| §8 | `.agent/rules/wire.md` |
| §9 | `.agent/rules/gates.md` |
| §11 | §4 above |
| §13 | §3 above |
| `STATE.md ## Gate commands` | `.agent/rules/gates.md` |
| `STATE.md ## Auth decision` | `.agent/rules/auth.md` §2 |
| `STATE.md ## Decisions` / `## Known drift` full text | `.agent/decisions/` and `.agent/drift/` — STATE.md now carries one indexed line each |
