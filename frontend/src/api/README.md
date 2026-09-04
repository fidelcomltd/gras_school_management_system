# `src/api/` — the OpenAPI client layer

Two files, two different rules.

| File | Origin | Rule |
|---|---|---|
| [`schema.d.ts`](./schema.d.ts) | **Generated.** `openapi-typescript` against the committed `contracts/openapi.json`. | Never hand-edit. Carries a `// GENERATED — DO NOT EDIT` header; §4.4's drift check enforces this. |
| [`client.ts`](./client.ts) | **Hand-written.** | Reviewed like any other source file. |

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

`GetPath`/`PostPath` cover the methods in use today. Extend `client.ts` with `apiPut` /
`apiPatch` / `apiDelete` the same way when a feature needs them — same shape, same pattern.

## Feature code

`src/features/<tag>/api.ts` hooks call `apiGet`/`apiPost` and nothing else — never the plain
verb helpers in `@/lib/http` directly, never `axios` or `fetch`. A feature hook that skips
`client.ts` skips schema inference too, which is exactly the "hand-typed interface that can
drift from the contract" §3 rules out.

If a feature needs a method `client.ts` doesn't cover yet (`PUT`/`PATCH`/`DELETE` — only `GET`
and `POST` are wired today), add `apiPut`/`apiPatch`/`apiDelete` to `client.ts` first, following
the exact shape of `apiGet`/`apiPost` above, then call that from the feature. There is no
sanctioned path from feature code straight to `@/lib/http`'s verb helpers — the one seam that
would bypass schema inference is closed. See [`src/features/README.md`](../features/README.md).

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
