import { useInfiniteQuery, useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { apiDelete, apiGet, apiPatch, apiPost } from '@/api/client';
import {
  ClassesKeys,
  type CreateLevelCommand,
  type CreateSectionCommand,
  type ReorderLevelsCommand,
  type UpdateLevelCommand,
  type UpdateSectionCommand,
} from './types';

/** One hook per endpoint, per CONVENTIONS.md §4 / `src/features/README.md`. */

const LEVELS_PATH = '/api/v1/levels';
const LEVEL_PATH = '/api/v1/levels/{id}';
const LEVELS_REORDER_PATH = '/api/v1/levels/reorder';
const SECTIONS_PATH = '/api/v1/sections';
const SECTION_PATH = '/api/v1/sections/{id}';

/** Active by default (spec 6.4.9); `status: 'all'` also returns inactive levels. */
export function useLevels(status?: 'all') {
  return useInfiniteQuery({
    queryKey: [ClassesKeys.Levels, status],
    queryFn: ({ pageParam, signal }) =>
      apiGet(
        LEVELS_PATH,
        { ...(pageParam !== undefined ? { cursor: pageParam } : {}), ...(status !== undefined ? { status } : {}) },
        { signal },
      ),
    initialPageParam: undefined as string | undefined,
    getNextPageParam: (lastPage) => lastPage.nextCursor ?? undefined,
  });
}

/** Gated `level.view`. Refetched when a level dialog opens, so an edit starts from current data. */
export function useLevel(id: string) {
  return useQuery({
    queryKey: [ClassesKeys.Level, id],
    queryFn: ({ signal }) => apiGet(LEVEL_PATH, undefined, { pathParams: { id }, signal }),
  });
}

/** Gated `level.create`. Two mutually exclusive placements: insert-after, or explicit order. */
export function useCreateLevel() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationKey: [ClassesKeys.CreateLevel],
    mutationFn: (payload: CreateLevelCommand) =>
      apiPost(LEVELS_PATH, payload, { idempotencyKey: crypto.randomUUID() }),
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: [ClassesKeys.Levels] });
    },
  });
}

/** Gated `level.update`; changing `status` additionally requires `level.deactivate`. */
export function useUpdateLevel() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationKey: [ClassesKeys.UpdateLevel],
    mutationFn: ({ id, ...payload }: UpdateLevelCommand) =>
      apiPatch(LEVEL_PATH, { id, ...payload }, { pathParams: { id } }),
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: [ClassesKeys.Levels] });
    },
  });
}

/** Gated `level.delete`. Permitted only where nothing has ever referenced the level. */
export function useDeleteLevel() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationKey: [ClassesKeys.DeleteLevel],
    mutationFn: (id: string) => apiDelete(LEVEL_PATH, { pathParams: { id } }),
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: [ClassesKeys.Levels] });
    },
  });
}

/**
 * Gated `level.update`. Posts the whole ordered array and re-renders straight
 * from the response (AC) — the default (active-only) list cache is replaced
 * with the response directly, no extra round trip; the `status=all` view is
 * merely invalidated, since it can also contain levels this response omits.
 */
export function useReorderLevels() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationKey: [ClassesKeys.ReorderLevels],
    mutationFn: (payload: ReorderLevelsCommand) => apiPost(LEVELS_REORDER_PATH, payload),
    onSuccess: (levels) => {
      queryClient.setQueryData([ClassesKeys.Levels, undefined], {
        pageParams: [undefined],
        pages: [{ items: levels, nextCursor: null }],
      });
      void queryClient.invalidateQueries({ queryKey: [ClassesKeys.Levels, 'all'] });
    },
  });
}

/** Gated `level.view`. Not paged — "a two-row seeded list a school extends rarely." */
export function useSections() {
  return useQuery({
    queryKey: [ClassesKeys.Sections],
    queryFn: ({ signal }) => apiGet(SECTIONS_PATH, undefined, { signal }),
  });
}

/** Gated `level.create`. */
export function useCreateSection() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationKey: [ClassesKeys.CreateSection],
    mutationFn: (payload: CreateSectionCommand) =>
      apiPost(SECTIONS_PATH, payload, { idempotencyKey: crypto.randomUUID() }),
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: [ClassesKeys.Sections] });
    },
  });
}

/** Gated `level.update`. Name only. */
export function useUpdateSection() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationKey: [ClassesKeys.UpdateSection],
    mutationFn: (payload: UpdateSectionCommand) =>
      apiPatch(SECTION_PATH, payload, { pathParams: { id: payload.id } }),
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: [ClassesKeys.Sections] });
      // A section's denormalised name is echoed on every level, so a rename
      // must also refresh the level list, not only the sections cache.
      void queryClient.invalidateQueries({ queryKey: [ClassesKeys.Levels] });
    },
  });
}
