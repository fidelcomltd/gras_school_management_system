import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { apiGet, apiPost, apiPut } from '@/api/client';
import { ResultsKeys, type SaveScoreSheetCommand, type VoidScoreSheetCommand } from './types';

/** One hook per endpoint, per CONVENTIONS.md §4 / `src/features/README.md`. */

const ARM_SUBJECTS_PATH = '/api/v1/arms/{id}/subjects';
const SCORE_SHEET_PATH = '/api/v1/arms/{armId}/score-sheets';
const VOID_PATH = '/api/v1/arms/{armId}/score-sheets/void';

/** The subjects an arm takes in a term: level mappings plus the arm's own exceptions (spec 6.6.7). */
export function useArmSubjects(armId: string, termId: string) {
  return useQuery({
    queryKey: [ResultsKeys.ArmSubjects, armId, termId],
    queryFn: ({ signal }) => apiGet(ARM_SUBJECTS_PATH, { term_id: termId }, { pathParams: { id: armId }, signal }),
    enabled: armId !== '' && termId !== '',
  });
}

/** Spec 6.7.4: every active pupil in the arm, with their marks for one subject. */
export function useScoreSheet(armId: string, subjectId: string, termId: string) {
  return useQuery({
    queryKey: [ResultsKeys.ScoreSheet, armId, subjectId, termId],
    queryFn: ({ signal }) => apiGet(SCORE_SHEET_PATH, { subjectId, termId }, { pathParams: { armId }, signal }),
    enabled: armId !== '' && subjectId !== '' && termId !== '',
    // A sheet being typed into must not refetch underneath the admin; the version check catches concurrent edits.
    staleTime: Infinity,
    refetchOnWindowFocus: false,
  });
}

/** Whole-sheet save (6.7.4); a stale `version` is a 409 naming who changed it. */
export function useSaveScoreSheet(armId: string, subjectId: string, termId: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationKey: [ResultsKeys.SaveScoreSheet, armId, subjectId, termId],
    mutationFn: (payload: SaveScoreSheetCommand) => apiPut(SCORE_SHEET_PATH, payload, { pathParams: { armId } }),
    onSuccess: (sheet) => queryClient.setQueryData([ResultsKeys.ScoreSheet, armId, subjectId, termId], sheet),
  });
}

/** Voids every row of the sheet with a reason (6.7.4), gated `result.score.void`. */
export function useVoidScoreSheet(armId: string, subjectId: string, termId: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationKey: [ResultsKeys.VoidScoreSheet, armId, subjectId, termId],
    mutationFn: (payload: VoidScoreSheetCommand) => apiPost(VOID_PATH, payload, { pathParams: { armId } }),
    onSuccess: () => void queryClient.invalidateQueries({ queryKey: [ResultsKeys.ScoreSheet, armId, subjectId, termId] }),
  });
}
