import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { apiDelete, apiGet, apiPost, apiPut } from '@/api/client';
import type { components } from '@/api/schema';

/**
 * Hooks for the per-pupil records beside the marks (spec 6.7.7): trait and development ratings, attendance, and the two
 * remarks. Each sheet is fetched whole and saved whole with its `version`; staleTime is infinite while being edited.
 */

export const RecordsKeys = {
  Sheet: 'results.records.sheet',
  Templates: 'results.records.templates',
} as const;

export type RecordKind = 'trait-ratings' | 'development-ratings' | 'attendance' | 'class-teacher-remarks' | 'head-teacher-remarks';

type S = components['schemas'];
const sheetKey = (kind: RecordKind, armId: string, termId: string) => [RecordsKeys.Sheet, kind, armId, termId];
const sheetOptions = { staleTime: Infinity, refetchOnWindowFocus: false, retry: false } as const;

export function useTraitRatings(armId: string, termId: string, enabled: boolean) {
  return useQuery({
    queryKey: sheetKey('trait-ratings', armId, termId),
    queryFn: ({ signal }) => apiGet('/api/v1/arms/{armId}/trait-ratings', { termId }, { pathParams: { armId }, signal }),
    enabled: enabled && armId !== '' && termId !== '',
    ...sheetOptions,
  });
}

export function useDevelopmentRatings(armId: string, termId: string, enabled: boolean) {
  return useQuery({
    queryKey: sheetKey('development-ratings', armId, termId),
    queryFn: ({ signal }) => apiGet('/api/v1/arms/{armId}/development-ratings', { termId }, { pathParams: { armId }, signal }),
    enabled: enabled && armId !== '' && termId !== '',
    ...sheetOptions,
  });
}

export function useAttendance(armId: string, termId: string) {
  return useQuery({
    queryKey: sheetKey('attendance', armId, termId),
    queryFn: ({ signal }) => apiGet('/api/v1/arms/{armId}/attendance', { termId }, { pathParams: { armId }, signal }),
    enabled: armId !== '' && termId !== '',
    ...sheetOptions,
  });
}

export function useRemarks(kind: 'class-teacher-remarks' | 'head-teacher-remarks', armId: string, termId: string) {
  return useQuery({
    queryKey: sheetKey(kind, armId, termId),
    queryFn: ({ signal }) =>
      kind === 'class-teacher-remarks'
        ? apiGet('/api/v1/arms/{armId}/class-teacher-remarks', { termId }, { pathParams: { armId }, signal })
        : apiGet('/api/v1/arms/{armId}/head-teacher-remarks', { termId }, { pathParams: { armId }, signal }),
    enabled: armId !== '' && termId !== '',
    ...sheetOptions,
  });
}

/** Saves one sheet and writes the server's answer (with its new version) straight into the cache. */
function useSaveSheet<TBody, TResult>(kind: RecordKind, armId: string, termId: string, save: (body: TBody) => Promise<TResult>) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationKey: [RecordsKeys.Sheet, 'save', kind, armId, termId],
    mutationFn: save,
    onSuccess: (sheet) => queryClient.setQueryData(sheetKey(kind, armId, termId), sheet),
  });
}

export const useSaveTraitRatings = (armId: string, termId: string) =>
  useSaveSheet('trait-ratings', armId, termId, (body: S['SaveTraitRatingsCommand']) =>
    apiPut('/api/v1/arms/{armId}/trait-ratings', body, { pathParams: { armId } }),
  );

export const useSaveDevelopmentRatings = (armId: string, termId: string) =>
  useSaveSheet('development-ratings', armId, termId, (body: S['SaveDevelopmentRatingsCommand']) =>
    apiPut('/api/v1/arms/{armId}/development-ratings', body, { pathParams: { armId } }),
  );

export const useSaveAttendance = (armId: string, termId: string) =>
  useSaveSheet('attendance', armId, termId, (body: S['SaveAttendanceCommand']) =>
    apiPut('/api/v1/arms/{armId}/attendance', body, { pathParams: { armId } }),
  );

export const useSaveClassRemarks = (armId: string, termId: string) =>
  useSaveSheet('class-teacher-remarks', armId, termId, (body: S['SaveClassTeacherRemarksCommand']) =>
    apiPut('/api/v1/arms/{armId}/class-teacher-remarks', body, { pathParams: { armId } }),
  );

export const useSaveHeadRemarks = (armId: string, termId: string) =>
  useSaveSheet('head-teacher-remarks', armId, termId, (body: S['SaveHeadTeacherRemarksCommand']) =>
    apiPut('/api/v1/arms/{armId}/head-teacher-remarks', body, { pathParams: { armId } }),
  );

/** The school's saved remark phrases for one kind (spec 6.7.7, ruling H). */
export function useRemarkTemplates(kind: S['RemarkKind']) {
  return useQuery({
    queryKey: [RecordsKeys.Templates, kind],
    queryFn: ({ signal }) => apiGet('/api/v1/remark-templates', { kind }, { signal }),
  });
}

export function useCreateRemarkTemplate() {
  const queryClient = useQueryClient();
  return useMutation({
    mutationKey: [RecordsKeys.Templates, 'create'],
    mutationFn: (body: S['CreateRemarkTemplateCommand']) => apiPost('/api/v1/remark-templates', body, { idempotencyKey: crypto.randomUUID() }),
    onSuccess: (template) => void queryClient.invalidateQueries({ queryKey: [RecordsKeys.Templates, template.kind] }),
  });
}

export function useDeleteRemarkTemplate(kind: S['RemarkKind']) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationKey: [RecordsKeys.Templates, 'delete'],
    mutationFn: (id: string) => apiDelete('/api/v1/remark-templates/{id}', { pathParams: { id } }),
    onSuccess: () => void queryClient.invalidateQueries({ queryKey: [RecordsKeys.Templates, kind] }),
  });
}
