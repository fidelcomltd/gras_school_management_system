import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { apiGet, apiPatch, apiPost, apiPut } from '@/api/client';
import type { components } from '@/api/schema';
import { getFile, postRequest } from '@/lib/http';
import { SettingsKeys } from './types';

/**
 * The settings groups beyond identity (spec 6.2): registration numbers, grading, assessment, result rules, and the
 * logo and signature images. Each group saves whole with the `expectedVersion` it was read at, so a concurrent change
 * is a 409 rather than a silent overwrite.
 */

type S = components['schemas'];

function useSettingsMutation<TBody>(key: string, send: (body: TBody) => Promise<unknown>, alsoInvalidate: string[] = []) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationKey: [key],
    mutationFn: send,
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: [SettingsKeys.Get] });
      for (const extra of alsoInvalidate) void queryClient.invalidateQueries({ queryKey: [extra] });
    },
  });
}

export const useUpdateRegNumber = () =>
  useSettingsMutation('settings.regNumber', (body: S['UpdateRegNumberCommand']) => apiPatch('/api/v1/settings/reg-number', body));

export const useUpdateAbbreviation = () =>
  useSettingsMutation('settings.abbreviation', (body: S['UpdateAbbreviationCommand']) => apiPatch('/api/v1/settings/abbreviation', body));

export const useUpdateGrading = () =>
  useSettingsMutation('settings.grading', (body: S['UpdateGradingCommand']) => apiPut('/api/v1/settings/grading', body));

export const useResetGrading = () =>
  useSettingsMutation('settings.gradingReset', (body: S['ResetGradingCommand']) => apiPost('/api/v1/settings/grading/reset', body));

export const useUpdateAssessment = () =>
  useSettingsMutation('settings.assessment', (body: S['UpdateAssessmentCommand']) => apiPut('/api/v1/settings/assessment', body));

export const useUpdateResultRules = () =>
  useSettingsMutation(
    'settings.resultRules',
    (body: S['UpdateResultRulesCommand']) => apiPut('/api/v1/settings/result-rules', body),
    [SettingsKeys.ResultRules],
  );

/** What the next registration number would look like with these choices. */
export function useRegNumberPreview(separator: string, serialWidth: number) {
  return useQuery({
    queryKey: [SettingsKeys.RegPreview, separator, serialWidth],
    queryFn: ({ signal }) => apiGet('/api/v1/settings/reg-number/preview', { separator, serialWidth }, { signal }),
    enabled: separator !== '' && serialWidth > 0,
  });
}

/** Gated `settings.view`: the result rules, which `GET /settings` does not carry. */
export function useResultRules() {
  return useQuery({
    queryKey: [SettingsKeys.ResultRules],
    queryFn: ({ signal }) => apiGet('/api/v1/settings/result-rules', undefined, { signal }),
  });
}

/** Spec 6.2.3: upload the logo or the head teacher's signature (PNG or JPEG, magic bytes checked server-side). */
export function useUploadSchoolImage(kind: 'logo' | 'signature') {
  const queryClient = useQueryClient();
  return useMutation({
    mutationKey: ['settings.upload', kind],
    mutationFn: (file: File) => {
      const form = new FormData();
      form.append('file', file);
      return postRequest<S['SchoolImageDto'], FormData>(`/api/v1/settings/identity/${kind}`, form, {
        headers: { 'Idempotency-Key': crypto.randomUUID() },
      });
    },
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: [SettingsKeys.Get] });
      void queryClient.invalidateQueries({ queryKey: [SettingsKeys.Image, kind] });
    },
  });
}

/** The current logo (200 px) or signature as an object URL; the image endpoints need the bearer token, so no plain <img src>. */
export function useSchoolImageUrl(kind: 'logo' | 'signature', uploadedAt: string | null) {
  return useQuery({
    queryKey: [SettingsKeys.Image, kind, uploadedAt],
    queryFn: async () => {
      const file = await getFile(kind === 'logo' ? '/api/v1/settings/identity/logo/200' : '/api/v1/settings/identity/signature', kind);
      return URL.createObjectURL(file.blob);
    },
    enabled: uploadedAt !== null,
    staleTime: Infinity,
  });
}
