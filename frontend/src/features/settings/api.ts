import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { apiGet, apiPatch } from '@/api/client';
import { SettingsKeys, type UpdateSchoolIdentityCommand } from './types';

/** One hook per endpoint, per CONVENTIONS.md §4 / `src/features/README.md`. */

const SETTINGS_PATH = '/api/v1/settings';
const IDENTITY_PATH = '/api/v1/settings/identity';

/** Gated `settings.view` server-side (403, not a shape this hook needs to know). */
export function useSettings() {
  return useQuery({
    queryKey: [SettingsKeys.Get],
    queryFn: ({ signal }) => apiGet(SETTINGS_PATH, undefined, { signal }),
  });
}

/** Gated `settings.identity.update` server-side. `Idempotency-Key` is optional here. */
export function useUpdateSchoolIdentity() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationKey: [SettingsKeys.UpdateIdentity],
    mutationFn: (payload: UpdateSchoolIdentityCommand) => apiPatch(IDENTITY_PATH, payload),
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: [SettingsKeys.Get] });
    },
  });
}
