# Frontend conventions

Golden Royal Ark School Portal — React web client.

**Read this before writing code.** It records decisions already made so you do not have to
re-litigate them. Where it conflicts with a general habit, this file wins. Where it
conflicts with the root [`CLAUDE.md`](../CLAUDE.md), raise it rather than picking a side.

---

## 1. Stack

| Concern | Choice | Notes |
|---|---|---|
| Build | Vite 8 + React 19 | |
| Language | TypeScript 6, `strict` | plus the extra flags in §2 |
| UI primitives | **Base UI** (`@base-ui/react`) | headless. **No `@radix-ui/*`, ever** |
| Styling | Tailwind CSS 4 | CSS-first config, no `tailwind.config.js` |
| Component layer | shadcn-style, owned in-repo | `src/components/ui/` |
| Server state | TanStack Query 5 | |
| HTTP transport | Axios, wrapped | never called directly outside `src/lib/http/` |
| Client state | Zustand 5 | |
| Validation | Zod 4 | |
| Routing | React Router 7 | v8 requires Node ≥22.22; this machine has 22.21 |
| Tests | Vitest + RTL + MSW | |
| Lint | oxlint | config in `.oxlintrc.json` |

Package manager is **npm**. `pnpm` is not installed on this machine.

Adding a dependency needs a justification in the task card. No `latest` ranges.

**`openapi-typescript` and the TypeScript peer range.** `openapi-typescript@7.13.0` declares
`peerDependencies: { typescript: "^5.x" }`; this repo runs TypeScript 6.0.3 (see above), which
a plain `npm ci` rejects with `ERESOLVE`. The tool only reaches for `typescript` internally for
AST/printer helpers that did not change in a way that matters between the 5.x and 6.x majors —
confirmed by generating, typechecking and building against it. Rather than relaxing peer
checking repo-wide (an `.npmrc` `legacy-peer-deps=true` would also silently let a genuine
future conflict — e.g. a library that really does need React 18 — install broken instead of
erroring), `package.json` carries a scoped fix instead:

```json
"overrides": {
  "openapi-typescript": { "typescript": "$typescript" }
}
```

`$typescript` pins *only* what `openapi-typescript` resolves for its own `typescript` peer to
this repo's own `typescript` devDependency range, so it tracks a future TS upgrade
automatically. Every other package's peer dependencies are still checked normally. Revisit
(and drop this override) once `openapi-typescript` ships a TS 6-aware peer range upstream.

---

## 2. TypeScript

Beyond `strict`, these are on and are not to be relaxed:

- `noUncheckedIndexedAccess` — `arr[0]` is `T | undefined`. Handle it.
- `exactOptionalPropertyTypes` — an optional prop that may receive `undefined` must be
  declared `prop?: T | undefined`. Use the `WithClassName<T>` helper in
  [`src/components/ui/props.ts`](src/components/ui/props.ts) for component props.
- `noPropertyAccessFromIndexSignature`, `noImplicitOverride`, `noUnusedLocals`,
  `noUnusedParameters`.

Hard rules:

- No `any`. Lint-enforced (`typescript/no-explicit-any: error`).
- No `!` non-null assertion. Lint-enforced. Narrow properly or throw with a message.
- No `@ts-ignore` / `@ts-expect-error`.

---

## 3. Naming

**One rule: every file and folder is `kebab-case`.** Including components — `app-shell.tsx`,
not `AppShell.tsx`. Exports keep their idiomatic casing (`AppShell`, `useThemeStore`).

| Thing | Convention | Example |
|---|---|---|
| Files, folders | `kebab-case` | `theme-toggle.tsx` |
| Components | `PascalCase` | `ThemeToggle` |
| Hooks | `useCamelCase` | `useStudents` |
| Types / interfaces | `PascalCase` | `StudentListItem` |
| Contract DTOs | `I`-prefixed | `IStudentListItem` — matches the backend's vocabulary |
| Enums, constants | `PascalCase` / `SCREAMING_SNAKE` | `StudentsKeys`, `API_BASE_URL` |
| Tests | `<source>.test.ts(x)`, colocated | `button.tsx` → `button.test.tsx` |

**180 lines maximum** for any logic or component file. Split by concern before it grows —
a page is composed of small focused pieces, not one long file.

No barrel re-export chains deeper than one level. `src/lib/http/index.ts` is fine; a barrel
that re-exports another barrel is not.

---

## 4. Directory map

```
src/
  api/          RESERVED — generated OpenAPI client. Never hand-edit. See src/api/README.md
  app/          composition root: app.tsx, providers/, router/
  components/   shared components — used by more than one screen
    ui/         the design system primitives (Base UI + Tailwind + cva)
    layout/     app shell
    feedback/   error boundary
    theme/      theme toggle
  config/       env-values.ts — the only reader of import.meta.env
  features/     server-state modules, one folder per OpenAPI tag. See src/features/README.md
  lib/
    auth/       auth-session.ts — the only place a token lives
    http/       axios client, verb helpers, error normalisation
    query/      TanStack Query client defaults
    utils/      cn()
  screens/      one folder per screen; screen-local components in its own components/
  stores/       Zustand stores for cross-screen client state
  styles/       brand.css (ramps), semantic.css (tokens), index.css (entry)
  test/         setup, render helpers, MSW server
```

**Component placement (§8 of the brief):** used by more than one screen → `src/components/`.
Used by exactly one screen → that screen's own `components/` folder. Promote only when a
*second* consumer appears, and promote in its own commit — never pre-emptively.

---

## 5. Configuration and secrets

- [`src/config/env-values.ts`](src/config/env-values.ts) is the **only** file that reads
  `import.meta.env`. It validates with Zod at module load, so bad config fails at boot.
- Everything else imports named constants: `API_BASE_URL`, `APP_ENV`, `IS_PRODUCTION`, …
- Adding a variable means editing three places: `.env.example`, the `ImportMetaEnv`
  interface in `src/vite-env.d.ts`, and the Zod schema.
- `.env` is gitignored; `.env.example` is committed.
- **Everything in a `VITE_` variable ships to the browser.** No secrets. Authorization is
  enforced by the backend; the UI only hides, it never protects.

---

## 6. HTTP layer

Three layers, bottom-up. Do not skip one.

**a. `lib/http/http-client.ts`** — the single Axios instance. Base URL, timeout, bearer
injection, correlation id, and 401 → single-flight refresh → retry-once.

**b. `lib/http/request.ts`** — `getRequest`, `postRequest`, `putRequest`, `patchRequest`,
`deleteRequest`. Each returns **only the payload** (never the Axios envelope), throws an
`ApiError` whose `message` is safe to render, and ends the session on a terminal 401.
Generic order is `<TResponse, TBody>`:

```ts
postRequest<void, ICreateStudentRequest>(`${BASE}`, payload);
```

**c. `features/<tag>/api.ts`** — one TanStack hook per endpoint. See
[`src/features/README.md`](src/features/README.md) for the exact template.

**Zero** raw `fetch` or `axios` anywhere outside `src/lib/http/`. This is checked by the
orchestrator's drift check and is a review blocker.

### Errors

Everything crossing the boundary becomes an `ApiError` with a `kind`
(`network`/`timeout`/`unauthorized`/`validation`/…), the RFC 9457 `type` as `code`, and
`fieldErrors` for a 422. Render `error.message` directly — it is already human-readable.

---

## 7. Auth

**Decision (2026-08-03): bearer access token, held in memory only.**

- [`src/lib/auth/auth-session.ts`](src/lib/auth/auth-session.ts) is the only module that
  touches a token. Not `localStorage`, not a Zustand store, not a component.
- Expiry is **backend-reported** (`expiresAt`), never computed from a hardcoded duration.
  `VITE_AUTH_EXPIRY_LEEWAY_SECONDS` absorbs clock skew.
- A timer schedules the lapse. On lapse it tries the single-flight refresh handler once;
  with no handler registered, listeners fire and the app logs out.
- `useSessionStore` holds **status only** (`anonymous` / `authenticated` / `expired`).
- Logout must be **server-authoritative**: revoke server-side first, then `signOut()`.
  Clearing client state alone is a blocker.

Open: the root `CLAUDE.md` §5 recommends HttpOnly cookie sessions and marks auth as
requiring human sign-off. This scaffold implements bearer per the frontend brief, isolated
so a switch touches `auth-session.ts` and `http-client.ts` only. **Confirm with the
orchestrator before building sign-in.**

---

## 8. Design system

- Primitives live in [`src/components/ui/`](src/components/ui/), one file per primitive,
  owned in-repo. Copy-in-and-customise, not a black-box dependency.
- Built on Base UI parts, styled with Tailwind via `cva()` variants and merged with `cn()`.
- Composition uses Base UI's **`render` prop**, which is the `asChild` equivalent:
  ```tsx
  <Button render={<Link to="/students" />}>Students</Button>
  ```
- Keep the API conventional: `variant`, `size`, `className`.
- If you port styling from a shadcn component, rewrite the imports to Base UI and
  **verify the accessibility behaviour** — do not assume parity.

Present: `button`, `input`, `select`, `dialog`, `field`, `icons`. Add more in the same shape.

The crest asset is not in the repo. `AppShell` renders a placeholder mark; drop the real
logo into `public/` and swap it.

---

## 9. Colour and theming

Three files in [`src/styles/`](src/styles/):

- **`brand.css`** — raw OKLCH ramps derived from the crest (`royal`, `gold`, `crimson`),
  plus fonts and radii. Decorative use only.
- **`semantic.css`** — the semantic tokens, defined for light and dark, exposed to Tailwind
  through `@theme inline`.
- **`index.css`** — entry point, base layer, focus ring, reduced-motion.

**Components use semantic tokens only.** `bg-surface`, `text-muted-foreground`,
`border-border`, `bg-destructive`. Never a raw hex, and never a brand ramp step in a
component. A component written against semantic tokens is correct in both themes with
**zero `dark:` classes** — if you find yourself writing `dark:`, the token is missing.

Token families: `background`/`foreground`, `surface` (+`-raised`, `-sunken`), `muted`,
`primary` (+`-hover`, `-subtle`), `secondary`, `accent`, `destructive`, `success`,
`warning`, `info`, `border`(+`-strong`), `input`, `ring`, `overlay`.

Dark mode is the `.dark` class on `<html>`, driven by `useThemeStore`. An inline script in
`index.html` applies it before first paint to avoid a flash — keep its storage key in sync
with the store.

---

## 10. State

- **Server state is TanStack Query's.** Never copy it into `useState` or a Zustand store.
- **Client state** (filters, selection, wizard step, theme) is Zustand or local `useState`.
  Local by default; lift only when a second consumer appears.
- Query keys come from a per-feature enum in `types.ts`. No inline string keys.
- `staleTime` is set globally to 30s in
  [`src/lib/query/query-client.ts`](src/lib/query/query-client.ts). Override per hook when
  the data needs it.
- Mutations invalidate **precisely**. `queryClient.invalidateQueries()` with no key is a
  blocker.
- Queries retry only on network/timeout/5xx. Mutations do not retry — a blind retry can
  double-submit.

---

## 11. Accessibility

Not optional. `jsx-a11y` rules are lint errors.

- Semantic elements. No `<div onClick>` standing in for a button.
- Every input is labelled — wrap in `Field` + `FieldLabel` and it is wired for you.
- Keyboard reachable, with a visible focus ring (set globally in `index.css`; do not remove).
- `aria-live` for async status; `FieldError` already does this.
- Icon-only controls carry an `aria-label` naming the *action*.
- **Four required states for every data view**: loading, empty, error, unauthorized. A
  component that only handles success is incomplete.
- No layout shift on data arrival — reserve space or use skeletons.

---

## 12. Testing

Every session writes tests for what it built and runs them green before reporting done.

- Vitest + React Testing Library. Query by **role or label**, not test IDs.
- Assert user-visible behaviour, not implementation details.
- `renderWithProviders` from [`src/test/render.tsx`](src/test/render.tsx) for anything
  touching server state.
- MSW for network. Handlers live in `src/test/msw/handlers.ts`; unhandled requests **fail**
  the test. Once the contract exists, derive handlers from it so mocks cannot drift.
- Colocate: `button.tsx` → `button.test.tsx`.
- `npm test` typechecks nothing — run `npm run typecheck` too. `npm run verify` does both.

Not yet set up: Playwright. The root `CLAUDE.md` §7 wants E2E for auth, the primary create
path, and one failure path. Add it when there is a flow to test.

---

## 13. Quality gates

```
npm run typecheck     # tsc -b
npm run lint          # oxlint — currently zero warnings; keep it that way
npm run test          # vitest run
npm run build         # tsc -b && vite build
npm run verify        # all four, in order
```

All four must pass before a task card closes, and the output must be shown — a report of
success without pasted output is not finished.

---

## 14. Deliberately absent

Do not treat these as oversights:

- `src/api/` is empty — no contract exists yet.
- `src/features/` is empty — no feature work has been authorised.
- No sign-in screen, no navigation, no user model — all need the contract and a product
  decision.
- No Playwright, no CI workflow, no icon library, no form library. `react-hook-form` +
  `@hookform/resolvers` will be needed for the first real form; it is not installed because
  nothing uses it yet.
- `src/screens/scaffold-status/` is a demonstration. **Delete it** when the first real
  screen lands.
