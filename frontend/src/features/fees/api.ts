import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { apiGet, apiPut } from '@/api/client';
import { FeesKeys, type SaveFeeNoticeGridCommand, type SaveOutstandingFeesCommand } from './types';

const GRID_PATH = '/api/v1/fee-notices';
const OUTSTANDING_PATH = '/api/v1/arms/{armId}/outstanding-fees';

/** Gated `fee.manage`. The section's lines against its levels, for the term whose sheets print them. */
export function useFeeNoticeGrid(sectionId: string, termId: string) {
  return useQuery({
    queryKey: [FeesKeys.Grid, sectionId, termId],
    queryFn: ({ signal }) => apiGet(GRID_PATH, { sectionId, termId }, { signal }),
    enabled: sectionId !== '' && termId !== '',
  });
}

/** "Copy from previous term": the earlier term's grid, read once through the cache. */
export function useFetchFeeNoticeGrid() {
  const queryClient = useQueryClient();
  return (sectionId: string, termId: string) =>
    queryClient.fetchQuery({
      queryKey: [FeesKeys.Grid, sectionId, termId],
      queryFn: ({ signal }) => apiGet(GRID_PATH, { sectionId, termId }, { signal }),
    });
}

/** Gated `fee.manage`. The whole grid in one save. */
export function useSaveFeeNoticeGrid() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationKey: [FeesKeys.SaveGrid],
    mutationFn: (payload: SaveFeeNoticeGridCommand) => apiPut(GRID_PATH, payload),
    onSuccess: (grid) => {
      // The saved grid (with its new ids and version) straight into the cache, so the next save is never made from stale ids;
      // labels are section-wide, so the section's other terms are refetched too.
      queryClient.setQueryData([FeesKeys.Grid, grid.sectionId, grid.termId], grid);
      void queryClient.invalidateQueries({
        queryKey: [FeesKeys.Grid, grid.sectionId],
        predicate: (query) => query.queryKey[2] !== grid.termId,
      });
    },
  });
}

/** Gated `fee.manage`. One arm's pupils against their outstanding figures for a term. */
export function useOutstandingFees(armId: string, termId: string) {
  return useQuery({
    queryKey: [FeesKeys.Outstanding, armId, termId],
    queryFn: ({ signal }) => apiGet(OUTSTANDING_PATH, { termId }, { pathParams: { armId }, signal }),
    enabled: armId !== '' && termId !== '',
  });
}

/** Gated `fee.manage`. Refused with 409 once the arm's results for the term are published. */
export function useSaveOutstandingFees() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationKey: [FeesKeys.SaveOutstanding],
    mutationFn: (payload: SaveOutstandingFeesCommand) => apiPut(OUTSTANDING_PATH, payload, { pathParams: { armId: payload.armId } }),
    onSuccess: (sheet) => queryClient.setQueryData([FeesKeys.Outstanding, sheet.armId, sheet.termId], sheet),
  });
}
