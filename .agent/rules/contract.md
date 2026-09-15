# The contract is law

**This file is CLAUDE.md §3 and §4.3–§4.4.** Binding in full.

Readers: `orchestrator` (always), `contract-guardian` (always), a dev agent ONLY when its card's
`Contract impact` is not `none`. A dev agent implementing against an unchanged contract does not
need this file — it needs its contract slice and its spec.

---

## 1. Source of truth

`contracts/openapi.json` is the single source of truth for every byte crossing the HTTP boundary.

**Generation:** the backend emits it at build time via
`backend/scripts/generate-openapi.ps1 -Promote` (Microsoft.Extensions.ApiDescription.Server). That
script is the ONLY sanctioned way `contracts/openapi.json` and `CONTRACT.lock` change. Never
hand-written.

**Consumption:** `frontend/src/api/schema.d.ts` is generated from the committed document by
`npm run generate:api` (openapi-typescript, types only, pinned exact). Generated files carry a
`// GENERATED — DO NOT EDIT` header. Any hand edit is a blocker in review.

**Never load the whole document.** `jq` the paths your card names:

```
jq '.paths."/api/v1/audit-events"' contracts/openapi.json
jq '.components.schemas.AuditEventDto' contracts/openapi.json
jq '.paths | keys | length' contracts/openapi.json
```

## 2. Change procedure — no exceptions

1. Contract change is written up in the task card: endpoint, method, request shape, response
   shape, status codes, error codes, breaking-vs-additive.
2. The orchestrator approves it, or escalates to the human if it is **breaking**.
3. `backend-dev` implements and regenerates `openapi.json`; `CONTRACT.lock` is updated.
4. `frontend-dev` regenerates the client from the *committed* document and adapts callers.
5. `contract-guardian` verifies the lockfile, the diff, and that no generated file was touched.

**Breaking** (requires human sign-off): removing or renaming a field or endpoint, narrowing a
type, making an optional field required, **adding a NEW required request field** (added 2026-09-15
after TASK-0062 shipped one under an `additive` header — a request that worked yesterday is
rejected today, which is the same thing from the caller's side), changing a status code's meaning, or changing enum member
values. **Additive** (no sign-off): new optional field, new endpoint, new enum member *if the
client handles unknown members*.

**Versioning:** URL-segment, `/api/v{n}/...`. Breaking changes ship a new version; the previous
version stays live for one release cycle minimum.

## 3. Handoff sequence — backend-first (§4.3)

```
contract delta approved
   → backend-dev implements + regenerates openapi.json + updates CONTRACT.lock
   → orchestrator reviews backend diff against contract + .agent/spec/backend.md
   → frontend-dev regenerates client + implements UI against it
   → orchestrator reviews frontend diff against .agent/spec/frontend.md
   → contract-guardian runs the §4.4 drift check
   → task card closed, STATE.md updated
```

Never dispatch both agents against the same contract delta in parallel. Parallel dispatch is
permitted **only** when the two tasks touch disjoint endpoints and neither changes the contract —
state this explicitly in the dispatch when you do it.

## 4. Drift detection (§4.4)

Run before closing any task, and in CI:

1. Rebuild the backend, regenerate the OpenAPI document to a temp path, diff against
   `contracts/openapi.json`. Any difference → the committed contract is stale. **Blocker.**
2. Regenerate the frontend client to a temp path, diff against `src/api/`. Any difference → the
   client is stale or hand-edited. **Blocker.** (`npm run check:api-drift` is this check.)
3. Grep the frontend for raw `fetch(` / `axios` calls to the API host outside `src/api/`. Any hit
   → bypassing the generated client. **Blocker.** (`src/test/http-boundary.test.ts` is this check.)
4. Confirm `CONTRACT.lock` matches the current document hash.

**The baseline is the WORKING TREE, not `git show HEAD:`.** Added 2026-09-09 after the same
failure mode struck twice in one day. Uncommitted-but-correct is the EXPECTED state, because the
orchestrator commits only after review. Comparing HEAD's `CONTRACT.lock` against the working
tree's `openapi.json` produces a contradiction that is an artefact of the comparison, not a
defect. Before reporting any contract-history contradiction, check whether one side came from
`git show HEAD:`.

## 5. Conflict resolution (§4.5)

| Situation | Resolution |
|---|---|
| Frontend needs a shape the backend didn't expose | Backend wins the *how*, frontend wins the *what*. Open a contract delta; never shim it in the client. |
| Two agents disagree on naming | Contract naming follows the backend's domain vocabulary. Frontend adapts at the generated-client edge, not in domain code. |
| A subagent claims the spec is wrong | It may be. Stop, write it into `## Open questions`, escalate to the human. No agent may unilaterally amend a spec file. |
| An agent reports "done" but the gate fails | Reopen the card with the failing output pasted in. Do not fix it yourself. |
| Merge conflict in `openapi.json` | Never hand-merge. Regenerate from backend and re-derive the client. |
