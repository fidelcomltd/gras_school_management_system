import { useMutation, useQueryClient } from '@tanstack/react-query';
import { apiPut } from '@/api/client';
import type { components } from '@/api/schema';
import { SettingsKeys } from './types';

/** Rating scales, traits and development domains (spec 6.2.7, 6.2.13): each saved whole with its group version. */

type S = components['schemas'];

function useRatingsMutation<TBody>(key: string, send: (body: TBody) => Promise<unknown>) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationKey: [key],
    mutationFn: send,
    onSuccess: () => void queryClient.invalidateQueries({ queryKey: [SettingsKeys.Get] }),
  });
}

export const useUpdateRatingScales = () =>
  useRatingsMutation('settings.ratingScales', (body: S['UpdateRatingScalesCommand']) => apiPut('/api/v1/settings/rating-scales', body));

export const useUpdateTraits = () =>
  useRatingsMutation('settings.traits', (body: S['UpdateTraitsCommand']) => apiPut('/api/v1/settings/traits', body));

export const useUpdateDevelopmentDomains = () =>
  useRatingsMutation('settings.developmentDomains', (body: S['UpdateDevelopmentDomainsCommand']) =>
    apiPut('/api/v1/settings/development-domains', body),
  );
