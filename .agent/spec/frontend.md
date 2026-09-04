# Frontend specification — CLAUDE.md §7

This file **is** §7 of the root CLAUDE.md. It was moved out of that file on 2026-09-04 so that
only `frontend-dev` loads it; every reference to "§7" anywhere in the repo means this document.
Binding in full. Concrete conventions that implement it: `frontend/CONVENTIONS.md`. Deliberate
omissions on record: `frontend/HANDOFF.md` §14.


**Structure & style**
- TypeScript `strict: true`, `noUncheckedIndexedAccess: true`. No `any`; no non-null `!` without an adjacent comment justifying it. `// @ts-ignore` is a blocker.
- Feature-first folders under `src/features/<feature>/` (`components/`, `hooks/`, `api/`, `types.ts`). **Promotion discipline:** code lives in the feature that owns it until a *second* feature needs it, then it moves to `src/shared/` in its own commit — never pre-emptively.
- No barrel `index.ts` re-export chains beyond one level.
- Functional components, hooks only. Colocate tests next to source.

**Data layer**
- All server communication goes through the generated client in `src/api/`. **Zero** raw `fetch`/`axios` calls to the API elsewhere in the tree.
- TanStack Query for all server state. Centralised query-key factory per feature — no inline string keys. Explicit `staleTime`. Mutations invalidate precisely, not `queryClient.invalidateQueries()` with no key.
- Server state never duplicated into `useState`/global store. Client state (UI, forms, filters) is separate and local by default.
- Zod validation at the network boundary only, for anything the generated types can't guarantee. Not re-validating trusted internal shapes.
- Loading, empty, error, and unauthorized are **four required states** for every data view. A component that only handles success is incomplete.

**UI & correctness**
- Forms: react-hook-form + zod resolver. Validation rules mirror the backend's; where they diverge, the backend is authoritative and the client must handle the server-side rejection gracefully.
- Error boundaries per route. A failed query never blanks the app.
- Accessibility is not optional: semantic elements, labelled inputs, keyboard-reachable interactive elements, visible focus, `aria-live` for async status. No `div` with `onClick` standing in for a button.
- No layout shift on data arrival — reserve space or use skeletons.
- Env config via typed, validated env module. No hardcoded API hosts.
- No secrets or privileged logic in the bundle. Authorization decisions are backend-enforced; the UI only *hides*, it never *protects*.

**Testing**
- Vitest + React Testing Library, queried by role/label — not by test ID unless there is no accessible handle.
- MSW for network mocking, handlers derived from the OpenAPI document so mocks cannot drift from the contract.
- Playwright for the critical flows: auth, the primary create path, and one failure path.
- Tests assert user-visible behaviour, not implementation details.


