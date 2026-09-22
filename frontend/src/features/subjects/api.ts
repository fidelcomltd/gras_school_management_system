import { useInfiniteQuery, useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { apiDelete, apiGet, apiPatch, apiPost, apiPut } from '@/api/client';
import {
  SubjectsKeys,
  type CreateSubjectCommand,
  type SubjectMappingGridEntryInput,
  type SubjectStatus,
  type UpdateSubjectCommand,
} from './types';

/** One hook per endpoint, per CONVENTIONS.md §4 / `src/features/README.md`. */

const SUBJECTS_PATH = '/api/v1/subjects';
const SUBJECT_PATH = '/api/v1/subjects/{id}';
const GRID_PATH = '/api/v1/subject-mappings';
const COPY_PATH = '/api/v1/subject-mappings/copy';
const PREFILL_PATH = '/api/v1/subject-mappings/prefill';

/** Gated `subject.view`. */
export function useSubjects(status?: SubjectStatus) {
  return useInfiniteQuery({
    queryKey: [SubjectsKeys.List, status],
    queryFn: ({ pageParam, signal }) =>
      apiGet(
        SUBJECTS_PATH,
        { ...(pageParam !== undefined ? { cursor: pageParam } : {}), ...(status ? { status } : {}), pageSize: 100 },
        { signal },
      ),
    initialPageParam: undefined as string | undefined,
    getNextPageParam: (lastPage) => lastPage.nextCursor ?? undefined,
  });
}

/** Gated `subject.create`. */
export function useCreateSubject() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationKey: [SubjectsKeys.Create],
    mutationFn: (payload: CreateSubjectCommand) => apiPost(SUBJECTS_PATH, payload, { idempotencyKey: crypto.randomUUID() }),
    onSuccess: () => void queryClient.invalidateQueries({ queryKey: [SubjectsKeys.List] }),
  });
}

/** Gated `subject.update` (and `subject.deactivate` for a status change). `null` fields are unchanged. */
export function useUpdateSubject() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationKey: [SubjectsKeys.Update],
    mutationFn: (payload: UpdateSubjectCommand) => apiPatch(SUBJECT_PATH, payload, { pathParams: { id: payload.id } }),
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: [SubjectsKeys.List] });
      void queryClient.invalidateQueries({ queryKey: [SubjectsKeys.Grid] });
    },
  });
}

/** Gated `subject.delete`. Refused (409) once the subject was ever mapped or scored; deactivate instead. */
export function useDeleteSubject() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationKey: [SubjectsKeys.Delete],
    mutationFn: (id: string) => apiDelete(SUBJECT_PATH, { pathParams: { id } }),
    onSuccess: () => void queryClient.invalidateQueries({ queryKey: [SubjectsKeys.List] }),
  });
}

/** Spec 6.6.5: every subject by every level for one term. */
export function useMappingGrid(termId: string) {
  return useQuery({
    queryKey: [SubjectsKeys.Grid, termId],
    queryFn: ({ signal }) => apiGet(GRID_PATH, { term_id: termId }, { signal }),
    enabled: termId !== '',
  });
}

/** Whole-grid save; `dryRun` previews additions and endings without writing (6.6.9). */
export function useSaveMappingGrid(termId: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationKey: [SubjectsKeys.SaveGrid, termId],
    mutationFn: ({ entries, dryRun }: { entries: SubjectMappingGridEntryInput[]; dryRun: boolean }) =>
      apiPut(GRID_PATH, { termId, entries, dryRun }, { params: { term_id: termId } }),
    onSuccess: (result) => {
      if (!result.dryRun) {
        void queryClient.invalidateQueries({ queryKey: [SubjectsKeys.Grid, termId] });
        void queryClient.invalidateQueries({ queryKey: [SubjectsKeys.List] });
      }
    },
  });
}

/** Copies another term's mappings into this one (6.6.8). */
export function useCopyMappings(termId: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationKey: [SubjectsKeys.Copy, termId],
    mutationFn: ({ sourceTermId, dryRun }: { sourceTermId: string; dryRun: boolean }) =>
      apiPost(COPY_PATH, { sourceTermId, destinationTermId: termId, dryRun }, { idempotencyKey: crypto.randomUUID() }),
    onSuccess: (result) => {
      if (!result.dryRun) void queryClient.invalidateQueries({ queryKey: [SubjectsKeys.Grid, termId] });
    },
  });
}

/** Adds the school's standard nursery/primary subject list to this term (human ruling 2026-09-16). Additive only. */
export function usePrefillMappings(termId: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationKey: [SubjectsKeys.Prefill, termId],
    mutationFn: (dryRun: boolean) => apiPost(PREFILL_PATH, { termId, dryRun }, { idempotencyKey: crypto.randomUUID() }),
    onSuccess: (result) => {
      if (!result.dryRun) void queryClient.invalidateQueries({ queryKey: [SubjectsKeys.Grid, termId] });
    },
  });
}
