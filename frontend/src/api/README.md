# `src/api/` — reserved for the generated contract client

**Empty on purpose. Do not hand-write anything here.**

The root [`CLAUDE.md`](../../../CLAUDE.md) §3 makes `contracts/openapi.json` the single
source of truth for the HTTP boundary, and §7 reserves this directory for code generated
from it. Every file that lands here will carry a `// GENERATED — DO NOT EDIT` header, and
§4.4 runs a drift check that regenerates into a temp path and diffs against this folder.
A hand edit fails that check.

## Why it is still empty

`contracts/openapi.json` does not exist yet — the backend has not been built, so there is
nothing to generate from. See [`contracts/README.md`](../../../contracts/README.md).

## What goes where in the meantime

| Concern | Location |
|---|---|
| Generated request/response **types** | here, once the contract exists |
| Axios instance, verb helpers, error normalisation | [`src/lib/http/`](../lib/http/) — hand-written, reviewed |
| Per-endpoint TanStack Query hooks | `src/features/<tag>/api.ts` |

This split is deliberate and was agreed with the orchestrator: the generated layer supplies
*types*, the hand-written layer supplies *transport*. Feature hooks compose the two. That
satisfies both "no raw axios outside the http layer" and "nothing in `src/api/` is
hand-edited".

## When the contract lands

1. Add the generator (`openapi-typescript` is the orchestrator's recommendation) as a
   devDependency and wire an `npm run generate:api` script.
2. Generate into this directory from the **committed** document, never from a live server.
3. Replace hand-written interfaces in `src/features/*/types.ts` with imports from here.
4. Derive the MSW handlers in `src/test/msw/handlers.ts` from the same document.
