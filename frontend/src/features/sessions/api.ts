import { useInfiniteQuery, useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { apiGet, apiPatch, apiPost } from '@/api/client';
import {
  SessionsKeys,
  type CreateSessionCommand,
  type ReopenTermCommand,
  type SessionState,
  type UpdateSessionCommand,
  type UpdateTermCommand,
} from './types';

/** One hook per endpoint, per CONVENTIONS.md §4 / `src/features/README.md`. */

const SESSIONS_PATH = '/api/v1/sessions';
const SESSION_PATH = '/api/v1/sessions/{id}';
const TERM_PATH = '/api/v1/terms/{id}';
const TERM_OPEN_PATH = '/api/v1/terms/{id}/open';
const TERM_CLOSE_PATH = '/api/v1/terms/{id}/close';
const TERM_REOPEN_PATH = '/api/v1/terms/{id}/reopen';

/** Cursor-paginated (spec 9.5). `state` is the only server-side filter. */
export function useSessions(state?: SessionState) {
  return useInfiniteQuery({
    queryKey: [SessionsKeys.List, state],
    queryFn: ({ pageParam, signal }) =>
      apiGet(
        SESSIONS_PATH,
        { ...(pageParam !== undefined ? { cursor: pageParam } : {}), ...(state !== undefined ? { state } : {}) },
        { signal },
      ),
    initialPageParam: undefined as string | undefined,
    getNextPageParam: (lastPage) => lastPage.nextCursor ?? undefined,
  });
}

/** Gated `session.view` server-side (403, not a shape this hook needs to know). */
export function useSession(id: string, enabled = true) {
  return useQuery({
    queryKey: [SessionsKeys.Detail, id],
    queryFn: ({ signal }) => apiGet(SESSION_PATH, undefined, { pathParams: { id }, signal }),
    enabled: enabled && id !== '',
  });
}

/** Gated `session.create`. `Idempotency-Key` REQUIRED — a fresh one per call, per submit. */
export function useCreateSession() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationKey: [SessionsKeys.Create],
    mutationFn: (payload: CreateSessionCommand) =>
      apiPost(SESSIONS_PATH, payload, { idempotencyKey: crypto.randomUUID() }),
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: [SessionsKeys.List] });
    },
  });
}

/** Gated `session.update`. */
export function useUpdateSession(id: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationKey: [SessionsKeys.UpdateSession, id],
    mutationFn: (payload: UpdateSessionCommand) =>
      apiPatch(SESSION_PATH, payload, { pathParams: { id } }),
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: [SessionsKeys.Detail, id] });
      void queryClient.invalidateQueries({ queryKey: [SessionsKeys.List] });
    },
  });
}

/** Gated `session.update`. Invalidates the owning session's detail, not just the term. */
export function useUpdateTerm(sessionId: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationKey: [SessionsKeys.UpdateTerm, sessionId],
    mutationFn: ({ id, ...payload }: UpdateTermCommand) =>
      apiPatch(TERM_PATH, { id, ...payload }, { pathParams: { id } }),
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: [SessionsKeys.Detail, sessionId] });
    },
  });
}

/** Gated `term.open`. Precondition failures return a naming message — surfaced verbatim. */
export function useOpenTerm(sessionId: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationKey: [SessionsKeys.OpenTerm, sessionId],
    mutationFn: (id: string) => apiPost(TERM_OPEN_PATH, undefined, { pathParams: { id } }),
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: [SessionsKeys.Detail, sessionId] });
      void queryClient.invalidateQueries({ queryKey: [SessionsKeys.List] });
    },
  });
}

/** Gated `term.close`. */
export function useCloseTerm(sessionId: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationKey: [SessionsKeys.CloseTerm, sessionId],
    mutationFn: (id: string) => apiPost(TERM_CLOSE_PATH, undefined, { pathParams: { id } }),
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: [SessionsKeys.Detail, sessionId] });
      void queryClient.invalidateQueries({ queryKey: [SessionsKeys.List] });
    },
  });
}

/** Gated `term.close` PLUS `isSuperAdmin` (handler-checked, not a privilege code). */
export function useReopenTerm(sessionId: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationKey: [SessionsKeys.ReopenTerm, sessionId],
    mutationFn: (payload: ReopenTermCommand) =>
      apiPost(TERM_REOPEN_PATH, payload, { pathParams: { id: payload.id } }),
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: [SessionsKeys.Detail, sessionId] });
    },
  });
}
