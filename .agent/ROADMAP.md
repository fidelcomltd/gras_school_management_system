# Roadmap

Derived from `product-specification/index.md` "Suggested build order", with the one departure
recorded in `STATE.md` Decisions (2026-08-26): authorisation infrastructure precedes settings.

Cards are written **just ahead of dispatch**, not all at once. A card written months early
against a spec that then revises is worse than no card. Rows below without a TASK id are
planned, not yet carded.

Legend: **BE** backend-dev · **FE** frontend-dev · **RV** reviewer · **CG** contract-guardian

## Phase 0 — foundations (no product surface)

| Task | Title | Owner | Spec | Blocked by |
|---|---|---|---|---|
| TASK-0001 | Re-audit both scaffolds against §6/§7 | RV | CLAUDE.md §6, §7 | — |
| TASK-0002 | Privilege register and authorisation enforcement | BE | 01 §4.2–4.4, 14 §9.2 | — |
| TASK-0003 | Admin accounts, authentication, session management | BE | 03 §6.1.11, 14 §9.1 | TASK-0002 |
| TASK-0004 | OpenAPI client generator + typed API layer + frontend CI | FE | CLAUDE.md §3, §7 | — |

TASK-0002 and TASK-0004 touch disjoint areas and neither alters the committed contract's
existing paths, so they may be dispatched in parallel — but TASK-0004 must regenerate against
whatever contract is committed at its start, and re-run drift after TASK-0002 lands.

## Phase 1 — configuration and academic calendar

| Task | Title | Owner | Spec | Notes |
|---|---|---|---|---|
| TASK-0005 | School settings: identity, reg-number pattern, config versioning | BE | 04 §6.2.3, 6.2.4, 6.2.9 | `config_version` is append-only and load-bearing for result snapshots |
| — | Grading scale editor + assessment structure | BE | 04 §6.2.5, 6.2.6, **6.2.13** | Seed = nine bands incl. `F 0-19`; 20/20/60. Component count must never be assumed anywhere |
| — | Rating scales, trait lists, development domains and indicators | BE | 04 §6.2.7, 6.2.13 | Scale is a property of the rating block, not school-wide (conflict item 6) |
| — | Fee notice configuration and entry screens | BE+FE | 04 §6.2.13 | Notice only. No arithmetic beyond the printed total, and no result is ever withheld |
| — | Audit log | BE | 03 §6.1.12, 14 §9.3 | Same transaction as the change; DB role holds no UPDATE/DELETE |
| — | Sessions and terms | BE | 05 §6.3 | Term dates derive weekly-report weeks |
| — | Class levels, arms, progression chain | BE | 06 §6.4 | `section` drives result-sheet routing — load-bearing, not descriptive |
| — | Back-office shell, navigation, settings screens | FE | 04, 06 | First real screens; delete `scaffold-status/` here |

## Phase 2 — the register

| Task | Title | Owner | Spec | Notes |
|---|---|---|---|---|
| — | Pupil entity, `pending` status, registration number issue | BE | 07 §6.5.4, 6.5.10, 6.5.14 | Every pupil query except the admissions queue filters `pending` out |
| — | Contacts, health, pickup/barred persons, document checklist | BE | 07 §6.5.5–6.5.9 | Health and barred data behind `pupil.safeguarding.view`, audited on read |
| — | Nine-step resumable admission flow | BE+FE | 07 §6.5.11, 6.5.12 | Largest single flow in the product; split if the card exceeds ~400 lines |
| — | Bulk import | BE | 07 §6.5.13 | Background job with progress; 5000 rows max |
| — | Subjects, mappings, per-arm exceptions | BE | 08 §6.6 | 14 nursery subjects, 19 primary |

## Phase 3 — results

Largest module. `index.md` says split it; expect five or more cards.

| Task | Title | Owner | Spec |
|---|---|---|---|
| — | Result set state machine + score entry + completeness gate | BE | 09 §6.7.3–6.7.5 |
| — | Computation engine | BE | 09 §6.7.6, 13 (fixture) |
| — | Non-academic input, section-specific | BE | 09 §6.7.7, 6.7.12 |
| — | Approval, publication, config snapshot | BE | 09 §6.7.8, 6.7.9 |
| — | Annual cumulative result | BE | 09 §6.7.10 |
| — | Score entry grid with offline resilience | FE | 09 §6.7.4, 14 §9.8.2 |
| — | Nursery + primary result sheet renderers | BE+FE | 21, 22, 18 §C |

`13-result-computation-rules.md` is not a build task — it is the spec the engine satisfies, and
its worked example is a regression fixture. Its numbers assume the superseded assessment
structure and grading scale and must be restated before use.

## Phase 4 — weekly reports (parallel with Phase 3)

| Task | Title | Owner | Spec | Notes |
|---|---|---|---|---|
| — | Weekly report entities, entry grid, publication | BE+FE | 20 §6.10, 23 §G | Unblocked 2026-08-26. Parent's Comment: teacher-transcribed, **optional**, never blocks save/submit/publish. Portal read-only. |

## Phase 5 — pins and the public portal

| Task | Title | Owner | Spec | Notes |
|---|---|---|---|---|
| — | Pin batches, generation, revocation, usage report | BE | 10 §6.8 | Pins are unbound — any valid pin opens any reg number (school directive) |
| — | Public portal: lookup, result view, PDF, verification | BE+FE | 11 §6.9 | Rate limited, audited with null actor, 400 ms deliberate pad |

## Phase 6 — cross-cutting

| Task | Title | Owner | Spec |
|---|---|---|---|
| — | Reports (13 + 8 more, incl. class safeguarding sheet) | BE+FE | 15 §10 |
| — | End-to-end flows as integration tests | BE+FE | 12 |
| — | Backup, archive bundle, restore rehearsal | BE | 14 §9.8.3 |
| — | NDPA 2023 retention and purge schedules | BE | 14 §9.9 |
| — | Playwright: auth, primary create path, one failure path | FE | CLAUDE.md §7 |

## Standing constraints for every card that touches marks or sheets

From `04-module-school-settings.md` 6.2.13, restated because it is the single most expensive
thing to get wrong:

- The assessment component list is **configuration**. No surface may assume how many components
  there are. Column sets, CA totals, PDF widths, portal payload shape and import templates are
  all derived from the component list at runtime.
- The continuous-assessment total is computed from the `is_examination` flag, never from a
  constant.
- The portal returns components as an **ordered array**, never as named fields.
- The import template generates its component columns at download time.

And from `14-non-functional-requirements.md`:

- Cursor pagination on every list endpoint. Never offset. (§9.5)
- Delete only where nothing has ever referenced the row; everything else deactivates. (§9.4)
- Every privilege check is server middleware. Hiding a menu item is not enforcement. (§9.2)
- Idempotency keys on pupil registration, pin generation, promotion commit and result
  publication. (§9.8.2)
