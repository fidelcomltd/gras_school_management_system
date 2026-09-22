import { useInfiniteQuery, useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { apiGet, apiPost } from '@/api/client';
import { getFile, saveFile } from '@/lib/http';
import { PinsKeys, type GeneratePinBatchCommand } from './types';

/** One hook per endpoint, per CONVENTIONS.md §4 / `src/features/README.md`. */

/** Gated `pin.view`: a session's batches, newest first. */
export function usePinBatches(sessionId: string) {
  return useInfiniteQuery({
    queryKey: [PinsKeys.Batches, sessionId],
    queryFn: ({ pageParam, signal }) =>
      apiGet('/api/v1/pin-batches', { sessionId, ...(pageParam !== undefined ? { cursor: pageParam } : {}) }, { signal }),
    initialPageParam: undefined as string | undefined,
    getNextPageParam: (lastPage) => lastPage.nextCursor ?? undefined,
    enabled: sessionId !== '',
  });
}

/** Gated `pin.view`: the batch and each pin's prefix and state (never a pin value). */
export function usePinBatch(batchId: string) {
  return useQuery({
    queryKey: [PinsKeys.Batch, batchId],
    queryFn: ({ signal }) => apiGet('/api/v1/pin-batches/{batchId}', undefined, { pathParams: { batchId }, signal }),
  });
}

/** Gated `pin.generate` (spec 6.8.9). `Idempotency-Key` REQUIRED: generating twice would print two batches. */
export function useGeneratePinBatch() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationKey: [PinsKeys.Generate],
    mutationFn: (body: GeneratePinBatchCommand) => apiPost('/api/v1/pin-batches', body, { idempotencyKey: crypto.randomUUID() }),
    onSuccess: (batch) => void queryClient.invalidateQueries({ queryKey: [PinsKeys.Batches, batch.sessionId] }),
  });
}

type BatchAction = { kind: 'mark-distributed' } | { kind: 'revoke-batch'; reason: string } | { kind: 'revoke-pin' | 'reinstate-pin'; pinId: string; reason: string };

/** The batch's and its pins' state changes (spec 6.8.9–6.8.11); each refreshes the batch. */
export function usePinAction(batchId: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationKey: [PinsKeys.Action, batchId],
    mutationFn: (action: BatchAction): Promise<unknown> => {
      switch (action.kind) {
        case 'mark-distributed':
          return apiPost('/api/v1/pin-batches/{batchId}/mark-distributed', undefined, { pathParams: { batchId } });
        case 'revoke-batch':
          return apiPost('/api/v1/pin-batches/{batchId}/revoke', { reason: action.reason }, { pathParams: { batchId } });
        case 'revoke-pin':
          return apiPost('/api/v1/pins/{pinId}/revoke', { reason: action.reason }, { pathParams: { pinId: action.pinId } });
        case 'reinstate-pin':
          return apiPost('/api/v1/pins/{pinId}/reinstate', { reason: action.reason }, { pathParams: { pinId: action.pinId } });
      }
    },
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: [PinsKeys.Batch, batchId] });
      void queryClient.invalidateQueries({ queryKey: [PinsKeys.Batches] });
    },
  });
}

/**
 * Gated `pin.print`: the slips (the only place pin values appear) or the distribution list, as a PDF. Fetched and
 * saved, never opened by navigation, because printing writes (the batch becomes Printed; 6.8.9 step 6).
 */
export function usePinDownload(batchId: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationKey: [PinsKeys.Download, batchId],
    mutationFn: async (what: 'print' | 'distribution-list') =>
      saveFile(await getFile(`/api/v1/pin-batches/${batchId}/${what}`, what === 'print' ? 'pin-slips.pdf' : 'distribution-list.pdf')),
    onSuccess: () => void queryClient.invalidateQueries({ queryKey: [PinsKeys.Batch, batchId] }),
  });
}
