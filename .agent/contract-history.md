# Contract hash history

Moved out of `.agent/STATE.md` on 2026-09-14 (context-budget pass). It was 16 KB of superseded
hashes that every agent loaded on every dispatch to learn one current value.

**The CURRENT hash, path count and schema count live in `STATE.md` `## Contract`. That is the
only contract fact a dispatch normally needs.** Open this file only when you are reconciling
hash history, investigating how a shape got its present form, or auditing a promote that looks
out of sequence.

The "api version / N paths" enumerations below are SNAPSHOTS AT THEIR OWN DATE and go stale by
design — never read a path count out of this file. `jq '.paths | keys | length' contracts/openapi.json`
is the live answer.

---

**2026-09-09, TASK-0049 REOPENED point closed by backend-dev — orchestrator still needs to reconcile
the hash history below.** `GET /api/v1/audit-events/export`'s 200 response was documented as an empty
`{"description": "OK"}` — no `content`, no `text/csv`, no schema — while every other response on the
same operation (401/403/422/429) and `ListAuditEvents`' own 200 both carried `content`. Root cause:
`.Produces(StatusCodes.Status200OK, contentType: "text/csv")` in
`backend/src/SchoolManagement.Api/Endpoints/AuditEventEndpoints.cs` supplies no response `Type`, and
the generator silently drops `content` for an untyped response even when a `contentType` is given.
Fix: switched to the generic `.Produces<string>(StatusCodes.Status200OK, contentType: "text/csv")`
overload — one line, no operation transformer needed (the existing `CsrfHeaderOperationTransformer`
/ `IdempotencyHeaderOperationTransformer` pattern was considered but is unnecessary here: the generic
overload alone supplies enough type information for the built-in generator to describe the body).
Regenerated + promoted: new hash **`a1bd936b1e5bff709b8891c5c55e1918805e48ce3ef1e8a8afedd14bd87ee9f0`**,
still **47 paths**. Diff against the prior committed document (`jq -S` on both, line diff) touches
**only** that one operation's 200 response — every other path/operation byte-identical; independently
confirmed additive (a `content` block appearing where none existed takes nothing away). New regression
test: `OpenApiContractTests.ExportAuditEvents_200Response_DeclaresTextCsvContent` (asserts on the
generated document directly, following this file's existing precedent — no new assertion style
introduced). `dotnet build -warnaserror` clean, `dotnet format --verify-no-changes` clean (exit 0),
targeted `dotnet test` on `SchoolManagement.ArchitectureTests` filtered to `OpenApiContractTests`:
13/13 passed, 0 skipped. Did NOT run the full `ci.ps1` gate (reserved for the orchestrator per the
rule below) and did NOT run the Postgres-backed integration suite (out of this fix's scope — no
runtime code touched, and the export's CSV behavior was already reviewed/accepted). **Not committed.**
**Discrepancy raised by backend-dev, INVESTIGATED AND DISMISSED by the orchestrator 2026-09-09 —
the ledger below is correct and no history was swapped or unlogged.** The agent reported that the
contract it started from "already carried hash `53820aa5…` AND already contained `/audit-events` at
47 paths", which would indeed be contradictory. Verified directly, it is not what was on disk:

- `git show HEAD:contracts/openapi.json` → `53820aa5…`, **45 paths, ZERO `audit-events` paths**.
  Exactly what the history below says.
- the WORKING TREE at that moment → `f9b73118…`, 47 paths, with `audit-events` — because the
  agent's OWN first dispatch had already promoted it, uncommitted.

**Root cause: it compared a committed value against an uncommitted one** — HEAD's `CONTRACT.lock`
(or HEAD's document) against the working tree's `openapi.json`, which of course disagree while a
promote is uncommitted. Nothing needed reconciling. Raising it rather than silently rewriting the
ledger was still the right instinct, and is why this correction is cheap to write.

⚠ **Second instance in one day of the same failure mode** — the contract-guardian incident recorded
in `## Known drift` was also a HEAD-vs-working-tree comparison. **Standing rule for every agent:
at closure time the correct baseline is the WORKING TREE.** Uncommitted-but-correct is the expected
state, because the orchestrator commits only after review. Before reporting any contract-history
contradiction, check whether one side of the comparison came from `git show HEAD:`.

openapi.json sha256: **`7a3c84e6a1872d014e519c8fa15227ba040b8b31ade41e20d9e98d32be509325`** — moved
          2026-09-09 by TASK-0055 (implemented, not yet closed). Additive: one new `entityId`
          (string, optional) query parameter on EACH of `ListAuditEvents` and `ExportAuditEvents`,
          plus two `description` text edits naming it. Still **47 paths** and still **86 schemas** —
          both key sets independently diffed byte-for-byte identical against the prior committed
          document; no path or schema added, removed or reshaped. Diff is +18/-3 lines. Hash
          independently recomputed with `sha256sum`, matches `CONTRACT.lock`. Frontend client
          regeneration explicitly out of this card's scope — TASK-0054 runs next, against this hash.

previous: **`a1bd936b1e5bff709b8891c5c55e1918805e48ce3ef1e8a8afedd14bd87ee9f0`** — moved
          2026-09-09 by TASK-0049's REOPEN dispatch, superseding `f9b73118…` below within the same
          card. Sole delta: `GET /api/v1/audit-events/export`'s 200 response gained
          `content."text/csv".schema.type = "string"`, which it should have carried from the start.
          Still **47 paths** — no path or schema added or removed, so the reopen widened nothing.
          Purely additive: the document previously said *nothing* about that response body and no
          generated client consumed it. Root cause was the non-generic
          `.Produces(200, contentType: "text/csv")` overload, which supplies no response `Type` and
          so emits no `content` even when given a media type; `.Produces<string>(...)` fixes it with
          no operation transformer and no shared file touched. Guarded against regression by
          `OpenApiContractTests.ExportAuditEvents_200Response_DeclaresTextCsvContent`, which asserts
          on the generated document. Recomputed with `sha256sum`, matches `CONTRACT.lock`.
          ✅ **Frontend client REGENERATED against this hash by TASK-0054 (2026-09-10) — §4.4
          check 2 GREEN.** `check:api-drift` "No drift.", verified independently by the
          orchestrator. Both audit operations reachable. **First run of that recurring card to add
          a client-seam line in eight** — a proven `never` on the `text/csv` response, not an
          assumed gap; see the TASK-0054 entry in `## Decisions`.

superseded within TASK-0049: **`f9b73118c6f6b14dd872374754f2113d3ecb7712c7ca9c3856829cd1129ca795`** — moved
          2026-09-09 by TASK-0049. Additive: `/audit-events`, `/audit-events/export` (**47 paths**,
          was 45), plus three new schemas (`AuditEventDto`, `CursorPageOfAuditEventDto`,
          `AuditOutcome`). +427/-0 per `git diff --stat` — the new paths and schemas appended
          without reshuffling any existing line (unlike TASK-0050's move, no alphabetical
          resort landed in the middle of the document this time). Independently verified purely
          additive: `components.schemas` keys diffed directly (3 added, 0 removed), `paths` keys
          diffed directly (2 added, 0 removed). Recomputed independently with `sha256sum`, matches
          `CONTRACT.lock`.
          Frontend client regeneration NOT done — out of this card's scope per its own text
          ("Any frontend work... is a separate later card"); §4.4 check 2 will show drift until
          that follow-up card runs `npm run generate:api`. Owner: a TASK-0047-shaped card, not yet
          created.

previous: **`53820aa5feb23ef8d34b4962b250a74ef202faa3cbc3f066873a6a2c38f0da9b`** — moved
          2026-09-09 by TASK-0050. Additive: `/admissions`, `/pupils`, `/pupils/{id}`,
          `/pupils/duplicates` (**45 paths**, was 41), plus six new schemas (`PupilDto`,
          `CursorPageOfPupilDto`, `CreatePupilCommand`, `UpdatePupilBiographicalCommand`,
          `PupilSex`, `PupilStatus`). Line diff is large (+1389/-227 per `git diff --stat`) because
          the new paths sort alphabetically between existing ones, reshuffling surrounding JSON —
          independently verified purely additive by diffing the SORTED line sets (every removed
          line has an exact matching added line elsewhere: 0 unmatched) and by diffing
          `components.schemas` keys directly (6 added, 0 removed). Recomputed independently with
          `sha256sum`, matches `CONTRACT.lock`.
          ✅ **Frontend client REGENERATED against this same hash by TASK-0052 (2026-09-09) —
          §4.4 check 2 GREEN. `frontend/src/api/schema.d.ts` re-run through `npm run generate:api`
          (+1394/-93), `check:api-drift` → "No drift.", verified three times by the orchestrator.
          All nine new operations (three reg-number settings, six pupils) reachable through the
          existing generic wrapper with ZERO new lines in `client.ts`/`client-types.ts` — eighth
          consecutive confirmation of that finding. §4.4 check 1 verified PASS by contract-guardian
          (backend regeneration matches the committed contract); checks 3 and 4 PASS.**
          ORCHESTRATOR'S CALL 2026-09-09: ONE card, not two — TASK-0052 was widened to consume
          both the 0005c and 0050 moves in a single regeneration. `generate:api` rewrites the whole
          of `schema.d.ts` from the committed document regardless, so two sequential cards would
          regenerate the same file twice and the first would be dead work. Vindicated: the single
          regeneration produced all nine operations at once.

previous: **`e86e1b187bbacd9f83b8bd725cb8c9066bf41c5fd936b606da331646d2080e90`** — moved
          2026-09-09 by TASK-0005c. Additive: `/settings/reg-number`,
          `/settings/reg-number/preview`, `/settings/abbreviation` (**41 paths**, was 38), plus two
          new groups on `SettingsDto`. +623/-3; the 3 deletions are a `SettingsDto` doc-comment
          rewording and its `required` list gaining two entries — nothing removed or narrowed.
          Recomputed independently with `sha256sum`, matches `CONTRACT.lock`.
          ✅ **Superseded — the client was regenerated against the LATER `53820aa5…` hash by
          TASK-0052, which consumed this move and TASK-0050's together. §4.4 check 2 GREEN.**

previous:  **`c3cb88ff74b931ba58057a11b781fbad58d72d1778ff91584dbfe712f661a4c9`** — moved
          2026-09-08 by TASK-0030. Additive: three assignment endpoints. Recomputed independently,
          matches `CONTRACT.lock`. **Frontend regenerated against this same hash by TASK-0047
          (2026-09-09): `frontend/src/api/schema.d.ts` re-run through `npm run generate:api`,
          `check:api-drift` green. All five §4.4 checks verified GREEN by contract-guardian on
          2026-09-09 — backend→contract byte-identical (38 paths, 342413 bytes), contract→client
          no drift, no client bypass, lockfile matched, generated header intact.** See TASK-0047 in
          `## Decisions` below for the full account.

previous:  **`84a3444a5172a524a64860e7b296ee6c15fc6f8d35d62cd7c873c803dd61b420`** — moved
          2026-09-08 by TASK-0039 (arms). Additive: the `/arms*` paths plus `ArmCount` on
          `SessionDto`/`SessionDetailDto` and a new 409 case on `POST /terms/{id}/open`. Hash
          independently recomputed with `sha256sum`, matches `CONTRACT.lock`. **Frontend
          regenerated against this same hash by TASK-0044 (2026-09-08): `frontend/src/api/
          schema.d.ts` re-run through `npm run generate:api`, `check:api-drift` green.** See
          TASK-0044 in `## Decisions` below for the full account.

previous:  `82870944982d77d2e540eb2ad455444151d670f439b6e6f9cc7fc54d41ba4168` — moved
          2026-09-08 by TASK-0038 (was `a618db62…`). Purely additive: five new paths
          (`/sections`, `/sections/{id}`, `/levels`, `/levels/{id}`, `/levels/reorder`) carrying
          nine operations, plus the `LevelDto`/`SectionDto`/command/`CursorPageOfLevelDto` schemas.
          No existing path or schema changed shape. **Hash independently recomputed with
          `sha256sum` against the committed file — matches `CONTRACT.lock` byte for byte.**
          `X-CSRF-Token` required on all six mutations; `Idempotency-Key` REQUIRED on
          `POST /levels` and `POST /sections`, ACCEPTED on the `PATCH`es, `DELETE` and
          `POST /levels/reorder` — all declared by construction through the existing operation
          transformers, never hand-annotated. **32 paths now.** ⚠ **Frontend client NOT yet
          regenerated against this hash — §4.4 check 2 is RED until a TASK-0037-shaped card runs.**
          History below.

previous:  9dca7f11ed2c4ed00cd444f2fe3d99af8334f7f48cc0a1b74ebc64d9d761a573
          (was 73316bdb… until TASK-0034, 2026-09-07: the document is now newline-normalised so a
          Windows promote and a Linux CI run of the same commit produce identical bytes.)
          (was `618f730d…` before TASK-0028 dispatch 2; this dispatch moved it by adding
          `GET|POST /api/v1/roles` and `GET|PATCH|DELETE /api/v1/roles/{id}`, purely additive —
          new schemas `RoleDto`, `CreateRoleCommand`, `UpdateRoleCommand`, `CursorPage<RoleDto>`
          (rendered `CursorPageOfRoleDto`), no existing path or schema changed shape.
          **Independently recomputed with `sha256sum` against the committed file — matches
          `CONTRACT.lock` byte for byte.** `900` lines added to `contracts/openapi.json`, 1 line
          changed in `CONTRACT.lock` (the hash itself) — verified via `git diff --stat`.
          `X-CSRF-Token` required on all four `/roles*` mutations; `Idempotency-Key` REQUIRED on
          `POST /roles`, ACCEPTED on `PATCH`/`DELETE` — both declared by construction via the
          existing operation transformers, never hand-annotated.
          `lockedUntil` remains DECLARED on the `ProblemDetails` schema since TASK-0027 dispatch 2.
regenerated: 2026-09-07 by TASK-0028 dispatch 2, `-Promote` (generator + SDK under `## Layout`).
          **21 paths now** — `/roles` and `/roles/{id}` join the nineteen privileges/admins/
          settings/config-version/auth/reference ones. ~~**Frontend client NOT yet regenerated
          against this hash**~~ — **done by TASK-0033 (2026-09-07): `frontend/src/api/schema.d.ts`
          regenerated against this same `73316bdb…` hash, `check:api-drift` green.** See TASK-0033
          in `## Decisions` below for the full account.
          **Hash moved again, 2026-09-07, TASK-0035: `73316bdb…` →
          `a618db6208e45fd846648537baf9d1eb10d256587530ad182c4d490f1eb8c2a6` (223737 bytes, was
          160697) — six new paths (`/sessions`, `/sessions/{id}`, `/terms/{id}`,
          `/terms/{id}/{open,close,reopen}`), eleven new schemas, purely additive.** Frontend
          regenerated against this same hash by TASK-0037
          (2026-09-07): `frontend/src/api/schema.d.ts` re-run through `npm run generate:api`,
          `check:api-drift` green. See TASK-0037 in `## Decisions` below for the full account.
api version: v1 · 27 paths: `/sessions`, `/sessions/{id}` + `/terms/{id}`,
          `/terms/{id}/{open,close,reopen}` + `/roles`, `/roles/{id}` + `/privileges` +
          `/admins`, `/admins/{id}`, `/admins/{id}/status`,
          `/admins/{id}/password-reset`, `/admins/{id}/sessions` +
          `/auth/{csrf,sign-in,sign-out,me,refresh,password}` +
          `/settings`, `/settings/identity`, `/config-versions`, `/config-versions/{id}` +
          `/reference/{ping,records,arms/{armId}/secure}`. `/health/*` excluded
          (`ASSUMPTIONS.md` §2.9); `/reference/*` is scaffolding. `GET /privileges` is
          authenticated-only (`.RequireAuthenticatedCaller()`), no privilege required (spec
          6.1.14), not paged — a fixed 93-row compile-time register. `GET|POST /roles` and
          `GET|PATCH|DELETE /roles/{id}` are each gated by one fixed `role.{view,create,update,
          delete}` privilege declaratively (`.RequirePrivilege(...)`) — none of the five is
          data-dependent the way two `/admins*` routes are. `POST /sessions` requires
          `Idempotency-Key`; `PATCH /sessions/{id}`, `PATCH /terms/{id}` and the three term
          transitions accept it optionally; `GET /sessions` and `GET /sessions/{id}` are reads
          (neither CSRF nor Idempotency-Key). History: `decisions/2026-Q3.md`.

