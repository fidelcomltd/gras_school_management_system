import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { apiGet, apiPost } from '@/api/client';
import { PromotionKeys, SessionsKeys, type CommitPromotionCommand } from './types';

const PREVIEW_PATH = '/api/v1/sessions/{sessionId}/promotion/preview';
const COMMIT_PATH = '/api/v1/sessions/{sessionId}/promotion';
const REVERSE_PATH = '/api/v1/promotion-batches/{batchId}/reverse';

/** Gated `promotion.run`. The review screen's rows, destination arms and blockers (spec 6.3.7). */
export function usePromotionPreview(sessionId: string) {
  return useQuery({
    queryKey: [PromotionKeys.Preview, sessionId],
    queryFn: ({ signal }) => apiGet(PREVIEW_PATH, {}, { pathParams: { sessionId }, signal }),
    enabled: sessionId !== '',
  });
}

/** Gated `promotion.run`. One transaction for the whole school; `Idempotency-Key` REQUIRED, fresh per submit. */
export function useCommitPromotion(sessionId: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationKey: [PromotionKeys.Commit, sessionId],
    mutationFn: (payload: CommitPromotionCommand) =>
      apiPost(COMMIT_PATH, payload, { pathParams: { sessionId }, idempotencyKey: crypto.randomUUID() }),
    onSuccess: () => invalidateAfterPromotion(queryClient),
  });
}

/** Gated `promotion.reverse` (Super Admin). A reason of 10 to 500 characters. */
export function useReversePromotion() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationKey: [PromotionKeys.Reverse],
    mutationFn: ({ batchId, reason }: { batchId: string; reason: string }) =>
      apiPost(REVERSE_PATH, { batchId, reason }, { pathParams: { batchId } }),
    onSuccess: () => invalidateAfterPromotion(queryClient),
  });
}

// Promotion moves every pupil: whatever lists enrolments, arms or pupils is stale afterwards.
function invalidateAfterPromotion(queryClient: ReturnType<typeof useQueryClient>) {
  void queryClient.invalidateQueries({ queryKey: [PromotionKeys.Preview] });
  void queryClient.invalidateQueries({ queryKey: [SessionsKeys.Detail] });
  void queryClient.invalidateQueries({ queryKey: [SessionsKeys.List] });
  void queryClient.invalidateQueries({ predicate: (query) => String(query.queryKey[0]).startsWith('pupils.') || String(query.queryKey[0]).startsWith('arms.') });
}
