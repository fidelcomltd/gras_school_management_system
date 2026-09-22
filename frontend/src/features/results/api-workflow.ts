import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { apiGet, apiPost } from '@/api/client';
import { RecordsKeys } from './api-records';
import { ResultsKeys } from './types';

/**
 * The result set's lifecycle (spec 6.7.5, 6.7.8–6.7.11): the readiness gate, computation, and every transition. Each
 * transition invalidates the readiness gate and every entry sheet for the arm, since each carries the set's state.
 */

export const WorkflowKeys = {
  Readiness: 'results.readiness',
  Transition: 'results.transition',
} as const;

export type Transition = 'compute' | 'submit' | 'approve' | 'return' | 'publish' | 'withdraw' | 'reopen';

/** Spec 6.7.5/6.7.11: per-pupil completeness, the five counters, and why submission is blocked, if it is. */
export function useReadiness(armId: string, termId: string) {
  return useQuery({
    queryKey: [WorkflowKeys.Readiness, armId, termId],
    queryFn: ({ signal }) => apiGet('/api/v1/arms/{armId}/readiness', { termId }, { pathParams: { armId }, signal }),
    enabled: armId !== '' && termId !== '',
  });
}

// The screen only needs success or the refusal; each transition's own response shape is not read.
function send(transition: Transition, resultSetId: string, reason: string | undefined): Promise<unknown> {
  const pathParams = { pathParams: { resultSetId } };
  switch (transition) {
    case 'compute':
      return apiPost('/api/v1/result-sets/{resultSetId}/compute', undefined, pathParams);
    case 'submit':
      return apiPost('/api/v1/result-sets/{resultSetId}/submit', undefined, pathParams);
    case 'approve':
      return apiPost('/api/v1/result-sets/{resultSetId}/approve', undefined, pathParams);
    case 'return':
      return apiPost('/api/v1/result-sets/{resultSetId}/return', { resultSetId, reason: reason ?? '' }, pathParams);
    case 'publish':
      return apiPost('/api/v1/result-sets/{resultSetId}/publish', undefined, { pathParams: { resultSetId }, idempotencyKey: crypto.randomUUID() });
    case 'withdraw':
      return apiPost('/api/v1/result-sets/{resultSetId}/withdraw', { reason: reason ?? '' }, pathParams);
    case 'reopen':
      return apiPost('/api/v1/result-sets/{resultSetId}/reopen', undefined, pathParams);
  }
}

/** Runs one transition on the arm's result set; the server's refusal message is shown verbatim. */
export function useTransition(armId: string, termId: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationKey: [WorkflowKeys.Transition, armId, termId],
    mutationFn: ({ transition, resultSetId, reason }: { transition: Transition; resultSetId: string; reason?: string | undefined }) =>
      send(transition, resultSetId, reason),
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: [WorkflowKeys.Readiness, armId, termId] });
      void queryClient.invalidateQueries({ queryKey: [ResultsKeys.ScoreSheet, armId] });
      void queryClient.invalidateQueries({ queryKey: [RecordsKeys.Sheet] });
    },
  });
}

/** Spec 6.7.10: the arm's annual cumulative results, after Third Term is published. */
export function useComputeAnnual(armId: string) {
  return useMutation({
    mutationKey: [WorkflowKeys.Transition, 'annual', armId],
    mutationFn: () => apiPost('/api/v1/arms/{armId}/annual-results', undefined, { pathParams: { armId } }),
  });
}
