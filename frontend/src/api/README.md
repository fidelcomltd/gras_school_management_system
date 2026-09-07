# `src/api/` — the OpenAPI client layer

Two files, two different rules.

| File | Origin | Rule |
|---|---|---|
| [`schema.d.ts`](./schema.d.ts) | **Generated.** `openapi-typescript` against the committed `contracts/openapi.json`. | Never hand-edit. Carries a `// GENERATED — DO NOT EDIT` header; §4.4's drift check enforces this. |
| [`client.ts`](./client.ts) | **Hand-written.** | Reviewed like any other source file. |
| [`client-types.ts`](./client-types.ts) | **Hand-written.** | The type-level derivation behind `client.ts`'s verb helpers, split into its own file to stay under CONVENTIONS.md §3's 180-line cap. |
| [`client.test.ts`](./client.test.ts) / [`client-roles.test.ts`](./client-roles.test.ts) / [`client-sessions.test.ts`](./client-sessions.test.ts) / [`client-terms.test.ts`](./client-terms.test.ts) | **Hand-written.** | Colocated tests for `client.ts`'s generic verb helpers, split across four files (reference/admins, then privileges/roles — TASK-0033, then sessions and terms — TASK-0037) to stay under the same 180-line cap. |

This mirrors what `frontend/HANDOFF.md` decision 3 originally set out: the generated layer
supplies *types*, the hand-written layer supplies *transport*. `client.ts` is the thin seam
between them — it adds no HTTP behaviour of its own.

## Pipeline

```
contracts/openapi.json  (committed, backend-owned, never hand-edited)
        │  npm run generate:api
        ▼
src/api/schema.d.ts     (generated `paths` / `components` / `operations` types)
        │  imported by
        ▼
src/api/client.ts       (apiGet / apiPost — typed wrappers over @/lib/http)
        │  called by
        ▼
src/features/<tag>/api.ts   (TanStack Query hooks — none yet, see src/features/README.md)
```

### Regenerating

```sh
npm run generate:api     # writes src/api/schema.d.ts from ../contracts/openapi.json
npm run check:api-drift  # regenerates to a temp path and diffs; non-zero exit on drift
```

Both scripts share `scripts/openapi-schema.mjs` so they can never disagree about what
"current" means. `check:api-drift` is CLAUDE.md §4.4 check 2 and must run in CI (see below) —
it is what turns a stale or hand-edited `schema.d.ts` into a build failure instead of a
silent guess.

Run `generate:api` any time `contracts/openapi.json` changes, then commit the result. Do
this from the **committed** document only — never point the generator at a live server
(CLAUDE.md §3).

**A new operation on an existing method (GET/POST/PATCH/DELETE) needs no new code in
`client.ts`.** `apiGet`/`apiPost`/`apiPatch`/`apiDelete` are generic over every path
`schema.d.ts` declares for that method, so regenerating the schema is what makes the new
path callable — TASK-0033 (`/privileges`, `/roles`, `/roles/{id}`) and TASK-0037
(`/sessions`, `/sessions/{id}`, `/terms/{id}`, `/terms/{id}/open`, `/terms/{id}/close`,
`/terms/{id}/reopen`) each added zero lines to `client.ts`/`client-types.ts`, only
regenerated `schema.d.ts` and added the new operations' tests (`client-roles.test.ts`,
`client-sessions.test.ts`, `client-terms.test.ts`). A new *method* the contract has never
used before (`PUT` still has no operation anywhere) is the one case that needs a new verb
helper.

## `client.ts` — why a typed helper and not a second HTTP stack

`openapi-fetch`, NSwag and Kiota were all rejected (`STATE.md` Decisions, 2026-08-26)
because the frontend already owns a working axios transport with auth interceptors and a
single-flight refresh seam in [`src/lib/http/`](../lib/http/). Running a second runtime HTTP
client alongside it would duplicate that seam and give a feature two different ways to make
a request.

`apiGet`/`apiPost` in `client.ts` call straight through to `getRequest`/`postRequest` from
`@/lib/http` — same axios instance, same interceptors, same `ApiError` normalisation. All
they add is generic type parameters, keyed by path, that resolve against `schema.d.ts`:

```ts
import { apiGet } from '@/api/client';

// `result` is inferred as PingResponse — never asserted with `as` or hand-typed.
const result = await apiGet('/api/v1/reference/ping', { name: 'Ada' });
```

Passing a path outside `contracts/openapi.json`, or a query/body shape the contract doesn't
declare for that path, is a compile error. This is the mechanism that makes "the frontend
cannot call the backend without guessing across the boundary" (CLAUDE.md §3) enforced by the
type checker rather than by convention.

`apiGet`, `apiPost`, `apiPatch` and `apiDelete` (TASK-0029) cover every method the contract
declares (`PUT` has no operation anywhere yet, so `apiPut` doesn't exist until one does — add
it the same way when a feature needs it).

**Path parameters** (`/admins/{id}`) are threaded through a `pathParams` field on the trailing
options argument, type-driven from `operations[...]["parameters"]["path"]` — never a string
the caller formats by hand — and that whole options argument becomes *required* (not just the
field) exactly when the operation declares a path parameter, so omitting one is a compile
error, not a runtime 404:

```ts
import { apiGet } from '@/api/client';

const account = await apiGet('/api/v1/admins/{id}', undefined, { pathParams: { id } });
```

**`Idempotency-Key`** is caller-owned data, threaded the same way as its own `idempotencyKey`
field — required when the contract requires it (`POST /admins`), optional when the contract
allows it (`PATCH /admins/{id}`, `POST /admins/{id}/status`, `POST /admins/{id}/password-reset`,
`PATCH /settings/identity`), and simply absent from the options type everywhere else.
**`X-CSRF-Token` is never a field here** — despite the schema declaring it a required header on
every mutating operation, it is transport-owned: `http-client.ts`'s request interceptor injects
it on every mutating request, and `client.ts` has no way for a caller to set or override it.
The type-level derivation behind all of this (`SuccessBody`, `QueryOf`, `OptionsArgs`, …) lives
in [`client-types.ts`](./client-types.ts), split out to stay under CONVENTIONS.md §3's
180-line file cap.

## Feature code

`src/features/<tag>/api.ts` hooks call `apiGet`/`apiPost`/`apiPatch`/`apiDelete` and nothing
else — never the plain verb helpers in `@/lib/http` directly, never `axios` or `fetch`. A
feature hook that skips `client.ts` skips schema inference too, which is exactly the
"hand-typed interface that can drift from the contract" §3 rules out. There is no sanctioned
path from feature code straight to `@/lib/http`'s verb helpers — the one seam that would bypass
schema inference is closed. See [`src/features/README.md`](../features/README.md).

## Tests: MSW handlers derived from the contract

[`src/test/msw/openapi-handlers.ts`](../test/msw/openapi-handlers.ts) reads the same
`contracts/openapi.json` at test time and builds one default MSW handler per operation, each
answering its documented success status with the schema's own `example` payload.
[`src/test/msw/handlers.ts`](../test/msw/handlers.ts) exports the result as the default
handler set. A mock can no longer describe a response shape the contract doesn't.

Per-test overrides — error responses, pagination edge cases, anything not "the happy path" —
still go through `server.use(...)` with `problemResponse` from `handlers.ts`, exactly as
before. Only the *defaults* are contract-derived now.

## Boundary enforcement

[`src/test/http-boundary.test.ts`](../test/http-boundary.test.ts) fails if any raw `axios`
import or `fetch(` call appears outside `src/lib/http/` — CLAUDE.md §4.4 check 3. `client.ts`
itself is scanned like any other file; it stays clean because it only imports the verb
helpers, never axios.

## What CI should run (frontend, root-owned)

```sh
npm run typecheck
npm run lint
npm run test
npm run build
npm run check:api-drift
```

The first four are `npm run verify`. `check:api-drift` is listed separately because it is a
*contract* gate (CLAUDE.md §9's "Contract" row), not a frontend-quality gate — it fails when
`schema.d.ts` disagrees with the committed `contracts/openapi.json`, which can happen even
when every other frontend gate is green (e.g. the backend regenerated the contract and this
package wasn't updated to match).
