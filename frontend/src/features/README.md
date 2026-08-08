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
  types.ts    # request/response interfaces, *Variables types, the query-key enum
  store.ts    # optional; Zustand store for client state tied to these hooks
  components/ # optional; only if a component is shared across screens in this feature
```

## `types.ts`

Holds the query-key enum, the variables types for mutations, and — until `src/api/` is
generated — hand-written request/response interfaces.

```ts
export enum StudentsKeys {
  List = 'students.list',
  Detail = 'students.detail',
  Create = 'students.create',
  UpdateProfile = 'students.updateProfile',
}

export interface IStudentListItem {
  id: string;
  fullName: string;
  admissionNumber: string;
}

export interface IUpdateStudentProfileVariables {
  studentId: string;
  payload: IUpdateStudentProfileRequest;
}
```

## `api.ts`

One hook per endpoint. Hooks call the verb helpers from `@/lib/http` and nothing else — no
`axios`, no `fetch`.

```ts
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { getRequest, putRequest } from '@/lib/http';
import { StudentsKeys, type IStudentListItem, type IUpdateStudentProfileVariables } from './types';

const BASE = '/api/v1/Students';

/** Invalidation is centralised so every mutation invalidates the same keys. */
function useStudentInvalidation() {
  const queryClient = useQueryClient();
  return (studentId: string) => {
    void queryClient.invalidateQueries({ queryKey: [StudentsKeys.Detail, studentId] });
    void queryClient.invalidateQueries({ queryKey: [StudentsKeys.List] });
  };
}

export function useStudents() {
  return useQuery({
    queryKey: [StudentsKeys.List],
    queryFn: ({ signal }) => getRequest<IStudentListItem[]>(BASE, { signal }),
  });
}

export function useUpdateStudentProfile() {
  const invalidateStudent = useStudentInvalidation();
  return useMutation({
    mutationKey: [StudentsKeys.UpdateProfile],
    mutationFn: ({ studentId, payload }: IUpdateStudentProfileVariables) =>
      putRequest<void, IUpdateStudentProfileRequest>(`${BASE}/${studentId}`, payload),
    onSuccess: (_data, { studentId }) => invalidateStudent(studentId),
  });
}
```

## Rules

- **Never** `queryClient.invalidateQueries()` with no key. Invalidate precisely.
- **Never** an inline string query key. It goes in the enum in `types.ts`.
- Pass TanStack's `signal` through to the verb helper so cancelled queries abort in flight.
- Server state is not copied into `useState` or a Zustand store. `store.ts` is for client
  state only — filters, selection, wizard step.
- Every data view handles four states: loading, empty, error, unauthorized.

See [`CONVENTIONS.md`](../../CONVENTIONS.md) for the rest.
