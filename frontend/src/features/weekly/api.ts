import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { apiGet, apiPost, apiPut } from '@/api/client';
import type { WeeklyCellInput } from './types';

/** Query keys for weekly reports (spec 6.10). */
export const WeeklyKeys = {
  Grid: 'weekly.grid',
  Pupil: 'weekly.pupil',
  Completion: 'weekly.completion',
  Illness: 'weekly.illness',
} as const;

export const gridKey = (armId: string, termId: string, weekNumber: number | null) => [WeeklyKeys.Grid, armId, termId, weekNumber];

/**
 * One arm's grid for one week; `weekNumber` null opens the current week. Not refetched on focus: the screen holds unsaved
 * cells on top of it and merges saves in itself.
 */
export function useWeeklyGrid(armId: string, termId: string, weekNumber: number | null) {
  return useQuery({
    queryKey: gridKey(armId, termId, weekNumber),
    queryFn: ({ signal }) =>
      apiGet('/api/v1/arms/{armId}/weekly', weekNumber === null ? { termId } : { termId, weekNumber }, { pathParams: { armId }, signal }),
    enabled: armId !== '' && termId !== '',
    staleTime: 60_000,
    refetchOnWindowFocus: false,
  });
}

/** Sparse save of one week's cells (spec 6.10.11). Each attempt carries a fresh idempotency key. */
export function useSaveWeeklyNotes(armId: string) {
  return useMutation({
    mutationKey: [WeeklyKeys.Grid, 'save', armId],
    mutationFn: (body: { termId: string; weekNumber: number; cells: WeeklyCellInput[] }) =>
      apiPut('/api/v1/arms/{armId}/weekly', { armId, ...body }, { pathParams: { armId }, idempotencyKey: crypto.randomUUID() }),
  });
}

/** Publish or unpublish one arm's week (spec 6.10.8). */
export function useSetWeekPublished(armId: string, termId: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationKey: [WeeklyKeys.Grid, 'publish', armId, termId],
    mutationFn: ({ weekNumber, publish }: { weekNumber: number; publish: boolean }) =>
      publish
        ? apiPost('/api/v1/arms/{armId}/weekly/{weekNumber}/publish', { termId }, { pathParams: { armId, weekNumber } })
        : apiPost('/api/v1/arms/{armId}/weekly/{weekNumber}/unpublish', { termId }, { pathParams: { armId, weekNumber } }),
    onSuccess: () => void queryClient.invalidateQueries({ queryKey: [WeeklyKeys.Grid, armId, termId] }),
  });
}

/** The arm's auto-publish option (spec 6.10.8). */
export function useSetAutoPublish(armId: string, termId: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationKey: [WeeklyKeys.Grid, 'settings', armId],
    mutationFn: (autoPublish: boolean) => apiPut('/api/v1/arms/{armId}/weekly/settings', { armId, autoPublish }, { pathParams: { armId } }),
    onSuccess: () => void queryClient.invalidateQueries({ queryKey: [WeeklyKeys.Grid, armId, termId] }),
  });
}

/** One pupil's whole term, week by week. */
export function usePupilWeekly(pupilId: string, termId: string) {
  return useQuery({
    queryKey: [WeeklyKeys.Pupil, pupilId, termId],
    queryFn: ({ signal }) => apiGet('/api/v1/pupils/{pupilId}/weekly', { termId }, { pathParams: { pupilId }, signal }),
    enabled: pupilId !== '' && termId !== '',
    staleTime: 30_000,
  });
}

/** Spec 6.10.12 completion report. */
export function useWeeklyCompletion(termId: string, weekNumber: number | null) {
  return useQuery({
    queryKey: [WeeklyKeys.Completion, termId, weekNumber],
    queryFn: ({ signal }) =>
      apiGet('/api/v1/reports/weekly-completion', weekNumber === null ? { termId } : { termId, weekNumber }, { signal }),
    enabled: termId !== '',
    staleTime: 60_000,
  });
}

/** Spec 6.10.12 illness observation summary (safeguarding privilege). */
export function useWeeklyIllness(termId: string, enabled: boolean) {
  return useQuery({
    queryKey: [WeeklyKeys.Illness, termId],
    queryFn: ({ signal }) => apiGet('/api/v1/reports/weekly-illness', { termId }, { signal }),
    enabled: enabled && termId !== '',
    staleTime: 60_000,
  });
}
