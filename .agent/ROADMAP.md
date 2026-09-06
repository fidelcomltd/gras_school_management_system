# Roadmap

Derived from `product-specification/index.md` "Suggested build order", with the one departure
recorded in `STATE.md` Decisions (2026-08-26): authorisation infrastructure precedes settings.

Cards are written **just ahead of dispatch**, not all at once. A card written months early
against a spec that then revises is worse than no card. Rows below without a TASK id are
planned, not yet carded.

Legend: **BE** backend-dev · **FE** frontend-dev · **RV** reviewer · **CG** contract-guardian

## Phase 0 — foundations (no product surface)

| Task | Title | Owner | Spec | Status |
|---|---|---|---|---|
| TASK-0001 | Re-audit both scaffolds against §6/§7 | RV | CLAUDE.md §6, §7 | **done** |
| TASK-0002 | Privilege register and authorisation enforcement | BE | 01 §4.2–4.4, 14 §9.2 | **done** |
| TASK-0003 | Authentication and session management | BE | 03 §6.1.3/6.1.6/6.1.10/6.1.11, 14 §9.1 | **done** |
| TASK-0019 | Idempotency substrate | BE | 14 §9.8.2, 9.9 | **done** |
| TASK-0027 | Admin account management | BE | 03 §6.1.2/6.1.7/6.1.9-6.1.14, 14 §9.2/9.4/9.5 | **done** |
| TASK-0028 | Roles and the privilege register | BE | 03 §6.1.4, 6.1.7 r2, 6.1.14 · 01 §4.4, 4.5 | in progress — dispatch 1 of 3 |
| TASK-0030 | Role assignments, scopes, escalation rules 1 and 3 | BE | 03 §6.1.5, 6.1.7, 6.1.8, 6.1.10, 6.1.13-14 · 01 §4.2 | **blocked** — needs sessions (05 §6.3) and arms (06 §6.4); split from 0028 on 2026-09-06 |
| TASK-0020 | Adopt §7's frontend structure, form and E2E mandates | FE | §7 — resolves Open question 11 | **done** |
| TASK-0021 | Cookie auth seam and the sign-in screen | FE | §5, §7 — consumes TASK-0003's contract | **done** |
| TASK-0004 | OpenAPI client generator + typed API layer | FE | CLAUDE.md §3, §7 | **done** |
| TASK-0006 | Regenerate the frontend client after a contract move | FE | CLAUDE.md §4.3 | **done** |
| TASK-0029 | Regenerate the client, complete the typed wrapper surface | FE | CLAUDE.md §3, §4.3, §4.4 | queued — carded 2026-09-06, dispatch when TASK-0027 closes |

### Phase 0b — toolchain hygiene, all discovered by doing Phase 0

None of these were foreseen when the board was drawn on 2026-08-26. Each came out of a gate
actually being run rather than assumed, and each closes a way the pipeline could lie to us. They are
cheap, and they are worth finishing before the first product surface, because every later card
inherits whatever these leave broken.

| Task | Title | Owner | Why it exists | Status |
|---|---|---|---|---|
| TASK-0007 | Clear the SSH.NET High advisory | BE | The vulnerable-dependency gate is red, so §9 is unsatisfied and every card closes against a known-failing gate. A red gate nobody can fix stops being information. | **done** |
| TASK-0008 | Durable local home for the test connection string, + local gitleaks | BE | No sanctioned place to keep `POSTGRES_TEST_CONNECTION`, so it gets forgotten — and forgetting it makes the integration tests SKIP while the suite still reports success. Also installs gitleaks, which currently does not run locally at all. | **done** |
| TASK-0009 | Move document-property contract tests off the database | BE | Eight checks needing no database were invisible whenever Postgres was unreachable. That is exactly how TASK-0002's missing example reached the committed contract. | **done** |
| TASK-0010 | Make OpenAPI document generation deterministic | BE | An incremental build produced a different document than a clean build, from identical source. §3 rests on the contract being reproducible from source. | **done** |
| TASK-0011 | Make the secret-scan gate pass for the right reason | BE | Installing gitleaks (TASK-0008) made the gate execute for the first time ever; it found 9 non-secrets tripping an untested rule. A gate that had never run was reporting PASS. | **done** |
| TASK-0012 | Declare the problem-detail extension members in the contract | BE+FE | TASK-0001 blocker B1. The contract forbids the `errorCode`/`traceId` every error response carries, and the closed type has already reached `schema.d.ts`. Blocks TASK-0003 frontend. | **done** |
| TASK-0013 | Idempotency — Option B (defer, binding record), decided 2026-09-04 | BE | TASK-0001 blocker B2. Deferred with a binding record: `ASSUMPTIONS.md` §2.14 + `TODO(TASK-0013)`. **Trigger: the first card implementing ANY retry-duplicable mutation builds the mechanism first.** | closed |
| TASK-0017 | Clear the secret-scan gate TASK-0011's own docs turned red | BE | TASK-0011 proved its rule still fired by committing the fake credential it tested with, re-arming the rule against its own write-up. A gate red for documenting its fix stops being information. | **done** |
| TASK-0014 | Frontend scaffold conformance fixes from the §7 audit | FE | TASK-0001 S4-S8. The feature README prescribes bypassing the generated client, and `npm run lint` cannot fail on warnings. | **done** |
| TASK-0031 | Move the test-connection prelude into the gate script itself | BE | Every caller had to hand-build a compound PowerShell statement to set one environment variable, and the reinvention is where the cost went: TASK-0028 dispatch 1 spent repeated ten-minute CI runs on the invocation rather than the code. Supersedes TASK-0018 by deleting the wrapper it fixed. | **done** |
| TASK-0032 | Make the secret scan see the diff it is gating | BE | `gitleaks detect` reads committed history while a dispatch gates an uncommitted tree, so gate 9 never examines the change under test and fires one card late, at the next author. A scan that reports PASS without having looked is TASK-0011 all over again. | **done** |
| TASK-0018 | Make `local-env.template.ps1` pass switches to the gate script | BE | TASK-0016 added the two switches a local developer most needs (`-NoFailFast`, `-AllowSkipped`) and the only sanctioned wrapper could not pass either: an array splat binds positionally. A gate option nobody can reach is a gate option nobody uses. | **done** |
| TASK-0015 | Backend scaffold conformance fixes from the §6 audit | BE | TASK-0001 S1-S3. `/health/ready` is an unthrottled anonymous DB round trip; Api depends on Infrastructure at runtime unenforced; the relocated document tests pass against a stale artefact. | **done** |

**The through-line, worth remembering when the next card is tempting to rush:** every one of these
was a way for a green result to mean nothing. Skipped tests reported as passes, a coverage floor
that stands down when the suite is incomplete, a secret scan that passes without running, a
generator that reports work it did not do. The gates were all present and all well-written — the
failures were in whether they actually executed.

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
