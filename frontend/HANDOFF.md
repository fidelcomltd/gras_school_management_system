# Handoff — frontend scaffold

**Session:** 2026-08-03 · frontend agent · scaffold only, zero feature code.

**2026-09-05 — three deviations below superseded.** Open question 11 (open since this session,
2026-08-27) asked whether to follow §7's `src/features/` structure, `react-hook-form`+zod, and
Playwright, all deliberately skipped here. The human resolved it 2026-09-05: **follow §7**, with
the proviso "as long as it will lead to a better file and codebase structure" — full text in
`.agent/decisions/2026-Q3.md` (2026-09-05 entry). TASK-0020 implements the resolution. The
reasoning below is **not wrong for its time** — it was written before the product specification
existed, so these were genuinely open questions rather than oversights — and is left in place
for the record. Specifically now superseded:

- **"Deliberately not done" below: "No Playwright" and "No `react-hook-form`."** Both are now
  added (TASK-0020):
  `react-hook-form` 7.87.0 + `@hookform/resolvers` 5.9.1, and `@playwright/test` 1.63.0 with a
  CI job and one smoke spec (`e2e/smoke.spec.ts`). No form and no real E2E flow exist yet — both
  still wait on TASK-0021 having something to build against.
- **Directory layout / "Feature grouping = OpenAPI tag" (decision 5).** Still true for the
  server-state part of a feature folder, but `src/features/<feature>/` is now also where the
  screen itself lives, per §7's feature-first mandate — see `CONVENTIONS.md` §4 "Feature
  folders". `src/screens/` is deprecated (kept only for `scaffold-status/` until TASK-0021
  deletes it) rather than the ongoing home for screens this scaffold assumed.
- **Open question 1 (auth mechanism)** is untouched by this — it was resolved separately
  2026-08-26 (cookie session, see root `CLAUDE.md` §5 / `STATE.md`) and is TASK-0021's to
  implement, not TASK-0020's.

---

## What exists

| Area | State |
|---|---|
| Build | Vite 8, React 19, TypeScript 6 `strict` + `noUncheckedIndexedAccess` + `exactOptionalPropertyTypes` |
| Design tokens | Full semantic layer, light + dark, derived from the crest (`src/styles/`) |
| Primitives | `button`, `input`, `select`, `dialog`, `field`, `icons` on Base UI (`src/components/ui/`) |
| Config | `src/config/env-values.ts`, Zod-validated at boot; `.env.example` committed, `.env` ignored |
| HTTP | Axios client + 5 verb helpers + `ApiError` normalisation (`src/lib/http/`) |
| Auth | In-memory bearer token, backend-reported expiry, scheduled logout, single-flight refresh seam (`src/lib/auth/`) |
| State | TanStack Query defaults, Zustand theme + session status stores |
| Shell | App shell, router, per-route error boundary, theme toggle |
| Tests | 130 tests across 15 files, all green |
| Docs | `README.md`, `CONVENTIONS.md`, `src/api/README.md`, `src/features/README.md` |

**Gates:** `typecheck` ✓ `lint` ✓ (zero warnings) `test` ✓ 130/130 `build` ✓

Bundle after vendor splitting: 48.9 kB app + ~554 kB vendor (~196 kB gzip total).

---

## Decisions taken

1. **`@base-ui/react` 1.6.0, not `@base-ui-components/react`.** Base UI renamed its package;
   the old name is still on a 1.0.0-rc. Verified: zero Radix in the dependency tree — Base
   UI's only UI dependency is Floating UI.
2. **React Router 7.18.2, not 8.** v8 requires Node ≥22.22.0; this machine runs 22.21.0.
   Pinning avoids shipping an engine mismatch. Revisit after a Node upgrade.
3. **Split `src/api/` from `src/lib/http/`** (your call, confirmed this session). `src/api/`
   stays empty and reserved for the generated contract client; the hand-written transport
   lives in `src/lib/http/`. Satisfies both "no raw axios outside the http layer" and
   "nothing in `src/api/` is hand-edited".
4. **Bearer token, isolated** (your call, confirmed this session). Memory-only — no
   `localStorage`, so XSS cannot read it. See the open question below.
5. **Feature grouping = OpenAPI tag.** One folder per tag, lowercased. Chosen because it is
   machine-derivable from the contract, so two sessions independently place the same hook in
   the same folder. Documented in `src/features/README.md`.
6. **All files `kebab-case`**, including components. One rule, no exceptions to remember.
7. **oxlint over ESLint** — it ships with the Vite 8 template. Config tightened to add
   `jsx-a11y`, `import`, and `promise` plugins; a11y rules are errors.
8. **Semantic tokens are the only colour API.** Brand ramps exist but are decorative-only.
   A component written correctly needs zero `dark:` classes.

---

## Open questions for you

1. **Auth mechanism needs human sign-off.** Root `CLAUDE.md` §5 recommends HttpOnly cookie
   sessions and marks auth as always requiring sign-off; the frontend brief specified bearer
   with expiry scheduling. I built bearer, confined to `src/lib/auth/auth-session.ts` and the
   interceptors in `http-client.ts` — a switch to cookies touches those two files and nothing
   else. **Confirm before any sign-in work begins.**
2. **No contract to build against.** `contracts/openapi.json` does not exist. `src/api/` and
   `src/features/` are empty as a result, and the refresh-token endpoint is a registered
   seam (`setRefreshHandler`) rather than an implementation — wiring it would have meant
   inventing an endpoint.
3. **Client generator still unchosen** (your `STATE.md` open question 6). I have not added
   one. `src/api/README.md` records the steps for whichever you pick; `openapi-typescript`
   remains the lightest fit given TanStack Query already owns state.
4. **Repo is not a git repository.** Nothing is committed. `.gitignore` correctly excludes
   `.env` — worth `git init` before that changes.

---

## Deliberately not done

- **No feature UI, no business logic** — the scope boundary you set.
- **No sign-in screen, navigation, or user model** — all need the contract and a product
  decision on information architecture.
- ~~**No Playwright.**~~ **Superseded 2026-09-05, added TASK-0020** — see the note at the top
  of this file. Root `CLAUDE.md` §7 wants E2E on auth, the primary create path, and one failure
  path; there was no flow to test yet at scaffold time, and there still isn't one — TASK-0020
  only lands the harness and a smoke placeholder (`e2e/smoke.spec.ts`).
- **No CI workflow.** Root-level CI is orchestrator-owned. Gate commands for `STATE.md`:
  `npm run typecheck`, `npm run lint`, `npm run test`, `npm run build` (or `npm run verify`).
  (The frontend CI workflow itself now exists — `.github/workflows/frontend-ci.yml`, landed
  between this session and TASK-0020 — this bullet is scaffold-time history, not current state.)
- ~~**No `react-hook-form`.**~~ **Superseded 2026-09-05, added TASK-0020** — see the note at
  the top of this file. `react-hook-form` 7.87.0 + `@hookform/resolvers` 5.9.1 are installed;
  no form is built yet, same reasoning as originally written below.
- **No crest asset.** The logo was not in the repo, so `AppShell` renders a placeholder
  wordmark. Drop the real file into `public/` and swap it.
- **`src/screens/scaffold-status/`** is a live demonstration of the tokens and primitives.
  It is not a feature — delete it when the first real screen lands.

---

## One thing to watch

Base UI's `Select` renders the **raw value** in the trigger unless `items` is passed to the
root — pick "Michaelmas" and the closed trigger reads `michaelmas`. This is Base UI's
documented contract, not a bug in the wrapper. It is called out at the top of
`src/components/ui/select.tsx`, and `select.test.tsx` pins both behaviours so a future
version change surfaces as a test failure rather than a UI regression.

---

## Suggested first task card

`TASK-0001 — Choose the client generator and wire the codegen script`, blocked on the first
backend build emitting `contracts/openapi.json`. Until then the frontend cannot start
feature work without guessing across the boundary.
