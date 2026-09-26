import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { apiGet, apiPut } from '@/api/client';
import type { components } from '@/api/schema';

type S = components['schemas'];
export type PupilContactDto = S['PupilContactDto'];
export type PupilContactInput = S['PupilContactInput'];
export type ContactRole = S['ContactRole'];
export type PickupPersonInput = S['PickupPersonInput'];
export type BarredPersonsDto = S['BarredPersonsDto'];
export type PupilHealthDto = S['PupilHealthDto'];
export type SavePupilHealthCommand = S['SavePupilHealthCommand'];
/** The schema is the nullable property's, so `null` is stripped for the choices. */
export type BloodGroup = NonNullable<S['BloodGroup']>;
export type Genotype = NonNullable<S['Genotype']>;
export type PupilDocumentDto = S['PupilDocumentDto'];
export type PupilDocumentType = S['PupilDocumentType'];
export type PupilDocumentListDto = S['PupilDocumentListDto'];
export type AdmissionCompletenessDto = S['AdmissionCompletenessDto'];

/** Query keys for a pupil's admission-form sections (spec 6.5.5 to 6.5.8). */
export const RecordKeys = {
  Contacts: 'pupils.records.contacts',
  Pickup: 'pupils.records.pickup',
  Barred: 'pupils.records.barred',
  Health: 'pupils.records.health',
  Documents: 'pupils.records.documents',
  Completeness: 'pupils.records.completeness',
  Photo: 'pupils.records.photo',
} as const;

const options = { staleTime: 30_000, retry: false } as const;

/** Every save can change what an admission still lacks. */
export function useSave<TBody, TResult>(key: string, pupilId: string, save: (body: TBody) => Promise<TResult>) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationKey: [key, 'save', pupilId],
    mutationFn: save,
    onSuccess: (result) => {
      queryClient.setQueryData([key, pupilId], result);
      void queryClient.invalidateQueries({ queryKey: [RecordKeys.Completeness, pupilId] });
    },
  });
}

export const useContacts = (pupilId: string) =>
  useQuery({
    queryKey: [RecordKeys.Contacts, pupilId],
    queryFn: ({ signal }) => apiGet('/api/v1/pupils/{pupilId}/contacts', undefined, { pathParams: { pupilId }, signal }),
    ...options,
  });

export const useSaveContacts = (pupilId: string) =>
  useSave(RecordKeys.Contacts, pupilId, (contacts: PupilContactInput[]) =>
    apiPut('/api/v1/pupils/{pupilId}/contacts', { pupilId, contacts }, { pathParams: { pupilId } }),
  );

export const usePickupPersons = (pupilId: string) =>
  useQuery({
    queryKey: [RecordKeys.Pickup, pupilId],
    queryFn: ({ signal }) => apiGet('/api/v1/pupils/{pupilId}/pickup-persons', undefined, { pathParams: { pupilId }, signal }),
    ...options,
  });

export const useSavePickupPersons = (pupilId: string) =>
  useSave(RecordKeys.Pickup, pupilId, (persons: PickupPersonInput[]) =>
    apiPut('/api/v1/pupils/{pupilId}/pickup-persons', { pupilId, persons }, { pathParams: { pupilId } }),
  );

/** Safeguarding: the server audits every read, so it is fetched only when shown and never refetched on focus. */
export const useBarredPersons = (pupilId: string, enabled: boolean) =>
  useQuery({
    queryKey: [RecordKeys.Barred, pupilId],
    queryFn: ({ signal }) => apiGet('/api/v1/pupils/{pupilId}/barred-persons', undefined, { pathParams: { pupilId }, signal }),
    enabled,
    ...options,
    staleTime: Infinity,
    refetchOnWindowFocus: false,
  });

export const useSaveBarredPersons = (pupilId: string) =>
  useSave(RecordKeys.Barred, pupilId, (body: { hasBarredPersons: boolean; persons: { fullName: string; details: string | null }[] }) =>
    apiPut('/api/v1/pupils/{pupilId}/barred-persons', { pupilId, ...body }, { pathParams: { pupilId } }),
  );

/** Safeguarding: audited on every read, like the barred list. */
export const useHealth = (pupilId: string, enabled: boolean) =>
  useQuery({
    queryKey: [RecordKeys.Health, pupilId],
    queryFn: ({ signal }) => apiGet('/api/v1/pupils/{pupilId}/health', undefined, { pathParams: { pupilId }, signal }),
    enabled,
    ...options,
    staleTime: Infinity,
    refetchOnWindowFocus: false,
  });

export const useSaveHealth = (pupilId: string) =>
  useSave(RecordKeys.Health, pupilId, (body: Omit<SavePupilHealthCommand, 'pupilId'>) =>
    apiPut('/api/v1/pupils/{pupilId}/health', { pupilId, ...body }, { pathParams: { pupilId } }),
  );

export const useDocuments = (pupilId: string) =>
  useQuery({
    queryKey: [RecordKeys.Documents, pupilId],
    queryFn: ({ signal }) => apiGet('/api/v1/pupils/{pupilId}/documents', undefined, { pathParams: { pupilId }, signal }),
    ...options,
  });

export const useSaveDocument = (pupilId: string) =>
  useSave(
    RecordKeys.Documents,
    pupilId,
    ({ documentType, ...body }: { documentType: PupilDocumentType; received: boolean; receivedDate: string | null; remarks: string | null; otherLabel: string | null }) =>
      apiPut('/api/v1/pupils/{pupilId}/documents/{documentType}', body, { pathParams: { pupilId, documentType } }),
  );

export const useCompleteness = (pupilId: string, enabled: boolean) =>
  useQuery({
    queryKey: [RecordKeys.Completeness, pupilId],
    queryFn: ({ signal }) => apiGet('/api/v1/admissions/{id}/completeness', undefined, { pathParams: { id: pupilId }, signal }),
    enabled,
    ...options,
  });
