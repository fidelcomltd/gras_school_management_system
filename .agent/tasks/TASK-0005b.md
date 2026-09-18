# TASK-0005b — Logo and signature uploads

Owner: backend-dev
Depends on: TASK-0005a
Contract impact: additive — `POST /settings/identity/{logo,signature}` plus the two privilege-
checked serving endpoints approved in `decisions/2026-Q3-contract-deltas.md` (TASK-0005 section).
Status: queued (stub — write in full just before dispatch)
Reads: `.agent/spec/backend.md` (section 9.6), `.agent/rules/wire.md`, `.agent/rules/contract.md`
       (additive); the TASK-0005 section of `.agent/decisions/2026-Q3-contract-deltas.md`

Carried here so it is not rediscovered:

- §9.6 in full: magic-byte content-type verification (never the extension or the declared type),
  **SVG rejected outright**, EXIF stripped, stored outside the web root under generated names,
  served through an endpoint applying the parent record's privilege check, with
  `Content-Disposition` and `nosniff`. Logo PNG/JPEG ≤2 MB ≥300×300, derivatives at 200 and 64 px;
  signature PNG preferred ≤1 MB, recommended 600×200.
- **The multipart idempotency-fingerprint gap** found in TASK-0005's delta pass:
  `RequireIdempotencyKeyExtensions.BuildFingerprint` JSON-serialises the bound command and so
  cannot see an uploaded file's bytes. Fold a content hash in, or a reused key against a genuinely
  different image misclassifies as a same-fingerprint replay instead of `idempotency.key_conflict`.
- **Delta amendment 4**: no `DELETE` route is invented — spec 6.2.12 enumerates none. 6.2.11's
  "logo deleted with no replacement" is a **domain invariant** on the set→null transition,
  carrying the verbatim message *"A logo is required. Upload a replacement before removing the
  current one."*, unit-tested at the domain level. 6.2.2 confirms the logo is not seeded, so
  null-at-install is the legitimate starting state and the invariant guards only set→null.

## Log
- 2026-09-06 created by orchestrator as part of TASK-0005's three-way split.
