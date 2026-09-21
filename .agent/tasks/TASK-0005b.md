# TASK-0005b — Logo and signature uploads (Cloudinary-backed)

Owner: backend-dev
Depends on: TASK-0005a (done). Branch `task-0005b` is stacked on the branch current at dispatch.
Contract impact: **additive. The delta below is APPROVED by the orchestrator (2026-09-21)** and extends the TASK-0005 approval in
`decisions/2026-Q3-contract-deltas.md`. The orchestrator promotes once, after stage C. Dev agents never run `-Promote`.
Status: **stage A dispatched 2026-09-21** (processor, no contract move). TASK-0090 closed first.
Stages: FOUR dispatches of ~400 hand-written lines each, tests included (`governance.md` §2). One stage per dispatch.
Reads: `.agent/rules/contract.md`, `.agent/rules/wire.md`, `.agent/rules/gates.md` §1 (backend row: **no integration runs**);
       `product-specification/14-non-functional-requirements.md` §9.6; `04-module-school-settings.md` lines 23-24, 72-77, 267-268, 278;
       `decisions/2026-Q3-contract-deltas.md`: `grep -n "Amendment 4\|serving endpoints\|multipart-fingerprint"` (read ~15 lines each);
       the rulings: `grep -n "TASK-0005b carded" .agent/decisions/2026-Q3.md`.

## Goal

An administrator uploads the school logo and the head teacher's signature. Each file is verified by its bytes, stripped of all
metadata and resized where the spec says, then stored privately in Cloudinary. It is served back only through the API's own
privilege-checked endpoints. Publication (a later card) needs both.

## Rulings (human, 2026-09-21)

- **Storage is Cloudinary; hosting is a VPS.** **The library is SkiaSharp** (MIT), plus `SkiaSharp.NativeAssets.Linux.NoDependencies`
  for the VPS, plus the official `CloudinaryDotNet` SDK. Pin all three exactly. This justification satisfies `wire.md`'s dependency rule.

## Orchestrator design (binding)

- **Cloudinary is private blob storage, not a CDN.** Upload with `type: authenticated`. The API streams bytes out through its own endpoints,
  never a redirect or a public URL. That keeps §9.6's privilege check, `Content-Disposition` and `nosniff`.
- **Processing happens locally, before upload.** Raw client bytes never leave the server.
- **Assets are immutable.** Every upload gets a new generated `public_id` (a GUID, in an environment-prefixed folder), and nothing is ever overwritten or
  deleted: the publication snapshot references the logo in force at the time, and 04 line 77 requires it to render forever.
- **Tests never touch the network.** The port `ISchoolImageStore` gets an in-memory fake for unit and integration tests. The Cloudinary
  adapter is proven by the human once, with real credentials (stage D).

## Contract delta (approved)

1. `POST /api/v1/settings/identity/logo` and `POST /api/v1/settings/identity/signature`, operations `UploadSchoolLogo` and `UploadHeadTeacherSignature`: multipart,
   with one `file` part. They use the privilege `PATCH /settings/identity` uses (grep it), plus CSRF and a REQUIRED `Idempotency-Key`. The per-route
   request size limit is the file cap plus 64 KB. `200 SchoolImageDto {width, height, uploadedAt, uploadedByName}`. `422` codes:
   `school_image.unsupported_type` (not PNG/JPEG by magic bytes, SVG included), `.too_large`, `.too_small` (logo below 300×300).
2. `GET /api/v1/settings/identity/logo/{size}` (`size` = `original` | `200` | `64`) and `GET /api/v1/settings/identity/signature`, operations `GetSchoolLogo`
   and `GetHeadTeacherSignature`. They use the privilege `GET /settings` uses, and return `image/png` or `image/jpeg` with `Content-Disposition: inline;
   filename=...`, `X-Content-Type-Options: nosniff`, and `Cache-Control: private`. `404 school_image.not_uploaded` when none exists.
3. The existing identity DTO in `GET /settings` gains `logo: SchoolImageDto|null` and `signature: SchoolImageDto|null` (response-only, additive).

## Acceptance criteria

**Stage A: the processor. The contract does not move.** `ISchoolImageProcessor` goes in Application and the SkiaSharp implementation in Infrastructure.
- [ ] Magic bytes decide the type: PNG and JPEG are accepted, and everything else is refused, including SVG, GIF, WebP and a PNG renamed `.jpg`.
- [ ] Caps: logo ≤2 MB and ≥300×300; signature ≤1 MB, with no minimum (600×200 is only a recommendation).
- [ ] Re-encoding strips all metadata. A committed JPEG fixture carrying EXIF GPS comes out with none, proven by reading the output's metadata.
- [ ] PNG transparency survives. Logo derivatives come out at 200 and 64 px on the long edge, aspect kept. The signature is re-encoded but never resized.
**Stage B: storage port, entity and upload routes (delta 1 and 3)**
- [ ] `ISchoolImageStore` with `PutAsync(bytes, contentType) → assetId` and `OpenAsync(assetId) → stream`, plus the in-memory fake.
      A `school_image` row holds kind, size variant, asset id, width, height, content type, and uploaded at/by. `school_profile` points at the
      current logo set and signature. There is a migration.
- [ ] Upload replaces the current pointer and never deletes the old asset. It is audited with `before_json`/`after_json` (asset ids,
      never bytes). The domain invariant from Amendment 4 guards set→null with its verbatim message, unit-tested.
- [ ] **Idempotency fingerprint gap:** fold a SHA-256 of the uploaded bytes into `BuildFingerprint`. The same key with different bytes
      is `idempotency.key_conflict`, not a replay (unit test).
**Stage C: serving (delta 2)**
- [ ] Both GETs stream from the store with the three headers. 404 before upload. A caller without the privilege gets 403.
- [ ] `OpenApiExamples.cs` has an example for each new schema. An architecture test asserts the multipart request and the image responses carry `content`
      (TASK-0049's trap).
**Stage D: the Cloudinary adapter**
- [ ] `CloudinaryImageStore` uses options `Cloudinary:CloudName`, `ApiKey` and `ApiSecret`, and a folder prefix per environment. The secrets come from environment
      variables or user-secrets only, never `appsettings*.json`, and gitleaks stays green. Startup fails fast outside Development/Test when they are missing.
      DI picks the fake in tests and Cloudinary elsewhere.
- [ ] Unit tests cover option validation and the upload parameters (`authenticated`, a generated public id, no overwrite) behind a thin seam over the SDK.
- [ ] `backend/docs/` gets a short runbook: where the keys go on the VPS, and a manual smoke test the human runs once.

**Every stage:** you WRITE integration tests and make them compile, but do not run them. The orchestrator does. Build, format and the whole unit
project must be green, and you commit at each green build.

## Out of scope

Pupil photographs (§9.6 row 1), the publication precondition and snapshot, any frontend screen, CDN/public delivery, deleting assets,
and client regeneration (the orchestrator cards it after promotion).

## Notes for the implementing agent

- **Nearest files:** `Settings/UpdateSchoolIdentityHandler.cs` and its endpoint for identity plumbing. For the idempotency filter, see
  `Api/Idempotency/RequireIdempotencyKeyExtensions.cs` (`BuildFingerprint`, line ~154). For the fail-fast startup guard, see
  `StartupEnvironmentGuard`; an arch test exempts it (drift 2026-09-04), so do not add a THIRD exemption.
- **Traps:** multipart binding plus the mediator validation pipeline is new here, so keep the file off any logged command. EXIF can hide in
  PNG `eXIf` chunks too. The TASK-0049 `Produces` trap applies to both the multipart request and the binary responses.
- Return a `LEDGER ACCOUNT` section. You never write `.agent/**`, and you never push.

## Log

- 2026-09-06 created by the orchestrator as a stub (TASK-0005's three-way split).
- 2026-09-21 written in full after the human ruled: Cloudinary on a VPS, SkiaSharp. Four stages.
