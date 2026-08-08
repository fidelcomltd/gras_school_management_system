# Golden Royal Ark School Portal — frontend

React web client for the school management system. *Sailing into greatness.*

## Getting started

```bash
npm install
cp .env.example .env    # then edit VITE_API_BASE_URL to point at your backend
npm run dev
```

Config is validated at boot — a missing or malformed variable fails immediately with a
message naming the offender, rather than surfacing as a confusing error later.

## Scripts

| Command | What it does |
|---|---|
| `npm run dev` | Vite dev server |
| `npm run build` | typecheck, then production build to `dist/` |
| `npm run preview` | serve the production build |
| `npm run typecheck` | `tsc -b` |
| `npm run lint` | oxlint |
| `npm run test` | Vitest, once |
| `npm run test:watch` | Vitest, watching |
| `npm run test:coverage` | Vitest with a V8 coverage report |
| `npm run verify` | typecheck → lint → test → build |

## Where things are

```
src/
  api/        RESERVED for the generated OpenAPI client — never hand-edited
  app/        composition root: providers, router
  components/ shared components; ui/ is the design system
  config/     env-values.ts — the only reader of import.meta.env
  features/   server-state modules, one per OpenAPI tag
  lib/        auth/, http/, query/, utils/
  screens/    one folder per screen
  stores/     Zustand client state
  styles/     colour tokens and base styles
  test/       setup, render helpers, MSW
```

## Before you write code

Read **[CONVENTIONS.md](CONVENTIONS.md)**. It covers naming, the HTTP layer, the token-based
colour system, auth, accessibility requirements, and the quality gates — all decided, all
enforced in review.

Two entry points worth knowing up front:

- Server calls go through the verb helpers in `src/lib/http/`, wrapped in a TanStack hook in
  `src/features/<tag>/api.ts`. Never call `axios` or `fetch` directly.
- Colours come from semantic tokens (`bg-surface`, `text-muted-foreground`). Never a raw hex,
  and never a `dark:` class — the token layer handles both themes.

## Status

Scaffold only. No feature code has been written; `src/api/` and `src/features/` are
intentionally empty pending `contracts/openapi.json`. See [HANDOFF.md](HANDOFF.md).
