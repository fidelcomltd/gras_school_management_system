# Project State

Last reconciled: 2026-09-21 by orchestrator (TASK-0088–0091 closed; TASK-0086 and TASK-0087 closed; contract `bb29ee1e…`) · no size cap, see
`## How to read and append to this file` at the bottom.

**This is the ledger. Read it whole — it is meant to be cheap enough to. Then read ONLY what your
card's `Reads:` line names out of `## Index`.**

## Product

**GRAS School Management System** — Golden Royal Ark School (nursery + primary, Nigerian
three-term session). Back office for config, pupils, admission, marks, results and pins, plus one
public page where a parent redeems a reg number + access pin to read a published result.
**Parents have no accounts.**

Authoritative spec: `product-specification/` rev 3.1, entry `index.md` (the school's `.docx` is
rev 1 and is **not** authoritative). Out-of-scope list: `00-document-overview.md` 3.2.

## Index

The router. Every file an agent might need, what it costs, who reads it, and the test for whether
to open it. **If the test does not apply to your card, do not open the file.**

### Rules — binding, one home each

| File | ~KB | Reader | Open it when |
|---|---:|---|---|
| `CLAUDE.md` | 5 | everyone | auto-injected; never open it deliberately |
| `.agent/rules/contract.md` | 5 | orchestrator, contract-guardian; a dev ONLY if `Contract impact` is not `none` | your card moves the contract, or you are running a drift check |
| `.agent/rules/gates.md` | 7 | whoever is about to run or report a gate | before running any gate, or reporting one. **Subagents: read section 1 and stop — you do not run the full gate** |
| `.agent/rules/wire.md` | 1 | both dev agents | always; it is one screen |
| `.agent/rules/auth.md` | 3 | whoever touches auth | your card touches sign-in, sessions, cookies, CSRF, refresh, logout, password or CORS |
| `.agent/rules/governance.md` | 5 | **orchestrator only** | writing or closing a card, reconciling this file, reporting to the human. Never load it into a dev dispatch |

### Specs — the two halves, never both

| File | ~KB | Reader | Open it when |
|---|---:|---|---|
| `.agent/spec/backend.md` (§6) | 3 | `backend-dev` | always, if you are backend-dev |
| `.agent/spec/frontend.md` (§7) | 3 | `frontend-dev` | always, if you are frontend-dev |
| `backend/AGENTS.md` | 10 | `backend-dev` | the concrete recipes behind §6. Section 4 is the endpoint recipe — read that section, not the file |
| `frontend/CONVENTIONS.md` | 17 | `frontend-dev` | the concrete conventions behind §7. Section 4 is the feature-folder layout. Read the section your card names |
| `frontend/HANDOFF.md` | 8 | `frontend-dev` | deliberate omissions and their reasons; only if your card questions why something is absent |
| `backend/docs/ASSUMPTIONS.md` | 108 | `backend-dev` | **NEVER whole.** Five sections: 1 decisions taken on recommendation, 2 decisions taken unasked, 3 open/needs-a-human, 4 defects found by the first real integration run, 5 verified facts. `grep -n "^### "` for the numbered item your card names |
| `frontend/src/api/README.md` | 2 | `frontend-dev` | how the generated seam is shaped |

### Archives — full text behind this file's index lines

| File | ~KB | Open it when |
|---|---:|---|
| `.agent/decisions/2026-Q3.md` | 280 | **NEVER whole.** Your card names a decision; `grep` its TASK id |
| `.agent/decisions/2026-Q3-contract-deltas.md` | 45 | you need an approved contract delta verbatim — auth cookie attributes, CSRF construction and error codes live here |
| `.agent/drift/2026-Q3.md` | 68 | **NEVER whole.** A drift line below is about to be your problem; `grep` its date and subject |
| `.agent/contract-history.md` | 17 | reconciling hash history, or a promote looks out of sequence. The CURRENT hash is in `## Contract` below — that is all a normal dispatch needs |
| `.agent/ROADMAP.md` | 14 | sequencing the next card |
| `.agent/AUDIT.md` | 16 | auditing process compliance |

### Contract

`contracts/openapi.json` is 420 KB. **Never load it.** `jq` the slice your card names:

```
jq '.paths."/api/v1/pupils"' contracts/openapi.json
jq '.components.schemas.PupilDto' contracts/openapi.json
jq '.paths | keys | length' contracts/openapi.json
```

## Layout

```
backend:   ./backend — solution SchoolManagement.slnx. Api / Application / Domain /
           Infrastructure under src/; Unit, Integration and Architecture tests under tests/.
           First real domain is Domain/Auth/ (TASK-0003); /api/v1/reference/* and /health/*
           remain scaffolding.
frontend:  ./frontend — Vite 8 / React 19 / TypeScript 6, package gra-school-portal, npm
           (lockfile committed). Design tokens, Base UI, axios transport in src/lib/http/,
           TanStack Query, Zustand, MSW, react-hook-form + zod, Playwright. Top-level:
           src/api/ app/ components/ config/ features/ lib/ stores/ test/.
           features/<feature>/ is the documented home for screens; features/auth/ is its
           first tenant (TASK-0021). screens/ DELETED 2026-09-06. shared/ EXISTS since 2026-09-14
           (`shared/forms/field-message.ts`, 13 cross-feature importers) — created by the commit
           promoting the first genuinely shared helper, never in advance (CONVENTIONS.md:126-130).
contract generator: Microsoft.Extensions.ApiDescription.Server/10.0.10 via
           backend/scripts/generate-openapi.ps1 -Promote — the ONLY sanctioned way
           contracts/openapi.json and CONTRACT.lock change.
client generator:   openapi-typescript@7.13.0 (types only, pinned exact, devDependency).
           generate-api-schema.mjs writes src/api/schema.d.ts (npm run generate:api);
           check-api-schema-drift.mjs is the drift check-2 gate. src/api/client.ts is the
           hand-written typed request helper over src/lib/http/.
repo:      git, branch main, origin https://github.com/maxcotech/school-management-proj.git
```

## Toolchain present on this machine

dotnet SDK 10.0.100 · node v22.21.0 · npm 10.9.4 · yarn 1.22.22 · pnpm ABSENT · pwsh ABSENT ·
docker **ABSENT ON THE WINDOWS HOST, LIVE IN WSL2** (corrected 2026-09-16 — the old flat "docker
ABSENT" was read as "no container runtime anywhere" and is why the container path went unexercised
for three weeks). Engine 29.8.1 in WSL2 Ubuntu as `dockerd -H fd:// -H tcp://0.0.0.0:2375`;
**reachable from Windows at `http://localhost:2375` — NOT at `127.0.0.1:2375`**, WSL2 NAT-mode
localhost forwarding answers on the hostname path only. `$env:DOCKER_HOST` is set at user scope to
the broken IPv4 literal. No Docker Desktop, no `\.\pipe\docker_engine`, no `dotnet` inside WSL.
**CORRECTED 2026-09-17: local `ci.ps1` runs were NOT container-backed. `~/.gras/pg-test.txt` (hosted Neon)
exists and the script prefers it over the container, so gates silently ran on Neon.** Human directive:
local container first, hosted ONLY on human confirmation per run (`rules/gates.md` §7; enforced by TASK-0078).
Testcontainers against the WSL daemon has been available since TASK-0065
(2026-09-16): 322 tests in ~4 min, and **proven green with the network physically disconnected** —
the WSL bridge survives the adapter going down. `POSTGRES_TEST_CONNECTION` still wins when set, and
CI still uses a service-container Postgres · psql ABSENT · git 2.51.1.windows.1 · gitleaks 8.30.1

**PINNED — changing one side alone re-breaks CI (0024, 0026):** `global.json`
`rollForward: latestPatch` · gitleaks **8.30.1** in `backend-ci.yml` must equal local · Node
`22.21.0` · **`AnalysisLevel 10.0-All` plus `Microsoft.CodeAnalysis.NetAnalyzers` 10.0.100, forced
over the SDK copy by `backend/Directory.Build.targets`** · `TestingPlatformDotnetTestSupport=false`.
CI prints `dotnet --version`. Re-run the `/analyzer:` check in that targets file after any bump.

## Contract

**Current: `0a74f0192c7371ed33c20df05c283b992a3383bc2c879cceebf246801b13be59`** · **95 paths** ·
**212 schemas** · api version `v1` · moved 2026-09-22 by annual computation (§6.7.10).
Previous: `bb29ee1ebe04…` / 94 paths / 211 schemas, pin printing on 2026-09-22; before that `5d01b0800150…` (pin batches), `b4bf1cf369f2…` (withdraw/reopen), `d1816d8c15b8…` (publication), `905632799f5c…` (TASK-0005b), `e7113c87a76b…` (TASK-0090), `584a4a3c9d5c…` (TASK-0088), `42d8e3b52ba4…` (TASK-0086), `9c2f8d55fe3a…` (TASK-0083), `8e3087d93f02…` (TASK-0072), `0ebca075110e…` (TASK-0071),
`84b46211e9fc…` (TASK-0077), `c5c4d6c6b8d4…` (TASK-0076), `57ea95b44bd4…` (TASK-0070), `152dc1c27db7…` (TASK-0069).

**Annual computation additive verified mechanically** (`jq`): +1 path, +1 schema, nothing else changed.

**Pin printing additive verified mechanically** (`jq`): +2 paths, no schema change.

**Pins additive verified mechanically** (`jq`): +6 paths, +8 schemas, nothing else changed.

**Withdraw/reopen additive verified mechanically** (`jq`): +2 paths, +2 schemas, nothing else changed.

**Publication additive verified mechanically** (`jq`): +1 path, +1 schema, nothing else changed.

**TASK-0005b additive verified mechanically** (`jq`): 3 paths + 3 schemas added, nothing removed, no path changed; `SettingsIdentityGroupDto`
gains response-only `logo`/`signature` (in no request body: checked).

**TASK-0090 additive verified MECHANICALLY by the orchestrator** (`jq`, description/example stripped): 2 paths and 3 schemas added, nothing removed,
zero existing paths changed. ONE existing schema changed: `ResultSetSummaryDto` gains response-required nullable `returnReason`; it appears in no
request body (checked), so additive, as `SectionDto.ratesTraits` was.

**TASK-0088 additive verified MECHANICALLY by the orchestrator** (previous document copied aside, `jq` diff, description/example stripped):
0 paths / 0 schemas removed; 2 paths added (`/arms/{armId}/readiness`, `/result-sets/{resultSetId}/submit`), 11 schemas added; ZERO existing paths
or schemas changed. The submit 422 references `ResultSetNotReadyProblemDetails` (the first typed problem extension) with `content` present.

**TASK-0086 additive verified MECHANICALLY by the orchestrator** (previous document copied aside, `jq` diff): 0 paths / 0 schemas removed;
5 paths added (`/arms/{armId}/attendance`, `/class-teacher-remarks`, `/head-teacher-remarks`, `/remark-templates`, `/remark-templates/{id}`),
13 schemas added; the one changed path (`/terms/{id}`) is byte-identical once `description` is stripped (new 409 in text only); zero existing
schemas changed with description/example stripped.

**TASK-0083's promotion (previous hash):** additive verified mechanically (the previous document was copied aside and diffed
with `jq`). 0 paths removed, 0 schemas removed. Added: 2 paths (`GET`/`PUT /api/v1/arms/{armId}/trait-ratings` and
`/development-ratings`) and 11 schemas. The 3 changed paths (`PUT /settings/rating-scales`, `/development-domains`, `/traits`)
are byte-identical once `description` is stripped: the new R2 409s are documented in text only. Changed schemas:
`SectionDto` gains a response-required `ratesTraits`; `CreateSectionCommand`/`UpdateSectionCommand` gain it OPTIONAL,
with `required` unchanged; `SectionListResponse` changes only in its `example`. No existing property changed.
(TASK-0072's promotion paragraph, which this replaces: `decisions/2026-Q3.md`, TASK-0072 closure.)

**Updated by the closing card at promotion.** Promotion run by the ORCHESTRATOR
(`generate-openapi.ps1 -Promote`); `CONTRACT.lock` written in the same run. **Additive verified
MECHANICALLY, not read off the card:** every existing schema's `required` array, every shared
property's shape and every property name diffed against the previous document, plus every existing
path compared whole — **2 paths added, 9 schemas added, zero removals, zero new required properties on
an existing schema, zero changed paths.** The ledger gate caught this block being stale on the first
try, which is what TASK-0075 built it for.

**Updated by the closing card this time, at promotion, not afterwards** — the failure mode the three
corrections below describe. Promotion run by the ORCHESTRATOR (`generate-openapi.ps1 -Promote`);
`CONTRACT.lock` written in the same run. **Additive verified MECHANICALLY, not read off the card:**
every existing schema's `required` array, every shared property's type, and every property name
diffed against the previous document — 8 paths added, 21 schemas added, **zero removals, zero new
required properties on an existing schema, zero type changes**. This is the check TASK-0069 failed.

**Corrected 2026-09-16 — AGAIN, and this is the THIRD time this block has gone stale the same way.**
It still named TASK-0063's `b293db2b…` / 51 / 93 after TASK-0069 moved the contract on 2026-09-16;
closing 0069 updated the archive and not this block, exactly as closing 0062 did before it (see the
2026-09-15 correction this replaces). Verified mechanically against the working tree — `sha256sum
contracts/openapi.json` and `jq '.paths|keys|length'` — not read off a card. **`CONTRACT.lock`
matched the live document throughout; it was only this prose that drifted, so the lock is the
trustworthy source and this block is not. A closing card MUST update this block.**

**Corrected 2026-09-15:** this block still named TASK-0055's `7a3c84e6…` / 47 paths / 86 schemas
even though TASK-0062 moved the hash to `de4397b4164d…` on 2026-09-14. Closing TASK-0062 updated
the archive and not this block. Verified against the working tree, not prose.

- `CONTRACT.lock` matches this hash — written by `-Promote` in the same run, and re-verified by
  `ci.ps1`'s contract-drift and ledger gates (both PASS) on 2026-09-18.
- Frontend client is **CURRENT against this hash** (2026-09-22, `1c9f402`); `No drift`, typecheck and lint clean. Orchestrator re-ran `check:api-drift` (No drift) and verify (see TASK-0089). The orchestrator re-ran
  `check:api-drift` (No drift, exit 0) and `npm run verify` (55 files / 381 tests / build clean, exit 0). Types only, zero wrapper code.
- **`apiPut` exists, so the whole contract surface is reachable** — `UpdateAssessment`, `UpdateGrading`,
  `ResetGrading`, `SaveScoreSheet` and now `UpdateResultRules` are all callable, though none is called
  from application code yet.
- The additive classification was verified MECHANICALLY (every existing schema's `required` array
  and every property type diffed against HEAD), not read off the card — see `decisions/2026-Q3.md`.
- `/health/*` is excluded from the document (`ASSUMPTIONS.md` section 2.9); `/reference/*` is
  scaffolding.
- Path counts go stale in prose. The live answer is `jq '.paths | keys | length'`.
- Superseded hashes, and how each shape got its present form: `.agent/contract-history.md`.

## In flight

Open cards only. Closed: TASK-0001–0004, 0006–0029, 0031–0035, 0037–0045, 0047, 0048, 0049,
0050, 0051, 0052, 0053, 0054, 0055, 0059, 0061, 0062, 0063, 0064, 0065, 0066, 0067, 0069, 0070, 0073, 0074, 0075, 0076, 0077, 0078, 0079, 0080, 0071, 0081, 0072, 0082, 0083, 0085, 0084, 0086, 0087, 0088, 0089, 0090, 0091, 0005a, 0005c. Closure notes: `decisions/2026-Q3.md`.

**Corrected 2026-09-14:** this list previously read `0037–0044`, which silently claimed 0041, 0042
and 0043 as closed while the table below correctly showed them in `review`. Their card headers
also read `Status: done`. Neither was true — `decisions/2026-Q3.md` holds an implemented-to-review
entry for each and **no closure entry**, and TASK-0041's final Log line records nothing committed.
Card headers and this list now both say `review`. TASK-0045 was missing from this table entirely.

**Closed 2026-09-14 (same day):** all four — 0041, 0042, 0043 and 0045 — reviewed against
their acceptance criteria and closed on one orchestrator gate run. **TASK-0045 turned out to be
already implemented in full**, not queued: the reconciliation compared card headers against the
archive and never against the working tree, so an under-claiming header was invisible to it.
`decisions/2026-Q3.md`.

| Task | Title | Owner | Status |
|---|---|---|---|
| TASK-0060 | Enforce the session boundary in scope decisions | backend-dev | **queued 2026-09-15** — a grant scoped to one session currently authorises against a target in another. Cross-cutting |
| TASK-0058 | Stop an audit-write failure turning a 403 into a 500 | backend-dev | **dispatchable 2026-09-19**: ruled fail-open (403 + error log). Still behind product work |
| TASK-0056 | Emit a machine-readable gate summary file | backend-dev | **queued 2026-09-14** — context-budget pass |
| TASK-0057 | Index-and-archive `backend/docs/ASSUMPTIONS.md` | backend-dev | **queued 2026-09-14** — 108 KB, section 2 alone is 90 KB. Docs only; section numbers are immutable (65 files cite them) |
| TASK-0036 | End-of-session promotion | backend-dev | **blocked** — arms, pupils and enrolments now exist (0059); still needs annual results |
| TASK-0046 | Assignments read surface, rule 2, copy-to-session, 6.1.13 cascades, role archive | backend-dev | **NOT YET CARDED** — split from TASK-0030 on 2026-09-08 but no card file exists. Write it before dispatch (noticed 2026-09-14) |
| TASK-0068 | Stop `GET /pupils` dropping a pupil at a page seam | backend-dev | **dispatchable 2026-09-19**: part 1 ruled (b), comparison in SQL |
| TASK-0074 | Regenerate the typed client against `152dc1c2…` | frontend-dev | **DONE 2026-09-16** — drift gate re-run by the orchestrator: `No drift`, exit 0; typecheck and lint clean. 4 ops / 10 schemas consumed, no removals, pin and lockfile untouched. **Left one gap, deliberately and correctly: no `apiPut`, so two of the new ops are typed but uncallable** |
| TASK-0005b | Logo and signature uploads (Cloudinary) | orchestrator | **A–C done 2026-09-22**; stage D (Cloudinary adapter) left, needs the human's keys for the smoke test |

Full sequence and cards not yet written: `.agent/ROADMAP.md`.

## Decisions

- 2026-09-22 **Frontend F8 (audit log) done; admin frontend plan F1-F8 complete.** `/audit` filters (Lagos dates to UTC, action, record type, outcome), Lagos-time list with before/after details, load more, CSV export (audit.export). Not built: config-version history view, arm subject exceptions UI, promotion screens (backend not built).
- 2026-09-22 **Frontend F7b (ratings settings) done**: Settings → Ratings tab: rating scales (points ordered as listed, ids kept), traits per block with each block's scale (archive rather than remove a saved trait), nursery development domains per section with indicators (DomainCard). Privileges settings.ratingscales/traits/developmentdomains.update.
- 2026-09-22 **Frontend F7a (settings) done**: `/settings` tabs — School (identity + logo/signature upload and preview via getFile), Registration numbers (format + live preview, abbreviation change with CHANGE + reason), Grading (band table, reset to standard), Assessment (components, reorder, total shown, exam radio), Result rules (method/weights, position scope, tie-break, pass/promotion, core subjects). Each saves with its group version; optional reason field for post-publication changes. F7b (rating scales, traits, development domains) next.
- 2026-09-22 **Frontend F6 (pins) done**: `/pins` batches per session + generate dialog (uses >10 typed twice), `/pins/:id` counters, print slips / distribution list as saved PDFs (new `lib/http/download.ts` getFile+saveFile, arraybuffer so problem bodies decode), mark handed out (pin.generate), revoke batch, revoke/reinstate single pins with a reason. `ReasonDialog` and `LabelledSelect` promoted to `src/shared/`.
- 2026-09-22 **Frontend F5 (results workflow) done**: `/results` readiness grid (marks Complete/part/—, ratings, attendance, both remarks), counters, blockers, needs-recompute warning, and the state-driven actions: compute, submit (disabled while blocked), approve, return (reason 10-500), publish (Idempotency-Key), withdraw (reason), reopen, and "Compute annual results" on a published Third Term. Each button needs its privilege.
- 2026-09-22 **Frontend F4 (class records) done**: `/results/class` tabs — ratings (traits for a ratesTraits section, else development domains with comments), attendance (absent derived, capped at times opened), class and head teacher remarks (changed rows only, saved phrases, head teacher fill-empty; head teacher also edits while Awaiting approval/Approved). Tab editability follows the arm-scoped privilege.
- 2026-09-22 **Frontend F3 (marks) done**: `/results/marks` term + class + subject pickers, whole-sheet grid (blank ≠ 0, ABS, live total, 0..max whole-number validation blocks save, version sent for 409), locked outside Draft/Returned, return reason shown, void with reason. `hasPrivilegeInArm` added so a class teacher sees only their arms. Keyboard-down navigation not built.
- 2026-09-22 **Frontend F2 (subjects) done**: `/subjects` catalogue (create/edit/deactivate/delete) and `/subjects/mapping` grid per term (local edits, dry-run preview, apply; copy from another term; standard-list prefill; read-only when closed). Shared `shared/pickers/term-picker.tsx` + `use-term-choice.ts` (defaults to the active session/term, derived not stored); `test/mock-me.ts` promoted. Arm-level subject exceptions have no UI yet.
- 2026-09-22 **Frontend F1 (pupils) done**: `/pupils` list (search on submit, status filter), create dialog (Section A+B, duplicate check before create, "Create anyway"), `/pupils/:id` detail with edit (dirty fields only) and reasoned number correction. New shared `components/feedback/query-states.tsx` (loading/error/form error). Admin frontend plan: F1 pupils, F2 subjects, F3 score entry, F4 ratings/attendance/remarks, F5 results workflow, F6 pins, F7 settings, F8 audit/templates.
- 2026-09-22 **Human: school domain is goldenroyalark.com.** Portal runs on its own subdomain (exact name not yet chosen); `Portal__PublicUrl` (pin slips, QR) and the cookie domain derive from it.
- 2026-09-22 **Portal 3d-2 done**: terms list links Annual Cumulative once computed and Third Term is still published; `/portal/annual/{sessionId}` page + `/pdf` ("ANNUAL REPORT SHEET", no use spent, cached per annual row id). Prints no position (F.7), no promotion status (only after a committed batch, C), no verification marks (6.9.6 names the term sheet only). Session resolution shared by term and annual views (`PortalSessionResolver`).
- 2026-09-22 **Annual computation (3d-1) done**: `POST /arms/{armId}/annual-results` (`result.annual.compute`, unscoped privilege) writes `annual_result` per pupil of the final arm (pupils with a result in its Third Term set): cumulative average simple/weighted over terms sat (weights rescaled), grade from the Third Term snapshot bands, SharedPosition rank (single-term pupils unranked; `annual_pupil_count` = ranked count, not "complete three-term" count), subject means, proposed Promoted/Repeat (a core subject never taken does not count against). 8.4.9 figures reproduced. An unscored earlier term is skipped; an unpublished one blocks with the spec copy.
- 2026-09-22 **Portal 3c done**: A4 result PDF (QuestPDF, same `ResultSheet` as the page; 58 KB for 14 subjects), disk-cached per (set, pupil, revision), `GET /portal/result/{term}/pdf` spends no use; `result_verification` token per pupil per revision issued at publish; public `/verify` + `/verify/{token}` (30/address/hour, initials only, figures only while current). QR via QRCoder 1.8.0 (MIT). Token spec conflict: 6.9.6 (22 chars, groups of 5) followed over C.7 (12, groups of 4). Verify page omits position (neither sheet prints one, §6.7.12 amendment); withdrawn-state copy is ours.
- 2026-09-22 **Portal 3b done**: on-screen result from the snapshot via `ResultSheetBuilder` (shared with the coming PDF); snapshot now includes subjects, form teacher, session, term dates, string enums. Fee block not printed (fee notice not built).
- 2026-09-22 **Portal 3a done** (§6.9): lookup, 30-min viewing sessions, blocks, spread control, 6.9.4 copies, 400 ms pad; no contract change. Global soft ceiling and the 5-pupils-per-hour address flag not built yet.
- 2026-09-22 **Human: parent portal is server-rendered HTML from the .NET app** (no framework, works without JS, §6.9.8), and runs **in the same process on its own subdomain** (not a separate deployment; restricted DB role deferred). Pin printing gate 1381/1381.
- 2026-09-22 **Pin printing done**: slips PDF (4/A4) + distribution list, QuestPDF 2026.9.0; nightly `PinMaintenanceService` purges ciphertext and marks exhausted batches. Print is a GET that writes: fetch + blob only.
- 2026-09-22 **Pin batches done** (§6.8): generate/list/detail/distribute/revoke/reinstate; Argon2id (pin cost 4 MB/1 pass) + keyed HMAC + AES-GCM; 2000 pins ~34s. Contract `5d01b080…`.
- 2026-09-22 **Human: prod = PostgreSQL + app on one Namecheap Pulsar VPS** (open question 5 resolved). **NDPA: not strict for this school**, not certifying; don't gate features on it.
- 2026-09-22 **Human: keep pins UNBOUND** (§6.8.2, reconfirmed). **Withdraw/reopen gate 1466/1466.** **PDF library: QuestPDF** (Community licence).
- 2026-09-22 **Withdraw/reopen done** (§6.7.9): Super Admin, reason ≥10; reopen needs an active term; `result_set_snapshot` keeps every revision. Contract `b4bf1cf3…`.
- 2026-09-22 **Publication done** (§6.7.9): `POST /result-sets/{id}/publish`, school-wide, snapshot written; contract `d1816d8c…`. Level-position re-check skipped: no sheet prints positions.
- 2026-09-22 **LEAN MODE** (human): orchestrator codes directly. **TASK-0005b A–C done**: upload + serving, multipart antiforgery 500 fixed; contract `90563279…`; gate 1359/1359. Stage D (Cloudinary) left.
- 2026-09-21 **TASK-0090 closed** — approve/return, school-wide; contract `e7113c87…`, 80 paths; gate 1384/1384. **The orchestrator's delta wrongly scoped both
  routes; the agent widened the privileges to comply; review reverted it** — FOURTH orchestrator delta error. → `decisions/2026-Q3.md`
- 2026-09-21 **TASK-0091 closed** — client current against `e7113c87…` (`be208a0`); verify 381/381. → `decisions/2026-Q3.md`
- 2026-09-21 **HUMAN RULINGS: hosting is a VPS; file storage is Cloudinary; image library SkiaSharp.** TASK-0005b written in full, four stages;
  Cloudinary as private (`authenticated`) store, API-proxied, processed locally first, assets immutable. → `decisions/2026-Q3.md`
- 2026-09-21 **TASK-0090 carded and dispatched** — approve/return, `ResultSetSummaryDto.returnReason`; one ~400-line stage. → `tasks/TASK-0090.md`
- 2026-09-21 **TASK-0089 closed** — client current against `584a4a3c…` (`2462299`); typed 422 unreachable via `ApiError`, readiness-screen card owns it. → `decisions/2026-Q3.md`
- 2026-09-21 **TASK-0088 closed** — recompute triggers, row lock, readiness, submit; contract `584a4a3c…`, 78 paths. Scoped gate 1401/1401. → `decisions/2026-Q3.md`
- 2026-09-21 **HUMAN DIRECTIVE: dev agents run no integration tests and no mutation proofs; the orchestrator does both once per card.**
  Each stage is one dispatch of ~400 lines, split at carding. `gates.md` §1, `governance.md` §2, `backend-dev.md`. → `decisions/2026-Q3.md`
- 2026-09-21 **TASK-0088 stage B implemented (`c9946d1`); dispatch died on the rate limit (SIXTH) mid RED/GREEN, mutation left in tree** —
  reverted by the orchestrator, which ran 17/17 new integration + RED/GREEN itself; promoted `584a4a3c…`, additive. → `decisions/2026-Q3.md`
- 2026-09-21 **TASK-0088 stage A done** (`5ecd9c4`, `a2913f7`) — 12 recompute triggers + result_set row lock; scoped gate 1379/1379 local container;
  race test was vacuous on review, rebuilt deterministic, RED/GREEN by two different mutations. Dispatch died on rate limit — FIFTH occurrence. → `decisions/2026-Q3.md`
- 2026-09-21 **TASK-0088 carded** — head remark gates publication only (not submit); recompute triggers built first in the same card;
  readiness arm-routed; submit 422 is the first typed problem extension; new result_set row-lock rule. → `decisions/2026-Q3.md`
- 2026-09-19 **TASK-0086 closed** — attendance, both remarks, remark templates, `ScopeResolution.AnyGrant`; contract `42d8e3b5…`, 76 paths.
  Full gate 1653/1653 on the FIRST run. → `decisions/2026-Q3.md`
- 2026-09-19 **TASK-0087 closed** — client current against `42d8e3b5…`, same PR. → `decisions/2026-Q3.md`
- 2026-09-19 **TASK-0086 stage A done** — attendance + both remarks, term guard; orchestrator scoped gate 1270/1270, Format fixed on
  bounce (CRLF + imports); contract drift expected until promotion after stage B. → `tasks/TASK-0086.md` log
- 2026-09-19 **TASK-0084 closed** — 401 is a legitimate interleaving (auth precedes lock); test split, deterministic, no product change.
  Scoped gate 1112/1112. Branch `task-0084` (`3cfc232`) off main, needs its own PR. → `decisions/2026-Q3.md`
- 2026-09-19 **Human rulings on TASK-0086**: L remarks 300 chars (not §6.7.7's 240); A attendance stores present only, absent derived;
  T two template lists by kind, class-teacher list at ANY scope; H head remark editable until Published. → `decisions/2026-Q3.md`
- 2026-09-19 **TASK-0084 runs parallel to a contract-moving card (0086)**, bending `rules/contract.md` §3's letter on the human's "alongside"
  instruction: separate worktree off main, 0084 contract impact none and must STOP otherwise. → `decisions/2026-Q3.md`
- 2026-09-19 **TASK-0083 closed** — trait and development rating entry, `ratesTraits`, R2 refusals; contract `9c2f8d55…`, 71 paths. Full gate
  1534/1534 on the FIRST run. → `decisions/2026-Q3.md`
- 2026-09-19 **TASK-0085 closed** — client current against `9c2f8d55…`, same PR. → `decisions/2026-Q3.md`
- 2026-09-19 **TASK-0083 stage 2 done (`0fae7b7`)** — nursery development ratings, real indicator usage gate; unit 1055/1055,
  integration 114/114 scoped. Point deletion under ANY rating is refused (FK), not only under open sets. → `tasks/TASK-0083.md` log
- 2026-09-19 **TASK-0083 stage 1 done (`27d7bdf`)** — trait ratings, `ratesTraits`, real trait usage gate; unit 1023/1023, integration
  103/103 scoped; ~2190 lines. Mirrors score sheets, including their 404 and per-cell validation shape. → `tasks/TASK-0083.md` log
- 2026-09-19 **TASK-0083 contract delta approved** — arm-scoped GET/PUT trait-ratings and development-ratings, `SectionDto.ratesTraits`,
  R2 409s; Q1-A partial saves, Q2-A departed pupils omitted, Q3-A comment needs a point. → `decisions/2026-Q3.md`
- 2026-09-19 **Human rulings**: TASK-0083 R1-A `section.ratesTraits`, R2-A refuse scale change under open ratings, R3-A reuse
  `result.trait.enter`; TASK-0058 fail-open (403 + error log); TASK-0068 (b) comparison in SQL. → `decisions/2026-Q3.md`
- 2026-09-19 **An agent pushed `task-0072` straight to `origin/main` (3 pushes, 2026-09-18 21:34-21:55); now blocked three ways**:
  a rule in the agent files, `git push` denied in `.claude/settings.json`, and a local `pre-push` hook refusing main/staging. → `decisions/2026-Q3.md`
- 2026-09-18 **TASK-0072 closed** — rating scales, development domains and traits are records; contract `8e3087d9…`, 69 paths.
  Full gate 1437/1437 on the 3rd run (a privilege-count fix, then the known concurrency flake). → `decisions/2026-Q3.md`
- 2026-09-18 **TASK-0082 closed** — client current against `8e3087d9…`, generated only, same PR as the promotion. → `decisions/2026-Q3.md`
- 2026-09-18 **TASK-0072 stage 2b done (`b9ee49f`)** — `PUT /settings/development-domains`; stage 1's 10 unit failures fixed by
  reason (95 privileges). Unit 972/972 whole project. Snapshot ripple ~1720 lines; stage 3 refactors it first. → `decisions/2026-Q3.md`
- 2026-09-18 **TASK-0072 stage 2a done (`fa55845`)** — domain/indicator tables, 4/45 seed, `RatingScaleUsageGate` real. Stage 1's
  unit filter hid 10 failures (privilege counts, `PipelineTests` DI fakes); dev unit runs are now whole-project. → `decisions/2026-Q3.md`
- 2026-09-18 **TASK-0072 stage 1 done** — rating scales as records, `PUT /settings/rating-scales` (`db84f56`); review fix
  `d63231b` makes scale/point ids stable across saves (was: new ids every save). ~1840 hand-written lines vs ~400 budget. → `decisions/2026-Q3.md`
- 2026-09-18 **TASK-0072 contract delta approved by the human** — 3 additive PUT /settings/* routes, per-block scale ids,
  3 stages, promote once at stage 3; open questions 1-4 settled as recommended. → `decisions/2026-Q3.md`
- 2026-09-18 **TASK-0081 closed — client current against `0ebca075…`**, one generated file, zero wrapper code. Carded after
  PR #5 went red on frontend `check:api-drift` alone; drift and verify re-run by the orchestrator (381 tests). → `decisions/2026-Q3.md`
- 2026-09-18 **TASK-0071 closed: result computation engine + §8.4 regression fixture.** Contract `0ebca075…`, 66 paths,
  additive verified mechanically. Survived a hung session: stages 1–2 checkpointed, stage 3 re-dispatched on the partial
  files, where the agent fixed two build breaks. Full gate 1330/1330, Skipped 0, local container. → `decisions/2026-Q3.md`
- 2026-09-18 **HUMAN RULINGS on TASK-0071:** a fractional average is banded by threshold (84.60 is B, 85.00 is A),
  like `passMark`; an unknown result-set id is 403 and the contract drops the unreachable 404. → `decisions/2026-Q3.md`
- 2026-09-18 **TASK-0080 closed — client current against `84b46211…`**, one generated file, zero wrapper code. Drift and
  verify re-run by the orchestrator (381 tests, Skipped 0). First dispatch under the `LEDGER ACCOUNT` rule; held. → `decisions/2026-Q3.md`
- 2026-09-18 **HUMAN RULING on the seeded result rules: keep `requireCorePass: true` and the strict validator.**
  The screen requires core subjects on first save (pre-selects English + Mathematics, admin confirms); Third Term
  publication and promotion refuse the incomplete state. Rejected: seed false, validator exemption. → `decisions/2026-Q3.md`
- 2026-09-18 **HUMAN RULING: only the orchestrator writes `.agent/**`.** Dev agents return a `LEDGER ACCOUNT`
  section; both agent definitions and `governance.md` §1 amended. Ends a twice-recorded scope breach. → `decisions/2026-Q3.md`
- 2026-09-18 **TASK-0077 closed — result rules settings exist; contract `84b46211…`, 65 paths, additive verified
  mechanically.** Three judgment calls surfaced by the agent; **two were errors in the orchestrator's own delta**
  (snake_case enums, unprefixed error code — third such instance). Optimistic concurrency added by delta amendment.
  Full gate ALL PASSED, 1267/1267, Skipped 0, local container. → `decisions/2026-Q3.md`
- 2026-09-17 **TASK-0079 closed — client regenerated against `c5c4d6c6…` and `apiPut` added, ending the
  zero-new-wrapper-code streak exactly where `src/api/README.md` predicted.** CSRF needed no interceptor
  change and the orchestrator VERIFIED why (denylist, not allowlist — an allowlist would have been a silent
  bypass). Non-vacuity proven by two different mutations, the orchestrator's independent of the agent's.
  `No drift` exit 0; verify 381 tests, Skipped 0. → `decisions/2026-Q3.md`
- 2026-09-17 **TASK-0076 closed — result sets, subject scores and the score sheet exist.** Contract `c5c4d6c6…`,
  64 paths, additive verified mechanically. **Two production bugs found by the orchestrator gate over a dead
  dispatch's tree**, neither self-reported: version-before-state ordering, and a `jsonb` re-serialisation bug that
  would have broken §6.7.4's autosave. Three agents, one rate-limit death. → `decisions/2026-Q3.md`
- 2026-09-17 **HUMAN RULINGS on TASK-0071**: level position from all arms' live marks, written for own arm only;
  Draft compute over gaps follows §8.3 literally. → `decisions/2026-Q3.md`
- 2026-09-17 **TASK-0076 dispatch A done**: result_set, subject_score, roster, four placeholders made real, term-close
  precondition. Contract unmoved. → `decisions/2026-Q3.md`
- 2026-09-17 **HUMAN RULING: Returned for Correction blocks term close** (with marks entered); a returned set
  would otherwise be stranded uneditable. Departs from §6.3.6's literal list. → `decisions/2026-Q3.md`
- 2026-09-17 **TASK-0078 closed: `ci.ps1 -UseHostedDb` is the only path to hosted Neon; Secret scan green again.**
  First local-container gate: integration 342 in 7m26s against 45+ min hosted. Pre-flight probe withdrawn by ruling. → `decisions/2026-Q3.md`
- 2026-09-17 **HUMAN DIRECTIVE: integration runs use the local container; hosted DB only on the human's per-run
  confirmation.** A TASK-0076 gate had silently run on Neon via `~/.gras/pg-test.txt`. `rules/gates.md` §7; TASK-0078.
- 2026-09-17 **TASK-0071 held at pre-dispatch; Phase 3 re-carded 0076 → 0077 → 0071.** Its input and output
  tables did not exist, nor did §6.2.8 result rules. Score-sheet routes depart from §6.7.13's query form
  because scope reads route values only. Two questions on 0071 await the human. → `decisions/2026-Q3.md`
- 2026-09-17 **TASK-0070 closed — subjects, level mappings, per-arm exceptions and the §8.1 resolver
  exist.** 867 tests, Skipped 0; contract `57ea95b4`, 62 paths, additive verified mechanically. Card
  was unbuildable as written: **five defects found before dispatch**, including a seed the migration
  could never apply and a privilege split letting `Map` end every mapping in a term. **Two of the
  corrections were to the orchestrator's own delta**, which named an error code that never existed
  and was never transcribed into the archive it was approved into. → `decisions/2026-Q3.md`
- 2026-09-17 **TASK-0070 dispatch 2 died a SECOND time, on the weekly limit (resets Sep 21 12:00).**
  Implementation complete — 8 paths, all handlers, migration, DI, fixture reseed; build clean under
  `10.0-All`, architecture 33/33. **Zero tests written**, and 7 `PipelineTests` failures traced to
  two missing `Substitute.For<>` registrations, not a DI bug. Card cannot close. →
  `decisions/2026-Q3.md`
- 2026-09-17 **TASK-0070 dispatch 2 died on a session rate limit — FOURTH occurrence** (0003, 0019,
  0005a, 0070); tree inventoried before anything was believed about it, seed verified mechanically
  against the card, agent resumed rather than re-dispatched. **A line count is not progress.** →
  `decisions/2026-Q3.md`
- 2026-09-16 **TASK-0073 closed — the password-redaction test now proves what it claims, and the
  full suite is 322/322 for the first time.** Mechanism was none of the FIVE hypotheses, including
  the orchestrator's three and the "platform" lead its own card named: `AddSerilog` defaults to
  `preserveStaticLogger: false`, so disposing ANY of the suite's 18 hosts calls `Log.CloseAndFlush()`
  and silences every other live host. The test captured nothing because nothing was logged. Fixed
  with one named parameter; `AuthLogRedactionTests.cs` has ZERO diff, so the positive control is
  intact. Verified independently by the orchestrator: 322/322, Skipped 0. → `decisions/2026-Q3.md`
- 2026-09-16 **TASK-0073 implemented by backend-dev — the redaction test's vacuous-pass mechanism
  found and fixed, not worked around.** `ObservabilitySetup.ConfigureSerilog`'s `AddSerilog` call
  defaulted to `preserveStaticLogger: false`, which (per `Serilog.Extensions.Hosting`'s own source,
  fetched and read, not assumed) makes every host's logging read the process-wide static
  `Serilog.Log.Logger` dynamically; this suite's 18 `WithWebHostBuilder` hosts each rebuild that
  logger, and disposing any one of them resets the ambient logger to Serilog's no-op
  `SilentLogger`, silencing the shared fixture's own logging (confirmed in situ via a temporary
  probe) — not a `Console.Out` bug, contrary to every prior hypothesis. Fixed with
  `preserveStaticLogger: true`; two consecutive clean full-suite runs post-fix (322/322 each,
  reproduced 321/322 three times pre-fix); not-vacuous proof done both directions (RED with an
  injected plaintext leak, GREEN after reverting it). One file changed
  (`ObservabilitySetup.cs`), no contract impact. **Awaiting the orchestrator's scoped gate run to
  close** — see `gates.md` §1. → `decisions/2026-Q3.md`, `.agent/tasks/TASK-0073.md` Log
- 2026-09-16 **TASK-0075 closed — the ledger's contract block is gated, and `CONTRACT.lock` is
  verified for the first time.** Three independent assertions in `ci.ps1` gate 11. **The existing
  contract-drift gate never read the lock at all** — trusted by every agent, verified by nothing.
  Each assertion proven to fail independently; the agent also falsified its own SELF-TEST, unasked.
  Orchestrator re-ran with a DIFFERENT mutation on a copy. **Gate runs LAST on purpose** — first
  would block a contract-moving dev agent on bookkeeping it is forbidden to fix. →
  `decisions/2026-Q3.md`
- 2026-09-16 **TASK-0065 closed — the integration suite no longer needs the network.** 322 tests in
  **3m40s offline** against 45+ min hosted; a missing database now FAILS instead of skipping. **The
  card was only possible because `docker ABSENT` in this file meant the WINDOWS HOST — Docker was
  live in WSL2 the whole time, and two probe methods gave true-but-misleading negatives that cost a
  withdrawn architectural ruling.** AC-4 proven offline by the human; the orchestrator cannot prove
  it, since it reaches this machine over the network. Closed at 321/322 by human ruling — the
  residual failure is TASK-0073. Four mechanisms for it proposed and refuted, three orchestrator's.
  → `decisions/2026-Q3.md`
- 2026-09-16 **TASK-0069 closed — grading scale and assessment structure exist.** Nine bands,
  20/20/60, ten grading rules and six assessment rules server-side, session lock, both seed
  profiles. Contract additive: 54 paths, 102 schemas, hash `152dc1c2`. Final gate 745/33/10,
  Skipped 0 everywhere. **Five review findings, none self-reported** — two "already solved"
  architecture failures that were real, a delta called additive that added required properties,
  a validator rule-order deviation, a missing `ApiTestFixture` reseed that made the fresh-database
  criterion fail, and a write outside the agent's scope. → `decisions/2026-Q3.md`
- 2026-09-16 **Conflicts 1, 2 and 4 are settled; results are unblocked.** The orchestrator had
  reported 1 and 2 as awaiting a human ruling. They were **already resolved by the school** and the
  spec was written to them — the real blocker was that `13-result-computation-rules.md` §8.4, which
  the spec itself designates as the regression fixture, still computed against the superseded
  15/15/10/60 structure and six-band scale. Restated against 20/20/60 and nine bands, holding every
  `subject_total` constant so §8.4.4-§8.4.7 carry over unchanged; every figure re-derived by an
  independent arithmetic check. Conflict 4 ruled by the human: Parent's Comment is **optional**,
  written by a teacher or administrator under the existing `weekly.enter`, portal stays read-only.
  No item in `25-open-conflicts-to-resolve.md` now blocks build. → `decisions/2026-Q3.md`

- 2026-09-16 **TASK-0067 closed — the scoped gate exists.** `-SkipIntegration` / `-IntegrationFilter`;
  a stage skipped BY REQUEST can never read as a pass, and `Skipped > 0` still fails in every mode.
  **Review caught a false-green: the zero-match check read the UNIT suite's `.trx`** when a filtered run
  wrote none. Diagnosing its own 6 network failures took 5m30s scoped vs 50m19s full. →
  `decisions/2026-Q3.md`

- 2026-09-16 **TASK-0061 closed on the FIRST scoped gate** (§ new policy): unit 700/700, arch 33/33,
  pupils integration 34/34, Skipped 0, contract unmoved — minutes, not the hour. The dispatch-time
  grep that found the shared `PupilListCursor` is what stopped the admissions queue breaking.
  **Hand-assembling the scoped gate went wrong 3x in 2 cards, none a code defect — see TASK-0067.**
  Two review findings carded as TASK-0068. → `decisions/2026-Q3.md`

- 2026-09-16 **HUMAN DIRECTIVE: the full gate is no longer run per card.** Cheap gates in full +
  scoped integration locally (minutes); the full suite is CI's job. **Root cause: `backend-ci.yml`
  triggered on `main` only while all work happened on `staging`, so the full run never fired and an
  hour of local Neon time was substituting for a CI job that was switched off.** CI now watches
  `staging`; `rules/gates.md` §0 is the new policy; TASK-0067 carded for `ci.ps1` flags.

- 2026-09-15 **TASK-0063 closed.** Reg-number correction + the permanent history alias; 1035 tests,
  Skipped 0, one Neon TLS drop re-run green in 13s against 12m51s. **A filtered `dotnet test` SKIPPED
  silently and still exited 0 — only `ci.ps1` enforces `Skipped > 0`. Read counts, never exit codes.**
  Contract `37f8b4c2d19d…` → `b293db2bc2b4…`. → `decisions/2026-Q3.md`

- 2026-09-15 TASK-0064 and TASK-0066 closed on a 10/10 gate. The admissions queue,
  approve and decline screens shipped; `frontend-dev` refused to guess two missing opaque ids and
  that refusal produced `GET /admissions/{id}` rather than a silent wrong-class defect.
  → `decisions/2026-Q3.md`

**Index only — one line per entry.** Full text: `decisions/2026-Q3.md` (grep the TASK id).
Implementation and closure of the same card are merged onto one line.

- 2026-09-15 **TASK-0051 closed.** Registration-number issue and the admission-approval transition;
  counter proven as a row lock, not a max query. 985 tests, Skipped 0, 5 triaged (2 a real stale-
  assertion regression, 3 Neon connection drops re-run green). **The orchestrator caused a gate/agent
  concurrency collision and stopped its OWN gate rather than the agent's DB-touching run.** Contract
  `de4397b4164d…` → `4b31521d833b…`. → `decisions/2026-Q3.md`
- 2026-09-15 **Two drift entries whose trigger named TASK-0051 had not been carried into the card**,
  which was written in full that same morning. The `governance.md` section 3 row 4 grep caught both.
  **Mechanical beats remembered, even for the agent who wrote the card hours earlier.** →
  `decisions/2026-Q3.md`
- 2026-09-15 **TASK-0062 closed, full green gate** (10/10, 953 tests, Skipped 0). `admission_record`
  sections A/I/J. **The orchestrator classified a BREAKING delta as additive** — a NEW required
  request field on `POST /pupils`; caught by contract-guardian at close, human signed off after the
  fact. → `decisions/2026-Q3.md`
- 2026-09-15 **TASK-0051 could not be written as one card: `admission_record` was never built and
  appeared in no dependency list, including its own.** Split three ways — TASK-0062 (the record),
  0051 (approve/decline/issue), 0063 (correction). → `decisions/2026-Q3.md`
- 2026-09-15 **Human ruling on TASK-0061: a pupil with NO open enrolment sorts LAST**, one flat
  trailing block, surname then id; non-null sentinel keys so the widened cursor never carries a
  NULL. Rejected: first, excluded (breaks search), and by-last-closed-enrolment. → `decisions/2026-Q3.md`
- 2026-09-15 **TASK-0059 closed, full green gate.** `enrolment` — the pupil-to-arm spine; the
  one-open-row invariant is a partial unique index proven in BOTH directions. 933/933, Skipped 0,
  contract unmoved. One AC split to TASK-0061. → `decisions/2026-Q3.md`
- 2026-09-15 **A card returned one AC short WITH the open question written down beats one returned
  complete and guessed.** 6.5.15 is silent on where an unenrolled pupil sorts. →
  `decisions/2026-Q3.md`
- 2026-09-15 **Judge a running gate by TWO samples, never one.** The test DB is a Neon POOLER
  endpoint, so an idle `pg_stat_activity` session is a pooled connection, not the worker; and
  `ci.ps1` logs nothing until its summary. The orchestrator misread silence as a hang twice, once
  killing a healthy run and corrupting the database. → `decisions/2026-Q3.md`
- 2026-09-14 **TASK-0053 closed, full green gate.** CSV formula injection neutralised in the audit
  export; neutralise-then-quote ordering is the substance. 917/917, Skipped 0, contract unmoved. →
  `decisions/2026-Q3.md`
- 2026-09-14 **Silence from `ci.ps1`'s integration stage is NOT evidence of a hang.** It logs nothing
  until the final summary, and `ResetDatabaseAsync` runs per test against hosted Neon at ~8s/test, so
  256 tests are 35+ minutes of silence. The orchestrator killed a healthy run, corrupting the shared
  database mid-reseed and costing ~2 hours. **Get evidence a run is stuck before killing it.** →
  `decisions/2026-Q3.md`
- 2026-09-14 **Human priority ruling: critical product features before further audit work.** TASK-0058
  deferred (not dropped; its drift trigger still names it); TASK-0059 (enrolment) opened as the first
  critical card — the stated blocker on TASK-0051, TASK-0036 and two live drift entries.
- 2026-09-14 **TASK-0041, 0042, 0043 and 0045 CLOSED** on one orchestrator gate run — back-office
  shell and guards, academic structure, admins and roles, arms. Contract unmoved. →
  `decisions/2026-Q3.md`
- 2026-09-14 **TASK-0045 was already fully implemented while the ledger called it `queued`.**
  Today's own reconciliation reinstated it as "dispatchable"; dispatching it would have rebuilt
  ~1,800 lines of tested code. **A card's `Status:` is a claim about the TREE — reconciliation
  must grep the code for the card's id before believing it.** Headers drift in BOTH directions,
  and comparing them only against the archive cannot see the under-claiming half. →
  `decisions/2026-Q3.md`
- 2026-09-14 **`src/shared/` appearing is the CONVENTION FIRING, not a violation** — created in
  the commit that promoted the first genuinely cross-feature helper, exactly as
  `CONVENTIONS.md:126-130` prescribes. `## Layout`'s "deliberately absent" line was stale and is
  corrected. → `decisions/2026-Q3.md`
- 2026-09-14 **A drift trigger arrived on TASK-0043 and no acceptance criterion carried it**
  (spec 6.1.2 self-edit). The implementing agent flagged it rather than shipping around it, which
  is `rules/governance.md` §3's second half doing the job its first half missed. →
  `decisions/2026-Q3.md`
- 2026-09-14 **Context-budget pass.** CLAUDE.md 16 KB to 5 KB (router only); STATE.md 250 KB to
  this; the rule sections split to `.agent/rules/` one-file-per-reader; decision and drift full
  text now archived at card close rather than at quarter end. Orchestrator model dropped from the
  1M-context Opus variant. Subagents no longer run the full gate on either side.
- 2026-09-14 **Card sufficiency made the explicit counterweight to the context budget**
  (`rules/governance.md` section 3). Six questions a card must answer before dispatch; a mechanical
  `grep` of this file's drift index replaces the accidental discovery that a 250 KB ledger used to
  provide; `Reads:` is a budget to spend, not a cap to squeeze; and **an implementing agent is now
  required to bounce an underspecified card rather than guess or read everything.** The budget pass
  created this risk and this is what contains it.
- 2026-09-14 **Closed cards' `## Log` sections archived to `.agent/tasks/logs/`** — 25 cards, 2,501
  log lines out of the card bodies, each leaving a pointer plus its closure entry. The convention
  already existed (applied to eight cards on 2026-09-04, then abandoned); this resumes it. Cards
  over ~120 lines fell from 32 to 16, and the remainder is genuine body. **This is what makes
  `rules/governance.md` section 3 row 3 affordable** — a card that says "follow TASK-0049" is now
  pointing at a brief, not a 234-line transcript.
- 2026-09-14 **Ledger reconciled against the card files — three real disagreements found, all
  fixed.** (1) The closed list claimed `0037–0044`, silently including 0041/0042/0043, whose card
  headers ALSO read `done`; the archive holds no closure entry for any of the three and TASK-0041's
  last Log line records nothing committed. All now read `review`. (2) TASK-0022/0024/0025/0026 and
  0031/0032 card headers read `queued`/`review` for work the archive records as closed. All now
  read `done`. (3) **TASK-0045 — a complete, dispatchable frontend card — was referenced by no
  other file in the repo** and would never have been picked up. Now in `## In flight`.
  **Card `Status:` headers had drifted in BOTH directions, so neither side was reliably
  authoritative.** What resolved each case was `decisions/2026-Q3.md` — closure is real only where
  a closure entry exists.
- 2026-09-10 **TASK-0054 closed.** Audit-log client seam typed end to end against `7a3c84e6…`.
  The contract's first non-JSON (`text/csv`) response body resolved `SuccessBody` to `never`;
  fixed with a `ResponseBodyOf` helper, proven by a throwaway type probe before any transport
  code. **Reusable shape: when a shared type-level helper widens, enumerate what can reach the new
  branch — a green suite cannot prove a widening safe.** 350 tests, Skipped 0.
- 2026-09-09 **TASK-0055 closed, first-run pass.** `entityId` filter on both audit operations,
  resolving open question 14 as the human directed. Additive; 47 paths and 86 schemas unmoved.
- 2026-09-09 **TASK-0049 closed after one reopen.** Audit log read surface (`GET /audit-events`,
  cursor-paged, eight optional filters) plus CSV export. Reopened because the export's 200 carried
  no `content`: `.Produces(200, contentType:"text/csv")` supplies no response `Type` and the
  generator silently drops `content`. `.Produces<string>(...)` fixes it. Regression test asserts
  on the generated document.
- 2026-09-09 **Ledger baseline ruling: at closure time the correct baseline is the WORKING TREE.**
  Two HEAD-vs-working-tree false contradictions in one day. Now in `rules/contract.md` section 4.
- 2026-09-09 **TASK-0052 closed.** Client regenerated against `53820aa5…`, consuming 0005c's
  reg-number move and 0050's pupils move in one pass.
- 2026-09-09 **TASK-0050 implemented.** Pupil entity, the pending-exclusion invariant, register
  read surface. Landed 2,452 hand-written lines against a ~1,000 estimate — **the estimate was the
  orchestrator's error, not padding.**
- 2026-09-09 **HUMAN SIGN-OFF: rejected audit events are written on their own connection**, not
  the ambient transaction, departing from spec 14 section 9.3.
- 2026-09-09 **HUMAN SIGN-OFF: `audit_event` append-only is enforced at BOTH layers**, and the
  two-role provisioning is deliberate drift. The migration emits `REVOKE UPDATE, DELETE`.
- 2026-09-09 **TASK-0048 implemented.** `audit_event` persistence and the append-only guarantee.
- 2026-09-09 **TASK-0047 implemented.** Client regenerated against `c3cb88ff…`; all three
  assignment operations reachable through the existing generic wrapper.
- 2026-09-08 **TASK-0044 implemented.** Client regenerated against `84a3444a…`; seven arms
  operations, zero new wrapper code.
- 2026-09-08 **TASK-0041 / 0042 / 0043 implemented, all three to review.** Back-office shell plus
  protected routing plus School Settings; sessions and terms and levels and sections; admin
  accounts and roles. No contract change on any of the three.
- 2026-09-08 **Open question 13 RESOLVED by the human: LEAVE IT.** Spec 6.4.2's rejection messages
  for chain rules 4-plural, 5 and 6 stay unreachable; rule 3's message is accepted for those cases.
- 2026-09-08 **Human priority ruling: product surface comes before further backend modules.**
- 2026-09-08 **TASK-0040 implemented.** Client regenerated against `82870944…`; nine
  sections/levels operations, zero new wrapper code.
- 2026-09-08 **TASK-0038 closed.** Sections, class levels, eight progression-chain rules. Ten
  gates PASS, 594/594, Skipped 0, line 80.55%.
- 2026-09-07 **TASK-0037 implemented.** Client regenerated against `a618db62…`; eight
  sessions/terms operations, zero new wrapper code.
- 2026-09-07 **Open question 6 RESOLVED — root `.gitattributes` added.** The repo now has one
  line-ending policy instead of one that stopped at `backend/`.
- 2026-09-07 **TASK-0034 closed — `contracts/openapi.json` was never reproducible across
  platforms, and gate 10's byte-exact hash was right to say so.** Roslyn writes XML doc files
  platform-dependently; the document is now newline-normalised.
- 2026-09-07 **TASK-0033 — an acceptance criterion was checked off with no test behind it**, found
  by the coordinator reviewing the diff, closed the same session.
- 2026-09-07 **TASK-0028 dispatch 2 closed.** Role entity persisted, five endpoints, rule 2,
  contract `73316bdb…`. The raw-SQL decision reviewed and found sounder than its own justification.
- 2026-09-06 **TASK-0032 closed — gate 9 can now see the diff it is gating.** Two gitleaks passes:
  history, plus `--no-git` over the working tree.
- 2026-09-06 **TASK-0031 closed — the gate invocation stopped being a thing an agent has to get
  right.** `ci.ps1` resolves `POSTGRES_TEST_CONNECTION` itself; the wrapper script is DELETED.
- 2026-09-06 **TASK-0028 SPLIT on a hard entity dependency** and rescoped to roles plus register.
- 2026-09-06 **TASK-0029 closed — typed client seam complete, every gate re-run by the
  ORCHESTRATOR rather than accepted on report.**
- 2026-09-06 **TASK-0027 closed after one reopen on three gaps.** All seven `/admins*` endpoints,
  contract `1a2d8aff…`. Dispatch 2 died on a session rate limit.
- 2026-09-06 **Section 5 human sign-off GRANTED for TASK-0027** (open question 12 closed).
- 2026-09-06 **TASK-0005 delta APPROVED WITH FOUR AMENDMENTS; card SPLIT three ways** —
  0005a identity and ledger, 0005b uploads, 0005c registration numbers.
- 2026-09-06 **TASK-0005a implemented; its dispatch DIED MID-RUN on a session rate limit — the
  THIRD time on this project** (0003, 0019, 0005a). The work was on disk, unverified and
  unrecorded. **Standing lesson: a dispatch that dies after writing and before verifying leaves a
  working tree that looks finished and is not — run the gates yourself before believing a silent
  dispatch.**
- 2026-09-06 **TASK-0021 implemented.** Cookie auth seam plus sign-in and landing screens; bearer
  DELETED, not disabled. Its AC-5 was stale and was amended at dispatch.
- 2026-09-06 **TASK-0019 closed — idempotency substrate shipped, contract-neutral.** 264/264,
  Skipped 0. Proven-not-vacuous on review: a real-concurrency test, and redaction asserted against
  the STORED row rather than the replayed response.
- 2026-09-06 **TASK-0019 delta reviewed; card SPLIT THREE WAYS.** Reviewing the delta against the
  spec rather than against itself found two endpoints missing from a list spec 6.1.14 literally
  enumerates. Full text: `decisions/2026-Q3-contract-deltas.md`.
- 2026-09-06 **STATE.md size cap REMOVED (human directive).** What replaces it is archive
  discipline alone. No agent may defer, trim or skip a STATE.md append to save bytes.
- 2026-09-05 **STANDING LESSONS ON GATES** (0010, 0011, 0015, 0016, 0022–0026). **(1) A check that
  cannot be shown to fail is not a check** — break what it guards, watch it go red, then accept it.
  **(2) A gate is only as trustworthy as the reproducibility of its INPUTS** — before trusting
  green, ask what it reads that is neither committed nor pinned.
- 2026-09-05 **TASK-0022: the gate self-test had NEVER been able to run in CI** — its fixtures
  were `*.trx`-ignored and never committed. Reviewing the DIFF, not the agent's report, found it.
- 2026-09-05 **TASK-0023 / 0024 / 0025 / 0026 closed.** Hermetic vitest `test.env`; SDK, gitleaks,
  analyzer version and test bridge all pinned. **`AnalysisLevel: latest-All` was the real culprit —
  `latest` means "whatever SDK is installed", so the rule set was a property of the machine.**
- 2026-09-05 **Open question 11 RESOLVED (human): follow section 7** — `src/features/<feature>/`,
  react-hook-form plus zod, Playwright; no bulk rename, `src/shared/` waits.

Earlier decisions (bootstrap through 2026-09-04): `decisions/2026-Q3.md`.

## Known drift

**Index only — one line per entry, each with its trigger and owner.** Full text:
`drift/2026-Q3.md`. STRUCK entries are closed and live only in the archive.

### Live — product and spec gaps

- 2026-09-21 **The head teacher's remark gates publication only, departing from the §6.7.12 amendment's submission gate list** (human ruling,
  TASK-0088). *Trigger: next spec revision; the publication card enforces it per §6.7.9. Owner: human.* → `decisions/2026-Q3.md`
- 2026-09-19 **An attendance save racing a term update can leave derived absent negative** — each reads the other's committed state only.
  Unlikely (admin lowers opened while a teacher saves). *Trigger: TASK-0088 (AC B2 carries it). Owner: `backend-dev`.*
- 2026-09-19 **§6.7.7 says remarks are 240 chars and types both attendance figures; built 300 and present-only by ruling L/A.** *Trigger: next spec revision. Owner: human.*
- 2026-09-19 **Renaming a scale point's code or label in place is ungated, even when ratings use it.** Published sheets are safe ONLY if
  publication snapshots the scale legend (§6.7.12 requires it, and publication is not built yet); open sets would show the new code mid-term.
  *Trigger: the publication/snapshot card, or the rating-scales screen card. Owner: human ruling.* → `drift/2026-Q3.md`
- 2026-09-18 ~~**Appendix E.3 headings say 13 and 16; the lists hold 14 and 15.**~~ **RESOLVED 2026-09-19**: the human confirmed 14 and 15. The heading counts are the typo, and the seed is correct as shipped. → `drift/2026-Q3.md`
- 2026-09-18 **The school has not been told how fractional averages are banded** (ruled: threshold, 84.60 is B).
  It decides printed grades and the spec is silent. *Trigger: before the first result sheet is printed. Owner: human
  plus the school.* → `decisions/2026-Q3.md`
- 2026-09-18 **The seeded default result rules cannot be saved back unchanged** — `requireCorePass: true` with empty
  `coreSubjectIds` fails the PUT validator (422). **Ruled 2026-09-18** — screen requires core subjects; publication and
  promotion refuse the incomplete state. *Trigger: the result-rules screen card, the publication card, TASK-0036
  (all three carry it). Owner: those cards.* → `drift/2026-Q3.md`
- 2026-09-17 **Sibling arms' stored level positions can go stale** (TASK-0071 ruling: compute writes own arm only).
  *Trigger: the approval/publication card. Owner: that card.* → `drift/2026-Q3.md`
- 2026-09-17 **Term close also blocks on Returned for Correction (human ruling), beyond §6.3.6's literal list.**
  *Trigger: next spec revision or a card citing §6.3.6. Owner: human.* → `drift/2026-Q3.md`
- 2026-09-17 **`needs_recompute` will be set by mark changes only** — transfer, mapping and settings
  triggers are unbuilt after TASK-0076. *Built by TASK-0088 stage A except the transfer trigger; trigger now: the card that builds pupil transfers. Owner: `backend-dev`.* → `drift/2026-Q3.md`
- 2026-09-17 **Result-rules promotion lock not built** (§6.2.8 "editable until promotion is run").
  *Trigger: TASK-0036. Owner: `backend-dev`.* → `drift/2026-Q3.md`
- 2026-09-16 **`08-module-subjects.md` §6.6.2 still says "No subjects are seeded" — rev 3.1 reversed
  that and the superseded text was never deleted; THIRD instance of the §6.2.5-§6.2.7 pattern.**
  *Trigger: any card citing §6.6.2, and the subject creation-screen card. Owner: human.* →
  `drift/2026-Q3.md`
- 2026-09-16 **`subject.code` ships NULLABLE and unseeded — deliberate departure from §6.6.2's
  `Req: Yes`; nothing prints a code today and appendix C declares it optional.** *Trigger: the arm
  broadsheet card. Owner: that card.* → `drift/2026-Q3.md`
- 2026-09-16 **Three seeded subject pairs are probably one subject spelled twice (Handwriting/Hand
  writing, Phonics/Phonics-Diction, Creative skills/Creative Art); both Handwriting spellings appear
  on the school's own sheets.** *Trigger: before the first result sheet is printed. Owner: human plus
  the school.* → `drift/2026-Q3.md`

- 2026-09-16 **`04-module-school-settings.md` §6.2.5, §6.2.6 and §6.2.7 still print seed data that
  §6.2.13 replaces, and the superseded text was never deleted.** §6.2.5 shows six bands and a
  `String 2` grade letter (now nine bands, String 3); §6.2.6 shows 15/15/10/60 (now 20/20/60);
  §6.2.7 specifies one school-wide trait scale (now per rating block). A dev who reads the section
  its card cites, and stops, builds the wrong thing — so TASK-0069 and TASK-0072 each carry an
  explicit was/now table as a workaround. **Trigger:** any card citing §6.2.5-§6.2.7. **Fix:**
  delete the superseded tables and leave a pointer to §6.2.13. **Owner:** human — the specification
  is the school's document; the 2026-09-16 authorisation covered §8.4 specifically, not the spec at
  large. Ask before the next settings card.

- 2026-09-15 **A correction whose new number equals the pupil's CURRENT number is not rejected**, and
  poisons that pupil's next correction into a `23505` instead of a clean 409. *Trigger: the next card
  touching `POST /pupils/{id}/registration-number`, or the portal card. Owner: `backend-dev`, after a
  human ruling on reject-vs-no-op.*
- 2026-09-15 **TASK-0051's ISSUANCE path does not check `pupil_reg_number_history`** — the two-table
  uniqueness rule is enforced on correction only, so a freshly issued number could duplicate a retired
  alias and make the portal lookup ambiguous. *Trigger: the portal redemption card, or any card changing
  reg-number composition or counter-reset settings. Owner: `backend-dev`.*
- 2026-09-15 ~~**`src/api/schema.d.ts` is STALE and `check:api-drift` is RED.**~~ **STRUCK 2026-09-16
  by TASK-0074** — regenerated against `152dc1c2…`; drift gate re-run by the orchestrator, `No drift`,
  exit 0. Full text: `drift/2026-Q3.md`.
- 2026-09-16 ~~**The contract's first two `PUT` operations are typed but NOT CALLABLE — `client.ts` has
  no `apiPut`.**~~ **STRUCK 2026-09-17 by TASK-0079** — `apiPut` added, mirroring `apiPatch`; CSRF
  already covered (denylist interceptor, verified). Trigger fired a step early, via a regeneration card
  rather than the feature card it named. All four PUT ops callable, none yet called. → `drift/2026-Q3.md`
- 2026-09-17 **The `@ts-expect-error` type-level tests in `src/api/` fire real unawaited HTTP requests**
  — `void apiX(...)` still executes, so the request goes out with a literal `{armId}` segment and MSW
  matches it. Pre-existing since TASK-0029, not a production defect (`buildPath` is correct), but it
  injects misleading errors into unrelated failures. *Trigger: the next card touching
  `frontend/src/api/*.test.ts`, incl. the score-entry screen card. Owner: `frontend-dev`.* → `drift/2026-Q3.md`
- 2026-09-15 **Published result snapshots do not exist, so TASK-0063's AC 6 is VACUOUS, not proven** —
  no results module, only the `IResultSetArmLookup` seam. Verified by the orchestrator, nothing invented.
  *Trigger: the results-module card that first persists a published snapshot, which must carry the test.
  Owner: `backend-dev`.*
- 2026-09-15 **The admissions queue's `missing` column cannot see steps 3 to 8** — contacts, health,
  barred/pickup persons and documents have no entity, so an empty `missing` does NOT mean "ready to
  approve". *Trigger: each step 3-to-8 entity card, and TASK-0051. Owner: `backend-dev`.*
- 2026-09-15 ~~**`headOfSchoolName` has no settings-derived default.**~~ **STRUCK 2026-09-15 by
  TASK-0051** — defaults from `settings.head_teacher_name` at approval, a supplied value still wins,
  proven both ways. The source field was an ORCHESTRATOR RULING (6.5.9 names no setting; 6.2.3 has
  exactly one field of that shape) and is one line to reverse. Full text: `drift/2026-Q3.md`.
- 2026-09-15 **Approval's blocking conditions can only see sections A, B, I and J.** Steps 3 to 8
  have no entities, so approval succeeds against a record 6.5.11 would call incomplete. *Trigger:
  the LAST step-3-to-8 entity card must complete the 6.5.11 gate. Owner: `backend-dev`.*
- 2026-09-15 **`admission` is null on `GET /pupils/{id}`** — only the create response populates it, yet
  6.5.15's detail view names the admission block. *Trigger: the pupil detail-view card. Owner:
  `backend-dev`.*
- 2026-09-15 ~~**The same null-`admission` gap also blocks `GET /admissions`'s own queue rows.**~~
  **STRUCK 2026-09-15 by TASK-0066** — `GET /admissions/{id}` (`GetAdmissionRecord`) now returns
  `AdmissionRecordDto` with `sessionId`/`classAdmittedInto`, gated `pupil.view`. TASK-0064's Approve
  dialog consumes it and is fully implemented. Full account: TASK-0064 card Log.
- 2026-09-09 **`GET /audit-events/export` writes a row on a GET and carries no CSRF token.**
  Spec-mandated by TASK-0049's card, harmless today. **It stops being harmless if the screen
  triggers the download by top-level navigation** — it must use fetch plus blob. *Trigger: the
  audit-log screen card. Owner: whoever writes it.*
- 2026-09-09 **`NigerianGeography`'s 774 LGA names are UNVERIFIED** against an authoritative
  source, and a wrong name can block a real admission (6.5.4 requires a closed list).
  *Trigger: before the first real admission. Owner: `backend-dev` plus human.*
- 2026-09-09 ~~**Arm-scoped `pupil.view` / `pupil.update` is a tested mechanism but a NO-OP against
  today's data.**~~ **STRUCK 2026-09-14 by TASK-0059** — `PupilArmOfRecordLookup` resolves a pupil's
  real arm from their open enrolment; `GetPupilHandler`/`UpdatePupilBiographicalHandler`/
  `ListPupilsHandler` all use it. Full text: `drift/2026-Q3.md`.
- 2026-09-09 **`GET /pupils` default sort omits 6.5.15's class-progression half** — it sorts
  surname then id. **TASK-0059 looked at this and deliberately did NOT build it** — needs a widened
  keyset cursor plus a product decision on where a pupil with no open enrolment sorts, neither of
  which fit that card safely. *Trigger: TASK-0061, unblocked 2026-09-15 by the human ruling it carried (unenrolled pupils sort
  LAST); now queued for dispatch. Owner: `backend-dev`.*
- 2026-09-08 **Spec 6.1.2's self-edit carve-out is not built** — an admin editing their OWN
  `staffName` / `phone` without holding `admin.update`. **Its trigger ALREADY FIRED and was
  missed: TASK-0043 was "the next `/admins` card" and carried no AC for it** (flagged anyway by
  the implementing agent — `admins/api.ts:77`). Needs the ROUTE to accept the narrower
  authorisation before any screen can offer it. *Trigger: the next card touching `PATCH
  /admins/{id}` authorisation, or a human asking why self-service editing is absent.
  Owner: `backend-dev` FIRST, then `frontend-dev`.*
- 2026-09-08 **`DELETE /levels/{id}`'s reference check is PARTIAL and MORE PERMISSIVE than spec
  6.4.2's** "delete only where nothing has ever referenced the row". *Trigger: when history tables
  exist. Owner: `backend-dev`.*
- 2026-09-07 ~~**`POST /terms/{id}/close` does not enforce spec 6.3.6's result-set precondition.**~~ **STRUCK
  2026-09-17 by TASK-0076 A**, with marks entered, plus Returned for Correction by ruling. → `drift/2026-Q3.md`
- 2026-09-06 **`DELETE /roles/{id}` hard-deletes unconditionally, and section 9.4 says it must not
  once assignments exist.** *Trigger: TASK-0046 (role archive). Owner: `backend-dev`.*
- 2026-09-06 **6.1.7 rule 2 cannot be proven end-to-end by a real caller yet** — it needs an actor
  holding `role.update` and not `settings.grading.update`. *Trigger: TASK-0046.
  Owner: `backend-dev`.*
- 2026-09-06 ~~**`abbreviation.issuedCount` will ship as `null` and stay null.**~~ **STRUCK
  2026-09-15 by TASK-0051** — a live count off the pupil table by abbreviation prefix, never the
  counter. Two stale tests asserting `null` were found by the gate and corrected to `0`. Full text:
  `drift/2026-Q3.md`.
- 2026-09-06 **The logo-removal affordance is unrouted** — 6.2.11 requires rejecting "logo deleted
  with no replacement" and 6.2.12 enumerates no `DELETE` route to reject on. *Trigger: TASK-0005b.
  Owner: `backend-dev`.*
- 2026-09-06 **Admin-account audit events are LOG-ONLY**, not the transactional table 6.1.12
  describes. *Trigger: partly superseded by TASK-0048; re-check when the admin module next moves.
  Owner: `backend-dev`.*
- 2026-09-06 **`mustChangePassword` is detected but there is still no change-password screen** — a
  flagged account can only read a message. *Trigger: the card wiring `POST /auth/password` into a
  screen. Owner: `frontend-dev`.*
- 2026-09-06 ~~**The session-end redirect is subscribed per-screen, not once.**~~ **STRUCK
  2026-09-14 by TASK-0041** — `protected-layout.tsx:28` is now the sole production subscriber.
  Full text: `drift/2026-Q3.md`.

### Live — standing obligations on every card

- 2026-09-09 **`before_json` / `after_json` are a STANDING obligation, not a backfill card.**
  `before_json` is never populated; `after_json` carries only the pre-existing `metadata` argument.
  Every card writing an audited mutation must populate both. *Owner: `backend-dev`, every card.*
- 2026-09-09 **`beforeJson` / `afterJson` cross the wire as raw JSON TEXT, not parsed objects** —
  typed `string?` to stop the generator hoisting a shared description-less schema. Every consumer
  must `JSON.parse`. **Accepted.** *Trigger: revisit only if a third `JsonElement` case appears.*
- 2026-09-06 **Any card shipping a retry-duplicable mutation must DECLARE `Idempotency-Key` on
  that route** and mark one-time credentials with `RedactFromIdempotencyReplayAttribute`. Section
  9.8.2's four operations are EXAMPLES, not the list. *Live trigger: TASK-0005.
  Owner: `backend-dev`.*
- 2026-09-04 **The Api-to-Infrastructure arch test exempts `StartupEnvironmentGuard` as well as
  `Program.cs`.** *Trigger: a THIRD exemption must argue for itself or the type gets a port.*

### Live — defects and test gaps

- 2026-09-22 **OpenAPI drops nullable dictionary values.** `SaveTraitRatingsRowInput.ratings` is `Dictionary<string,string?>` on the server (null clears a rating) but the contract says `additionalProperties: string`; the frontend widens with one commented assertion in `trait-ratings-editor.tsx`. Development ratings avoid it with `{pointId: null}`. *Trigger: next contract change touching rating inputs: add a schema transformer for nullable dictionary values. Owner: orchestrator.*
- 2026-09-22 **Result PDF cache is never pruned and the QR needs `Portal__PublicUrl`.** Files are keyed by revision, so stale ones just accumulate (~60 KB each; a few hundred MB after years); without `Portal__PublicUrl` the sheet prints no QR or token. Also `/verify` partitions its rate limit on `RemoteIpAddress` (same proxy caveat as below). *Trigger: deployment: set `Portal__PublicUrl`/`Portal__PdfCacheDirectory`, add a prune to `PinMaintenanceService` if disk matters. Owner: orchestrator.*
- 2026-09-22 **Portal source address reads `RemoteIpAddress` directly.** Behind the VPS reverse proxy every parent shares the proxy's address, so the per-address block would hit everyone. *Trigger: deployment: configure ForwardedHeaders for the proxy. Owner: orchestrator.*
- 2026-09-22 **Generating 2000 pins takes ~34s locally in one request.** *Trigger: deployment: set the reverse proxy read timeout above 60s, or make generation a background job. Owner: orchestrator.*
- 2026-09-21 **EXIF orientations 2-5, 7 and 8 are implemented but untested** (only 1 and 6 have fixtures) in `SkiaSchoolImageProcessor`. *Trigger: a real
  upload comes out mirrored or rotated, or the next card touching that file. Owner: `backend-dev`.*
- 2026-09-21 **`admin-detail-screen.test.tsx` "suspend then reactivate" timed out at 5s in a loaded `verify` run** (380/381); passed 4/4 alone in ~3s. 2026-09-22: `bulk-create-arms-dialog.test.tsx` did the same once (402/404), both green alone and on the rerun. Both type through several fields; the fix is a 15s per-test timeout like `create-level-dialog.test.tsx`.
  *Trigger: if it recurs, raise its timeout or find the slow await. Owner: `frontend-dev`.*
- 2026-09-21 **`ApiError.problem` is one generic union, so `SubmitResultSet`'s typed 422 (`readiness`) is unreachable without a cast.** *Trigger: the
  readiness-screen card. Owner: `frontend-dev`.*
- 2026-09-19 **`CreateRoleAssignmentHandler` may return a default `createdAtUtc`** — `AuditingInterceptor` stamps it at SaveChanges, after the
  handler built the DTO (found by reading, UNVERIFIED; TASK-0086 used `TimeProvider` instead). *Trigger: next card touching assignments (TASK-0046). Owner: `backend-dev`.*
- 2026-09-19 **`RequireAuthenticatedCaller()`'s doc says "never an RBAC-gated business operation"; pupil and remark-template routes use it exactly so**
  (handler-level guards). *Trigger: next card adding a handler-level guard. Owner: orchestrator — amend the doc or add a named helper.*
- 2026-09-17 ~~**The `Secret scan` gate has been RED since TASK-0075.**~~ **STRUCK 2026-09-17 by TASK-0078**:
  fixture-dir allowlist, planted-credential proof, both passes `no leaks found`. → `drift/2026-Q3.md`
- 2026-09-16 ~~**A concurrency test answered 401 where it expects 409, TWICE now (recurred 2026-09-18 on TASK-0072's close gate). Carded as TASK-0084.**~~ **STRUCK 2026-09-19 by TASK-0084**: H1, racy test, product right; split per interleaving.
  `AdminAccountEndpointsTests.ChangeStatus_TwoSuperAdmins...`; did not recur in three clean runs.
  **Not obviously a flaky assertion** — the losing racer may be losing its SESSION, not the race,
  which a real user would experience as a logout rather than a conflict. *Trigger: the next card
  touching admin status changes, session invalidation or `/admins` concurrency. Owner: `backend-dev`.*
  → `drift/2026-Q3.md`
- 2026-09-16 ~~**`backend-dev` wrote to `.agent/**` despite the dispatch forbidding it.**~~ **STRUCK
  2026-09-18 by human ruling** — cause was the agent definitions both forbidding (line 7) and ordering
  (final bullet) the write. Both now hand back a `LEDGER ACCOUNT`; only the orchestrator writes the
  ledger. Held on TASK-0079 and TASK-0077. → `drift/2026-Q3.md`

- 2026-09-16 **The production seed path for `grading_band` / `assessment_component` is exercised by
  nothing.** TASK-0069's fresh-database tests pass through `ApiTestFixture`'s truncate-and-reinsert
  path, which proves the rows can be inserted and read — not that the migration's `HasData` lands on
  a genuinely empty database. Same gap applies to `school_profile`, the seeded roles and the class
  levels, which have had the same harness shape since TASK-0005a. **Trigger:** first deployment to a
  fresh environment, or any card that changes a `HasData` seed. **Owner:** whoever builds the
  deployment path (open question 5).
- 2026-09-16 ~~**`ISubjectScoreSessionLockLookup` and `IPublishedResultsGate` return false/0.**~~ **STRUCK
  2026-09-17 by TASK-0076 A**: real queries, tests fail against the placeholders. → `drift/2026-Q3.md`
- 2026-09-16 **`GradingScaleRules.ValidateWholeScale` reports rule 7 before rules 5 and 6.** Spec
  6.2.5 says "the first failure in the order listed here", so a scale that both starts above 0 and
  overlaps names the wrong rule. Cosmetic — both are real errors and the editor surfaces one at a
  time either way. **Fix:** move the rule-7 and rule-8 checks after the adjacent-pair loop.
  **Owner:** whoever next touches that method.
- 2026-09-16 **`GET /settings/impact` (6.2.12) is not built.** Depends on the same absent
  `result_set`. **Trigger:** the results module. **Owner:** that card.

- 2026-09-16 ~~**`AuthLogRedactionTests`' password-redaction test has been passing for the wrong
  reason its whole life.**~~ **STRUCK by TASK-0073** — mechanism was `AddSerilog`'s default
  `preserveStaticLogger: false` letting the suite's other `WithWebHostBuilder` hosts silence the
  shared fixture's logging, not `Console.Out`/platform. Fixed; two clean full-suite runs. →
  `drift/2026-Q3.md`, `decisions/2026-Q3.md`
- 2026-09-16 **`ci.ps1`'s "produced no `.trx` for this run" branch is UNTESTED** — the zero-match
  guard's other half (a new-path snapshot diff) is proven, but no case was constructed that makes
  VSTest write no `.trx` at all, because forcing that on demand is not reliably reproducible.
  *Trigger: the next card touching the integration stage of `ci.ps1`, or TASK-0065. Owner: `backend-dev`.*
- 2026-09-16 **`ci.ps1` prints `PASS: Coverage threshold` live while the SUMMARY records `N/A:`** —
  `Invoke-Gate` writes its own verdict before the correction is applied. The card's requirement is about
  the SUMMARY block, so this is accepted, not a defect. *Trigger: any card making the console output
  load-bearing (e.g. TASK-0056's machine-readable gate summary). Owner: `backend-dev`.*

- 2026-09-09 **An unaudited 403 becomes a 500.** `RejectedAuditEventWriter.WriteAsync` is awaited
  with no `try` / `catch` in either `SystemAuditSink.RecordRejectionAsync` or its caller.
  *Trigger: TASK-0058 (re-pointed 2026-09-14 — the old trigger "the next audit card" FIRED on
  TASK-0053 and was deliberately not folded into an encoding-only card). Owner: `backend-dev`.*
- 2026-09-09 **`PrivilegeDecision.cs:21`'s session-bearing-scope-filtering gap, RE-EXAMINED
  2026-09-14 by TASK-0059, still unresolved and no longer a `TODO(TASK-nnnn)` tag.** Enrolment
  existing does not resolve it: pupil routes still bypass `ScopeResolver`/`PrivilegeDecision`
  entirely via the handler-level `PupilAccessGuard` pattern, so `PrivilegeGrant.SessionId` is still
  never exercised by a real caller. The remark was rewritten to stop pointing at TASK-0002 (closed)
  and, since 2026-09-15, reads `TODO(TASK-0060)` — the orchestrator opened that card at TASK-0059's
  closure. **The gap in plain terms: a grant scoped to one academic session authorises the same
  action against a target in another.** *Trigger: TASK-0060. Owner: `backend-dev`.*
- 2026-09-05 **`vite build` succeeds with NO `.env` and emits a bundle that throws on boot**
  (it inlines `VITE_*` as `undefined`). CI copies `.env.example`, which masks it; nothing checks
  env at build time. *Trigger: any card touching build or deployment. Owner: UNOWNED.*
- 2026-09-05 **`gate-summary.tests.ps1:101,:108` are near-unfalsifiable** — `-match` substring
  passes even against a mangled path. *Trigger: the next card touching that suite.
  Owner: `backend-dev`.*
- 2026-09-04 **The DEFAULT rate-limit policy has no 429 test.** *Owner: `backend-dev`, no trigger.*
- 2026-08-27 **Validation is a mediator pipeline behaviour, not section 6's endpoint filter.**
  *Trigger: ratify or revert. Owner: UNOWNED.* `ASSUMPTIONS.md` section 2.2.

- 2026-09-21 ~~**Signature and logo held by Cloudinary, a third-party processor (NDPA).**~~ **STRUCK 2026-09-22 by human**: NDPA certification is not sought; revisit if that changes.

### Live — blocked on the deployment decision (open question 5)

One decision clears all four.

- 2026-09-05 **Unpersisted Data Protection key ring** (TASK-0003). *Owner: `backend-dev` plus
  human.*
- 2026-09-05 **`SameSite=Lax` assumes frontend and API share a registrable domain.** Cross-site
  needs `SameSite=None` AND a reconsidered CSRF posture. *Owner: `backend-dev` plus human.*
- 2026-08-26 **Staging and dev-test databases share one role and password.** *Owner: human.*
- 2026-09-05 **Cookie domain undecided.** *Owner: human.*

### Live — build and tooling

- 2026-09-18 **A UnitTests namespace segment named `Results` shadows `Microsoft.AspNetCore.Http.Results`** (and
  `Pupils` under `.Application` shadows a static field), breaking unrelated tests with CS0234. *Trigger: a new UnitTests
  folder/namespace named `Results` or `Pupils` at those levels. Owner: `backend-dev`.* → `drift/2026-Q3.md`
- 2026-09-05 **`Microsoft.Testing.Platform.MSBuild` is an unpinned transitive floor, and
  `backend/` has NO NuGet lock file.** Pinning it broke restore. *Trigger: the next `xunit.v3`
  upgrade. Owner: `backend-dev`.*

### Accepted and recorded, no action

- 2026-09-09 **PROCESS INCIDENT, repaired: `contract-guardian` reverted a dev agent's uncommitted
  work** on TASK-0052 closure, then reported the damage its own commands had caused as a check
  failure. Fully repaired. The guardian's prompt now forbids every tree-moving command. *Closed.*
- 2026-09-09 **TASK-0050 landed 2,452 hand-written lines against a ~1,000-line threshold** — the
  ESTIMATE was the orchestrator's error, not padding. *Closed; informs future card sizing.*
- 2026-09-05 **Two accepted auth exposures (TASK-0003):** the bootstrap CLI prints the temp
  password to stdout, and `PersistLockoutStateAsync` has a timing asymmetry. (The third, the DP key
  ring, is under the deployment decision above.) *Owner: `backend-dev`.*
- 2026-09-08 **THE FLAG BYPASS IS GONE.** `SuperAdminFlagEffectivePrivilegeProvider` was deleted by
  TASK-0030 and replaced with `RoleAssignmentEffectivePrivilegeProvider`. *Closed.*

## Open questions

Live only. Resolved questions 1–4 and 6–14 are in `decisions/2026-Q3.md`.

5. **RESOLVED 2026-09-22 (human): app and PostgreSQL on one Namecheap Pulsar VPS; files on Cloudinary.** The four deployment
   drift items below are now decidable at deployment: DP key ring persisted on the VPS disk; `SameSite=Lax` holds if frontend and API
   share the domain; cookie domain = the school's domain; separate DB roles per environment.

## How to read and append to this file

**No size cap** (removed 2026-09-06). Never trim, defer or skip an append to hit a byte target.

**But `## Decisions` and `## Known drift` are INDEXES, and an index entry is ONE line** (two
physical lines if it wraps). That is the format, not a budget. Added 2026-09-14, after the
decisions section reached 187 KB that every agent loaded on every dispatch to learn state that fit
in 8 KB.

**Appending a decision or a drift entry, at card close:**

1. Write the full account into `.agent/decisions/2026-Q3.md` (or `drift/2026-Q3.md`) under a dated
   heading naming the TASK id.
2. Leave ONE line here: date, bolded claim, and — for drift — its *trigger* and *owner*.
3. Never delete. A superseded entry moves to the archive with its resolution recorded.

Rules: `CLAUDE.md` section 3, `.agent/rules/governance.md` section 1.
