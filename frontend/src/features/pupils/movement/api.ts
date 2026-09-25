import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { apiGet, apiPost } from '@/api/client';
import type { components } from '@/api/schema';
import { PupilsKeys } from '../types';

type S = components['schemas'];
export type PupilEnrolmentHistoryDto = S['PupilEnrolmentHistoryDto'];
export type PupilMovementResultSetDto = S['PupilMovementResultSetDto'];
export type PupilMovementEffect = S['PupilMovementEffect'];
export type ChangePupilStatusCommand = S['ChangePupilStatusCommand'];
export type TransferPupilCommand = S['TransferPupilCommand'];

/** The capacity block with its ints normalised once (the generator types them `number | string`). */
export interface MovementCapacity {
  capacity: number;
  enrolledAfter: number;
  overCapacity: boolean;
  canOverride: boolean;
}

export type PupilMovementOutcome = Omit<S['PupilMovementOutcomeDto'], 'capacity'> & { capacity: MovementCapacity | null };

export const MovementKeys = {
  History: 'pupils.movement.history',
  ChangeStatus: 'pupils.movement.status',
  Transfer: 'pupils.movement.transfer',
} as const;

const HISTORY_PATH = '/api/v1/pupils/{id}/enrolments';
const STATUS_PATH = '/api/v1/pupils/{id}/status';
const TRANSFER_PATH = '/api/v1/pupils/{id}/transfer';

function normalise(outcome: S['PupilMovementOutcomeDto']): PupilMovementOutcome {
  const { capacity } = outcome;
  return {
    ...outcome,
    capacity: capacity
      ? { ...capacity, capacity: Number(capacity.capacity), enrolledAfter: Number(capacity.enrolledAfter) }
      : null,
  };
}

/** Gated `pupil.view`: current class, class history and status changes (spec 6.5.15). */
export function usePupilEnrolments(id: string) {
  return useQuery({
    queryKey: [MovementKeys.History, id],
    queryFn: ({ signal }) => apiGet(HISTORY_PATH, undefined, { pathParams: { id }, signal }),
    staleTime: 30_000,
  });
}

/** After a real change: the pupil, the register, and this history are all stale. */
function useOnMoved(id: string) {
  const queryClient = useQueryClient();
  return (outcome: PupilMovementOutcome) => {
    if (outcome.dryRun) return;
    queryClient.setQueryData([PupilsKeys.Detail, id], outcome.pupil);
    void queryClient.invalidateQueries({ queryKey: [PupilsKeys.List] });
    void queryClient.invalidateQueries({ queryKey: [MovementKeys.History, id] });
  };
}

/** Gated `pupil.status.update` (spec 6.5.14). `Idempotency-Key` REQUIRED: the caller owns it, so a retry reuses it. */
export function useChangePupilStatus(id: string) {
  const onMoved = useOnMoved(id);
  return useMutation({
    mutationKey: [MovementKeys.ChangeStatus, id],
    mutationFn: async ({ idempotencyKey, ...body }: ChangePupilStatusCommand & { idempotencyKey: string }) =>
      normalise(await apiPost(STATUS_PATH, body, { pathParams: { id }, idempotencyKey })),
    onSuccess: onMoved,
  });
}

/** Gated `pupil.transfer` (spec 6.5.17). `Idempotency-Key` REQUIRED, as above. */
export function useTransferPupil(id: string) {
  const onMoved = useOnMoved(id);
  return useMutation({
    mutationKey: [MovementKeys.Transfer, id],
    mutationFn: async ({ idempotencyKey, ...body }: TransferPupilCommand & { idempotencyKey: string }) =>
      normalise(await apiPost(TRANSFER_PATH, body, { pathParams: { id }, idempotencyKey })),
    onSuccess: onMoved,
  });
}

/** Whether the dry run allows the real call to go ahead. */
export function canCommit(outcome: PupilMovementOutcome): boolean {
  return (
    !outcome.resultSets.some((set) => set.effect === 'Blocks') &&
    !(outcome.capacity?.overCapacity && !outcome.capacity.canOverride)
  );
}
