import { useInfiniteQuery, useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { apiGet, apiPost } from '@/api/client';
import { AdmissionsKeys, type ApproveAdmissionCommand, type DeclineAdmissionCommand } from './types';

/** One hook per endpoint, per CONVENTIONS.md §4 / `src/features/README.md`. */

const ADMISSIONS_PATH = '/api/v1/admissions';
const ADMISSION_PATH = '/api/v1/admissions/{id}';
const ADMISSION_APPROVE_PATH = '/api/v1/admissions/{id}/approve';
const ADMISSION_DECLINE_PATH = '/api/v1/admissions/{id}/decline';

/**
 * Gated `pupil.view`, SCHOOL-WIDE (spec 6.5.15) — every pending pupil,
 * unconditionally. Cursor-paginated (spec 9.5); the server's own order is
 * kept, never re-sorted client-side.
 */
export function useAdmissionsQueue() {
  return useInfiniteQuery({
    queryKey: [AdmissionsKeys.Queue],
    queryFn: ({ pageParam, signal }) =>
      apiGet(ADMISSIONS_PATH, pageParam !== undefined ? { cursor: pageParam } : {}, { signal }),
    initialPageParam: undefined as string | undefined,
    getNextPageParam: (lastPage) => lastPage.nextCursor ?? undefined,
  });
}

/**
 * `GET /api/v1/admissions/{id}` (TASK-0066). Gated `pupil.view`, SCHOOL-WIDE.
 * 404s unless `id` names a PENDING pupil. Carries `sessionId`/
 * `classAdmittedInto` — the two ids the approve dialog's arm selector needs
 * and the queue row itself cannot supply.
 */
export function useAdmissionRecord(id: string) {
  return useQuery({
    queryKey: [AdmissionsKeys.Record, id],
    queryFn: ({ signal }) => apiGet(ADMISSION_PATH, undefined, { pathParams: { id }, signal }),
  });
}

/**
 * Gated `pupil.admission.approve`. `Idempotency-Key` REQUIRED on this route —
 * the caller generates ONE key per dialog opening (`crypto.randomUUID()`)
 * and reuses it across a double-click or a retry after a dropped response;
 * this hook never generates its own, so it never breaks that contract.
 */
export function useApproveAdmission() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationKey: [AdmissionsKeys.Approve],
    mutationFn: ({ id, idempotencyKey, ...command }: ApproveAdmissionCommand & { idempotencyKey: string }) =>
      apiPost(ADMISSION_APPROVE_PATH, { id, ...command }, { pathParams: { id }, idempotencyKey }),
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: [AdmissionsKeys.Queue] });
    },
  });
}

/** Gated `pupil.admission.approve`. `Idempotency-Key` is optional on this route (contract). */
export function useDeclineAdmission() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationKey: [AdmissionsKeys.Decline],
    mutationFn: ({ id, ...command }: DeclineAdmissionCommand) =>
      apiPost(ADMISSION_DECLINE_PATH, { id, ...command }, { pathParams: { id } }),
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: [AdmissionsKeys.Queue] });
    },
  });
}
