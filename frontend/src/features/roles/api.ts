import { useInfiniteQuery, useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { apiDelete, apiGet, apiPatch, apiPost } from '@/api/client';
import { RolesKeys, type CreateRoleCommand, type RoleStatus, type UpdateRoleCommand } from './types';

/** One hook per endpoint, per CONVENTIONS.md §4 / `src/features/README.md`. */

const ROLES_PATH = '/api/v1/roles';
const ROLE_PATH = '/api/v1/roles/{id}';
const PRIVILEGES_PATH = '/api/v1/privileges';

/** Cursor-paginated (spec 9.5). Archived roles excluded unless `status` names them. */
export function useRoles(status?: RoleStatus, search?: string) {
  return useInfiniteQuery({
    queryKey: [RolesKeys.List, status, search],
    queryFn: ({ pageParam, signal }) =>
      apiGet(
        ROLES_PATH,
        {
          ...(pageParam !== undefined ? { cursor: pageParam } : {}),
          ...(status !== undefined ? { status } : {}),
          ...(search !== undefined && search !== '' ? { search } : {}),
        },
        { signal },
      ),
    initialPageParam: undefined as string | undefined,
    getNextPageParam: (lastPage) => lastPage.nextCursor ?? undefined,
  });
}

/** Gated `role.view`. Refetched when the edit dialog opens, mirroring `useLevel`. */
export function useRole(id: string) {
  return useQuery({
    queryKey: [RolesKeys.Detail, id],
    queryFn: ({ signal }) => apiGet(ROLE_PATH, undefined, { pathParams: { id }, signal }),
  });
}

/**
 * Authenticated only, no privilege required (spec 6.1.14) — every signed-in
 * admin needs the full menu to understand what a role can be built from.
 * Fixed 93-row compile-time register (§9.5's cursor rule does not apply):
 * `staleTime: Infinity` since it cannot change during a session.
 */
export function usePrivileges() {
  return useQuery({
    queryKey: [RolesKeys.Privileges],
    queryFn: ({ signal }) => apiGet(PRIVILEGES_PATH, undefined, { signal }),
    staleTime: Infinity,
  });
}

/** Gated `role.create`. `Idempotency-Key` REQUIRED. */
export function useCreateRole() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationKey: [RolesKeys.Create],
    mutationFn: (payload: CreateRoleCommand) => apiPost(ROLES_PATH, payload, { idempotencyKey: crypto.randomUUID() }),
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: [RolesKeys.List] });
    },
  });
}

/** Gated `role.update`. A system role rejects the whole request with 409. */
export function useUpdateRole() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationKey: [RolesKeys.Update],
    mutationFn: (payload: UpdateRoleCommand) => apiPatch(ROLE_PATH, payload, { pathParams: { id: payload.id } }),
    onSuccess: (role) => {
      queryClient.setQueryData([RolesKeys.Detail, role.id], role);
      void queryClient.invalidateQueries({ queryKey: [RolesKeys.List] });
    },
  });
}

/** Gated `role.delete`. A system role returns 409, surfaced verbatim by the caller. */
export function useDeleteRole() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationKey: [RolesKeys.Delete],
    mutationFn: (id: string) => apiDelete(ROLE_PATH, { pathParams: { id } }),
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: [RolesKeys.List] });
    },
  });
}
