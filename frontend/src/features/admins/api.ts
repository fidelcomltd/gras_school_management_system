import { useInfiniteQuery, useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { apiDelete, apiGet, apiPatch, apiPost } from '@/api/client';
import {
  AdminsKeys,
  type AdminAccountStatus,
  type AdminAccountSummaryDto,
  type ChangeAdminAccountStatusCommand,
  type CreateAdminAccountCommand,
  type UpdateAdminAccountCommand,
} from './types';

/** One hook per endpoint, per CONVENTIONS.md §4 / `src/features/README.md`. */

const ADMINS_PATH = '/api/v1/admins';
const ADMIN_PATH = '/api/v1/admins/{id}';
const ADMIN_STATUS_PATH = '/api/v1/admins/{id}/status';
const ADMIN_PASSWORD_RESET_PATH = '/api/v1/admins/{id}/password-reset';
const ADMIN_SESSIONS_PATH = '/api/v1/admins/{id}/sessions';

interface AdminListPage {
  items: AdminAccountSummaryDto[];
  nextCursor: string | null;
}
interface AdminListData {
  pageParams: (string | undefined)[];
  pages: AdminListPage[];
}

/** Cursor-paginated (spec 9.5). Deactivated accounts excluded unless `status` names them. */
export function useAdmins(status?: AdminAccountStatus, search?: string) {
  return useInfiniteQuery({
    queryKey: [AdminsKeys.List, status, search],
    queryFn: ({ pageParam, signal }) =>
      apiGet(
        ADMINS_PATH,
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

/** Gated `admin.view`. */
export function useAdmin(id: string) {
  return useQuery({
    queryKey: [AdminsKeys.Detail, id],
    queryFn: ({ signal }) => apiGet(ADMIN_PATH, undefined, { pathParams: { id }, signal }),
  });
}

/**
 * Gated `admin.create`. `Idempotency-Key` REQUIRED. The response embeds the
 * one-time `temporaryPassword` (credential material, TASK-0043): the caller
 * (`CreateAdminDialog`) copies it into its own local state and calls
 * `reset()` immediately, and `gcTime: 0` here means the underlying mutation
 * is evicted from TanStack Query's mutation cache as soon as that `reset()`
 * drops its last observer, rather than lingering for the default 5 minutes.
 */
export function useCreateAdmin() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationKey: [AdminsKeys.Create],
    gcTime: 0,
    mutationFn: (payload: CreateAdminAccountCommand) =>
      apiPost(ADMINS_PATH, payload, { idempotencyKey: crypto.randomUUID() }),
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: [AdminsKeys.List] });
    },
  });
}

/** Gated `admin.update` (or self-edit of `staffName`/`phone` only — not built here, see EditAdminDialog). */
export function useUpdateAdmin() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationKey: [AdminsKeys.Update],
    mutationFn: (payload: UpdateAdminAccountCommand) =>
      apiPatch(ADMIN_PATH, payload, { pathParams: { id: payload.id } }),
    onSuccess: (account) => {
      queryClient.setQueryData([AdminsKeys.Detail, account.id], account);
      void queryClient.invalidateQueries({ queryKey: [AdminsKeys.List] });
    },
  });
}

/**
 * Gated `admin.suspend` / `admin.deactivate` (data-dependent, resolved by
 * the caller — see `AdminDetailScreen`'s `STATUS_TARGETS`). Patches the
 * detail cache AND every list page's matching row directly from the
 * response (AC: "reflects the new status without a full refetch") since
 * `AdminAccountDetailDto`/`AdminAccountSummaryDto` share the same fields.
 */
export function useChangeAdminStatus() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationKey: [AdminsKeys.ChangeStatus],
    mutationFn: (payload: ChangeAdminAccountStatusCommand) =>
      apiPost(ADMIN_STATUS_PATH, payload, { pathParams: { id: payload.id } }),
    onSuccess: (account) => {
      queryClient.setQueryData([AdminsKeys.Detail, account.id], account);
      queryClient.setQueriesData<AdminListData>({ queryKey: [AdminsKeys.List] }, (old) => {
        if (!old) return old;
        return {
          ...old,
          pages: old.pages.map((page) => ({
            ...page,
            items: page.items.map((item) => (item.id === account.id ? account : item)),
          })),
        };
      });
    },
  });
}

/**
 * Gated `admin.password.reset`. Same credential-material handling as
 * `useCreateAdmin` — the caller copies `temporaryPassword` out and resets,
 * and `gcTime: 0` evicts the mutation from cache as soon as it does.
 */
export function useResetAdminPassword() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationKey: [AdminsKeys.ResetPassword],
    gcTime: 0,
    mutationFn: (id: string) => apiPost(ADMIN_PASSWORD_RESET_PATH, undefined, { pathParams: { id } }),
    onSuccess: (result) => {
      void queryClient.invalidateQueries({ queryKey: [AdminsKeys.Detail, result.id] });
    },
  });
}

/** Gated `admin.session.revoke`. Always 204, even with no active sessions. */
export function useRevokeAdminSessions() {
  return useMutation({
    mutationKey: [AdminsKeys.RevokeSessions],
    mutationFn: (id: string) => apiDelete(ADMIN_SESSIONS_PATH, { pathParams: { id } }),
  });
}
