# TASK-0005c — full implementation log

Moved out of `TASK-0005c.md` on 2026-09-14 (context-budget pass). A card is a brief, not a
transcript: the card body is what a future agent reads to copy the pattern, and it stays
under ~120 lines. This is the record of how the card got to closed. Nothing was edited on
the way across.

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
