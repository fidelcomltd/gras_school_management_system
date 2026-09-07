# TASK-0005c — Registration number configuration

Owner: backend-dev
Depends on: TASK-0005a
Contract impact: additive — `PATCH /settings/reg-number`, `GET /settings/reg-number/preview`,
`PATCH /settings/abbreviation`. Approved: `decisions/2026-Q3-contract-deltas.md` (TASK-0005).
Status: queued (stub — write in full just before dispatch)

Carried here so it is not rediscovered:

- **Delta amendment 1, a BLOCKER**: `serial_reset` is an enum of `per_year` **or `continuous`**
  (6.2.4). A counter keyed on `admission_year` alone can only express `per_year`, so `continuous`
  would be accepted, stored, and do nothing — a control that reports success and changes no
  behaviour. The counter must carry both modes; the preview and the width-reduction scan must both
  read whichever partition the **saved** `serial_reset` selects. **A test must set `continuous`,
  cross a year boundary in seeded counter state, and assert the serial does not restart.**
- **Delta amendment 2**: `abbreviation.issuedCount` is **nullable**, `null` meaning "no register
  exists to count yet" — never `0`, which is a claim the system cannot support. No pupil register
  exists until a later card.
- Preview semantics, confirmed against 6.2.4 and not to be re-litigated: composed from the
  **current saved abbreviation**, with `separator`/`serialWidth` supplied unsaved as query
  parameters, using the next serial that would actually be issued (`GRAS/2026/0040` with 39 pupils
  already admitted in 2026).
- Width reduction is rejected when an issued serial in the selected counter exceeds the new width,
  `detail` carrying 6.2.10's exact string with real numbers: *"Serial 1043 will not fit in a width
  of 3. Choose 4 or more."*
- Abbreviation change: the literal `CHANGE` token, a mandatory **non-empty** reason (no
  10-character floor — 6.2.9's floor does not reach this group), an audit event, and it **rewrites
  no already-issued number**. The counter is keyed on admission year alone, so a mid-year change
  neither restarts the serial nor produces two pupils differing only by prefix. A historically-used
  value is allowed (6.2.11).

## Log
- 2026-09-06 created by orchestrator as part of TASK-0005's three-way split.
