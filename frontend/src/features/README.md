# `src/features/` — server-state modules

Empty. No feature code exists yet; this file defines the shape the first one takes.

## Grouping rule

**One folder per OpenAPI tag.** The tag on an endpoint in `contracts/openapi.json` decides
which folder its hook lives in — `/api/v1/Students/{id}` tagged `Students` goes in
`src/features/students/`.

Why the tag and not something cleverer: the tag is already the backend's own grouping of
its endpoints, it is machine-readable, and it means two agents in two sessions independently
place the same hook in the same folder without negotiating. Inventing a frontend-side
taxonomy would drift from the contract the moment the backend regroups.

Folder names are the tag, lowercased and kebab-cased. If an endpoint carries several tags,
use the first; if it carries none, that is a contract defect — raise it, do not guess.

## Layout

```
src/features/<tag>/
  api.ts      # query & mutation hooks — one hook per endpoint, no exceptions
  types.ts    # query-key enum, *Variables types, contract-derived re-exports (no hand-typed DTOs)
  store.ts    # optional; Zustand store for client state tied to these hooks
  components/ # optional; only if a component is shared across screens in this feature
```

## `types.ts`

Holds the query-key enum and the variables types for mutations. **Not** hand-written
request/response interfaces — those already exist, generated, in `src/api/schema.d.ts`.
Re-export the ones this feature needs by indexing `components['schemas']` so the type stays
locked to the contract instead of a copy that can drift from it:

```ts
import type { components } from '@/api/schema';

export enum SampleRecordsKeys {
  List = 'sampleRecords.list',
  Create = 'sampleRecords.create',
}

/** Contract-derived — never hand-typed. Source of truth: src/api/schema.d.ts. */
export type SampleRecordDto = components['schemas']['SampleRecordDto'];
export type CreateSampleRecordCommand = components['schemas']['CreateSampleRecordCommand'];
```

The worked example below is the real `/api/v1/reference/records` endpoint — the only one
`contracts/openapi.json` currently declares (its `Reference` tag is test scaffolding, not a
product tag; see `STATE.md ## Contract`). Follow the identical shape for the first real
feature — swap the path, the schema names, and the folder name for the tag you're
implementing.

## `api.ts`

One hook per endpoint. Hooks call `apiGet`/`apiPost` from `@/api/client` and nothing else — no
`getRequest`/`postRequest` from `@/lib/http` directly, no `axios`, no `fetch`. Going through
`client.ts` is what makes the response type inferred rather than asserted; see
`src/api/README.md` for why the plain verb helpers are not a sanctioned fallback.

```ts
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { apiGet, apiPost } from '@/api/client';
import { SampleRecordsKeys, type CreateSampleRecordCommand } from './types';

const PATH = '/api/v1/reference/records';

export function useSampleRecords(page?: number, pageSize?: number) {
  return useQuery({
    // `data` here is inferred as PagedResultOfSampleRecordDto — never asserted.
    queryKey: [SampleRecordsKeys.List, page, pageSize],
    queryFn: ({ signal }) => apiGet(PATH, { page, pageSize }, { signal }),
  });
}

export function useCreateSampleRecord() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationKey: [SampleRecordsKeys.Create],
    mutationFn: (payload: CreateSampleRecordCommand) => apiPost(PATH, payload),
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: [SampleRecordsKeys.List] });
    },
  });
}
```

## Rules

- **Never** `queryClient.invalidateQueries()` with no key. Invalidate precisely.
- **Never** an inline string query key. It goes in the enum in `types.ts`.
- Pass TanStack's `signal` through to `apiGet`/`apiPost`'s `options` so cancelled queries
  abort in flight.
- Server state is not copied into `useState` or a Zustand store. `store.ts` is for client
  state only — filters, selection, wizard step.
- Every data view handles four states: loading, empty, error, unauthorized.

See [`CONVENTIONS.md`](../../CONVENTIONS.md) for the rest.
