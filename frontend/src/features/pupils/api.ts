import { useInfiniteQuery, useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { apiGet, apiPatch, apiPost } from '@/api/client';
import { RecordKeys } from './records/api';
import {
  PupilsKeys,
  type CorrectRegistrationNumberCommand,
  type CreatePupilCommand,
  type PupilsFilters,
  type UpdatePupilBiographicalCommand,
} from './types';

/** One hook per endpoint, per CONVENTIONS.md §4 / `src/features/README.md`. */

const PUPILS_PATH = '/api/v1/pupils';
const PUPIL_PATH = '/api/v1/pupils/{id}';
const DUPLICATES_PATH = '/api/v1/pupils/duplicates';
const REG_NUMBER_PATH = '/api/v1/pupils/{id}/registration-number';

/** Cursor-paginated (spec 6.5.12). Search matches name or registration number server-side. */
export function usePupils(filters: PupilsFilters) {
  const search = filters.search.trim();
  return useInfiniteQuery({
    queryKey: [PupilsKeys.List, search, filters.status],
    queryFn: ({ pageParam, signal }) =>
      apiGet(
        PUPILS_PATH,
        {
          ...(pageParam !== undefined ? { cursor: pageParam } : {}),
          ...(search ? { search } : {}),
          ...(filters.status ? { status: filters.status } : {}),
        },
        { signal },
      ),
    initialPageParam: undefined as string | undefined,
    getNextPageParam: (lastPage) => lastPage.nextCursor ?? undefined,
  });
}

/** Gated `pupil.view`. */
export function usePupil(id: string) {
  return useQuery({
    queryKey: [PupilsKeys.Detail, id],
    queryFn: ({ signal }) => apiGet(PUPIL_PATH, undefined, { pathParams: { id }, signal }),
  });
}

/** Spec 6.5.5: possible duplicates by name and date of birth, checked before creating. */
export function fetchDuplicates(surname: string, firstName: string, dateOfBirth: string) {
  return apiGet(DUPLICATES_PATH, { surname, firstName, dateOfBirth });
}

/** Gated `pupil.create`. Always creates a Pending pupil. `Idempotency-Key` REQUIRED, fresh per submit. */
export function useCreatePupil() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationKey: [PupilsKeys.Create],
    mutationFn: (payload: CreatePupilCommand) => apiPost(PUPILS_PATH, payload, { idempotencyKey: crypto.randomUUID() }),
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: [PupilsKeys.List] });
    },
  });
}

/** Gated `pupil.update`. */
export function useUpdatePupil(id: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationKey: [PupilsKeys.Update, id],
    mutationFn: (payload: UpdatePupilBiographicalCommand) =>
      apiPatch(PUPIL_PATH, payload, { pathParams: { id }, idempotencyKey: crypto.randomUUID() }),
    onSuccess: (pupil) => {
      queryClient.setQueryData([PupilsKeys.Detail, id], pupil);
      void queryClient.invalidateQueries({ queryKey: [PupilsKeys.List] });
      // Previous school and other information are chased items (spec 6.5.12).
      void queryClient.invalidateQueries({ queryKey: [RecordKeys.Completeness, id] });
    },
  });
}

/** Gated `pupil.regnumber.correct`: a reasoned, audited correction. */
export function useCorrectRegistrationNumber(id: string) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationKey: [PupilsKeys.CorrectNumber, id],
    mutationFn: (payload: CorrectRegistrationNumberCommand) =>
      apiPost(REG_NUMBER_PATH, payload, { pathParams: { id }, idempotencyKey: crypto.randomUUID() }),
    onSuccess: (pupil) => {
      queryClient.setQueryData([PupilsKeys.Detail, id], pupil);
      void queryClient.invalidateQueries({ queryKey: [PupilsKeys.List] });
    },
  });
}
