# TASK-0005c — Registration number configuration

Owner: backend-dev
Depends on: TASK-0005a (identity + `config_version` substrate) — closed.
Contract impact: **additive** — `PATCH /settings/reg-number`, `GET /settings/reg-number/preview`,
`PATCH /settings/abbreviation`, plus two new groups on the existing `SettingsDto`.
Approved delta: `decisions/2026-Q3-contract-deltas.md` — **TASK-0005, approved with four
amendments.** Read that entry before starting; amendments 1-3 all land in this card.
Status: done

## Goal

The school can set how a registration number is composed — separator, serial width, reset rule —
and change its abbreviation with the consequences stated up front. Nothing issues a number yet;
this card owns the **configuration and the counter's read paths**, TASK-0051 owns the increment.

## Contract delta — additive

| Endpoint | Privilege | Notes |
|---|---|---|
| `PATCH /api/v1/settings/reg-number` | `settings.regnumber.update` | `separator`, `serialWidth`, `serialReset`. `expectedVersion` echoed. `Idempotency-Key` **accepted, not required**. |
| `GET /api/v1/settings/reg-number/preview` | `settings.view` | `separator`/`serialWidth` as **unsaved query parameters**; abbreviation from **saved** state. Read — no CSRF. |
| `PATCH /api/v1/settings/abbreviation` | `settings.abbreviation.update` | Literal `CHANGE` token + reason. `expectedVersion`. `Idempotency-Key` accepted. |

`GET /settings` gains an **abbreviation** group and a **reg-number** group, following
`SettingsIdentityGroupDto`'s established shape — do not invent a second grouping convention.

Field rules are **spec 6.2.4's table**; read it there. `separator` one of `/ - .` default `/`;
`serialWidth` 3-6 default 4; `serialReset` `per_year` | `continuous` default `per_year`;
`year_source` is **fixed and not editable** ("a number that changes meaning with the calendar is
not an identifier") — expose it as a constant, not a settable field.

## The three amendments you are implementing

**Amendment 1 — `serial_reset: continuous` must actually work. This is the BLOCKER.** A counter
keyed on `admission_year` alone can only express `per_year`. Under `continuous` the serial does not
restart in January — 2027's first pupil follows 2026's last. Left keyed on year, `continuous` would
be accepted, stored, and **do nothing**: a control that reports success and changes no behaviour,
which is the exact defect family this project keeps removing. So:

- `registration_counter` carries **both** modes — a global/sentinel partition (`counter_key = 'ALL'`)
  alongside the per-year rows, per 6.5.10's counter-key rule.
- The preview composes from whichever partition the **currently saved** `serialReset` selects.
- The width-reduction scan reads **that same partition**, not always the per-year rows.
- **Required test: set `continuous`, seed counter state crossing a year boundary, assert the serial
  does not restart.** A test that only exercises `per_year` leaves this unproven and fails the card.

**Amendment 2 — `abbreviation.issuedCount` is NULLABLE and must never ship `0`.** No pupil register
exists yet, so nothing here can count issued numbers. `null` means "no register exists to count";
`0` is a claim the system cannot support, and would render 6.2.4's dialogue as "0 pupils already
hold…" as though it were fact. Document it in the contract as nullable-until-the-register-exists.
Wiring a real count is TASK-0051's, already a live trigger in `## Known drift`.

**Amendment 3 — the stale-save message is grading-scale copy; do not reuse it verbatim.** 6.2.11's
sentence names the grading scale specifically. This card touches abbreviation and registration
number, neither of which is the grading scale. Write the sentence **per group**, and record it in
`backend/docs/ASSUMPTIONS.md` as **authored copy awaiting school confirmation** — do not present
invented user-facing text as though the spec supplied it.

## Acceptance criteria

- [x] `registration_counter` table + migration, carrying both partition modes per amendment 1.
      **This card never increments it** — no issue path, no `MAX+1` anywhere (6.5.10 forbids
      deriving the counter from the pupil table, including in the import path).
- [x] `continuous` proven: seeded counter state crossing a year boundary, serial does not restart.
      `per_year` proven separately to restart. **Both directions, or the card is not done.**
- [x] Width reduction rejected when an issued serial in the **selected** partition exceeds the new
      width, `detail` carrying 6.2.10's exact string **with the real numbers substituted**:
      `Serial 1043 will not fit in a width of 3. Choose 4 or more.`
- [x] Preview: composed from the **saved** abbreviation with `separator`/`serialWidth` supplied
      unsaved as query parameters, using the **next serial that would actually be issued**. With an
      empty register that is serial 1; the shape must be right, not the value hardcoded.
- [x] Abbreviation change requires the **literal `CHANGE`** token and a **non-empty trimmed**
      reason — **no 10-character floor** (6.2.9's floor is scoped by its own prose to
      grading/assessment/traits/trait-scale/result-rules; 6.2.10 confirms abbreviation is never
      locked). Cap the length. Record both choices in `ASSUMPTIONS.md`.
- [x] Abbreviation change **rewrites no issued number**, and the counter is keyed on admission year
      **alone, not the abbreviation** — so a mid-2026 change neither restarts the serial nor can
      produce two pupils whose numbers differ only by prefix. Tested.
- [x] An abbreviation value **already used historically is allowed** (6.2.11) — tested, not just
      asserted in a comment.
- [x] Optimistic concurrency per group: client-echoed integer `expectedVersion`, compared **inside
      the same transaction** as the `config_version` insert, **409 before any write**. The loser
      gets the audit-seam call and **no** version row — that is what 6.2.11's "both attempts appear
      in the audit log" actually requires. Tested both sides.
- [x] Every save writes a `config_version` row holding the whole serialised configuration (6.2.9).
      Never updated, never a bare reference.
- [x] `openapi.json` regenerated via `scripts/generate-openapi.ps1 -Promote`; `CONTRACT.lock`
      updated; diff **additive only**. All ten §9 gates green, `SUMMARY` pasted.

## Out of scope

- **Issuing a number.** No `POST /admissions/{id}/approve`, no counter increment, no
  `pupil_reg_number_history`. All TASK-0051.
- **Grading scale, assessment structure, traits, trait scale, result rules** — 6.2.5-6.2.8, and the
  6.2.9 recompute-flag machinery that belongs to them. This card's groups are never locked
  (6.2.10) and never trigger that warning.
- **Logo/signature uploads** — TASK-0005b, including the multipart-fingerprint gap the approved
  delta assigned there.
- **Any frontend work.** This card moves the contract; a regeneration card follows.
- Anything under `frontend/**`.

## Notes for the implementing agent

Read the **TASK-0005 entry in `decisions/2026-Q3-contract-deltas.md`** first — it is the approved
delta plus its four amendments, and its "Confirmed as proposed" list settles concurrency,
`config_version`, idempotency and seeding so you do not re-litigate them. Then spec
`04-module-school-settings.md` §6.2.4 (the field table and the abbreviation dialogue), §6.2.10 (the
width-reduction message) and §6.2.11 (historical values, stale save). Skip 6.2.5-6.2.8.

Follow TASK-0005a's shipped shape — `UpdateSchoolIdentityCommand`, `SettingsIdentityGroupDto`,
`Settings/UpdateSchoolIdentityHandler.cs` — for the group/version/audit pattern. Do not invent a
second convention for any of it.

`GRAS` is already seeded as the abbreviation by 6.2.2/TASK-0005a; do not re-seed it.

Size: if you pass roughly 900 lines of production code, stop and report.

**Gates.** `STATE.md` `## Gate commands` is the single home for the canonical invocation, its four
rules and the strict serialization requirement — follow it there. **Run the full gate once, at the
end.** While iterating use `dotnet test --filter`. Stay in the turn until you have the SUMMARY.

## Log
- 2026-09-06 created by orchestrator as part of TASK-0005's three-way split.
- 2026-09-09 **expanded from stub and moved to the front of the queue** on the human's direction,
  after open question 13 established it blocks TASK-0051 and therefore the whole pupil register.
- 2026-09-09 **CLOSED by orchestrator.** All ten gates PASS: `total=795 passed=795 failed=0
  skipped=0`, line 80.72% / branch 68.86% (both UP from 80.65/68.66). Contract
  `e86e1b187bbacd9f83b8bd725cb8c9066bf41c5fd936b606da331646d2080e90`, 41 paths (was 38),
  recomputed independently with `sha256sum` and matching `CONTRACT.lock`.

  **Reviewed against the two criteria that decide the card, by reading the diff:**
  1. **Amendment 1 is genuinely closed, not nominally satisfied.**
     `RegistrationCounterPartition.Resolve` returns the fixed `"ALL"` sentinel under `Continuous`
     and the year string under `PerYear`. Both directions proven in paired tests:
     `HandleAsync_UnderContinuous_CrossingAYearBoundary_TheSerialContinuesRatherThanRestarting`
     (seeds `"ALL"` at 41, asserts `GRAS/2026/0042` then `GRAS/2027/0042` — year segment moves,
     serial does not) and `..._UnderPerYear_..._TheSerialDoesRestart` (asserts `GRAS/2027/0001`).
     The continuous test also seeds a DECOY `"2027"` row at 0, so reading the wrong partition fails
     differently rather than passing by luck — that decoy is what makes the test load-bearing.
     Further proven against live Postgres by
     `GetRegNumberPreview_UnderContinuous_ReadsTheAllPartitionInsteadOfTheYearPartition`, which
     PATCHes `serialReset` and re-previews on one real database.
  2. **No increment path leaked in from TASK-0051's scope.** `IRegistrationCounterRepository` has
     exactly one member, `GetLastSerialAsync`. No `MAX(` anywhere in the touched files.
     `RegistrationCounter` has only a private EF constructor and private setters — no public
     factory. The `lastSerial + 1` in `GetRegNumberPreviewHandler` computes the next serial for
     DISPLAY, not a stored mutation, which is what 6.5.10 wants.
     `RegistrationCounterPartition`'s remarks correctly document the year-source split: this card
     passes `TimeProvider`'s current year, TASK-0051 will pass the admission year from
     `admission_record.date_admitted`. Good boundary hygiene for the card that follows.

  **Two process deviations, both accepted:**
  - **Size overrun, ~1,005 hand-written production lines against the card's ~900 stop-and-report
    threshold (~12% over).** The agent flagged it while writing its report rather than stopping to
    ask, which is not what the card said. Accepted on the merits: its argument that amendment 1's
    proof needs the table, the preview read and the width-check read to exist and agree in one
    card is correct, and I could not find a split either. The instruction was still to stop and
    ask; noted so the next card's threshold is treated as a gate, not a guideline.
  - **First gate run red for environmental reasons.** 44/228 integration tests failed, 42 of them
    `SocketException: No such host is known` (DNS), ~2 mid-query drops, **zero assertion
    failures**, in a run that took 1 h 54 m against a normal sub-10-minute suite — retry backoff,
    not work. Confirmed environmental, DNS verified recovered (6/6) before one clean re-run, which
    passed everything. No test or product code was touched in response, per `## Gate commands`.

  **Left correctly undone, matching Out of scope exactly:** no increment path, no issue path, no
  `pupil_reg_number_history`, no uploads, no frontend work. **TASK-0051 is now unblocked** on this
  dependency (it still needs TASK-0050 and the `enrolment` question). §4.4 check 2 is RED until
  TASK-0052 regenerates the client.
