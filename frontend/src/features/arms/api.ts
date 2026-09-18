import { useInfiniteQuery, useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { apiDelete, apiGet, apiPatch, apiPost } from '@/api/client';
import {
  ArmsKeys,
  type ArmStatus,
  type BulkCreateArmsCommand,
  type CreateArmCommand,
  type UpdateArmCommand,
} from './types';

/** One hook per endpoint, per CONVENTIONS.md §4 / `src/features/README.md`. */

const ARMS_PATH = '/api/v1/arms';
const ARM_PATH = '/api/v1/arms/{id}';
const ARMS_BULK_PATH = '/api/v1/arms/bulk';
const ARMS_NEXT_LABEL_PATH = '/api/v1/arms/next-label';

export interface ArmsFilters {
  sessionId?: string;
  levelId?: string;
  label?: string;
  status?: ArmStatus;
  formTeacherAdminId?: string;
}

/**
 * Cursor-paginated (spec 9.5). The server already returns level-chain order
 * then label collated naturally (spec 6.4.5) — never re-sorted client-side
 * (TASK-0045 AC), unlike `useLevels`, which re-sorts because its own list
 * endpoint makes no such ordering promise.
 */
export function useArms(filters: ArmsFilters) {
  return useInfiniteQuery({
    queryKey: [ArmsKeys.List, filters],
    queryFn: ({ pageParam, signal }) =>
      apiGet(
        ARMS_PATH,
        {
          ...(pageParam !== undefined ? { cursor: pageParam } : {}),
          ...(filters.sessionId ? { sessionId: filters.sessionId } : {}),
          ...(filters.levelId ? { levelId: filters.levelId } : {}),
          ...(filters.label ? { label: filters.label } : {}),
          ...(filters.status ? { status: filters.status } : {}),
          ...(filters.formTeacherAdminId ? { formTeacherAdminId: filters.formTeacherAdminId } : {}),
        },
        { signal },
      ),
    initialPageParam: undefined as string | undefined,
    getNextPageParam: (lastPage) => lastPage.nextCursor ?? undefined,
  });
}

/** Gated `arm.view`. Refetched when the detail/edit screen opens, so an edit starts from current data. */
export function useArm(id: string) {
  return useQuery({
    queryKey: [ArmsKeys.Detail, id],
    queryFn: ({ signal }) => apiGet(ARM_PATH, undefined, { pathParams: { id }, signal }),
  });
}

/** Pre-fills a new arm's label (spec 6.4.3); editable, not reserved. Only runs once both ids are chosen. */
export function useNextArmLabel(levelId: string, sessionId: string) {
  return useQuery({
    queryKey: [ArmsKeys.NextLabel, levelId, sessionId],
    queryFn: ({ signal }) => apiGet(ARMS_NEXT_LABEL_PATH, { levelId, sessionId }, { signal }),
    enabled: levelId !== '' && sessionId !== '',
  });
}

/** Gated `arm.create`. `Idempotency-Key` REQUIRED — a fresh one per call, per submit. */
export function useCreateArm() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationKey: [ArmsKeys.Create],
    mutationFn: (payload: CreateArmCommand) =>
      apiPost(ARMS_PATH, payload, { idempotencyKey: crypto.randomUUID() }),
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: [ArmsKeys.List] });
    },
  });
}

/**
 * Gated `arm.create`. `Idempotency-Key` REQUIRED on every call, including a
 * `dryRun` preview — the contract requires the header regardless of `dryRun`
 * (spec 6.4.9). Callers ALWAYS dry-run first (TASK-0045 AC): this hook does
 * not enforce that ordering itself, `BulkCreateArmsDialog` does, by
 * construction — no commit button exists until a preview response is in
 * hand. The list is only invalidated once a non-dry-run call actually wrote.
 */
export function useBulkCreateArms() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationKey: [ArmsKeys.BulkCreate],
    mutationFn: (payload: BulkCreateArmsCommand) =>
      apiPost(ARMS_BULK_PATH, payload, { idempotencyKey: crypto.randomUUID() }),
    onSuccess: (_result, variables) => {
      if (!variables.dryRun) {
        void queryClient.invalidateQueries({ queryKey: [ArmsKeys.List] });
      }
    },
  });
}

/** Gated `arm.update`; setting `formTeacherAdminId` ADDITIONALLY requires `arm.formteacher.assign`. */
export function useUpdateArm() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationKey: [ArmsKeys.Update],
    mutationFn: ({ id, ...payload }: UpdateArmCommand) =>
      apiPatch(ARM_PATH, { id, ...payload }, { pathParams: { id } }),
    onSuccess: (arm) => {
      queryClient.setQueryData([ArmsKeys.Detail, arm.id], arm);
      void queryClient.invalidateQueries({ queryKey: [ArmsKeys.List] });
    },
  });
}

/** Gated `arm.delete`. Permitted only where no enrolment has ever existed. */
export function useDeleteArm() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationKey: [ArmsKeys.Delete],
    mutationFn: (id: string) => apiDelete(ARM_PATH, { pathParams: { id } }),
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: [ArmsKeys.List] });
    },
  });
}
