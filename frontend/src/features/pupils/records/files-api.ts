import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import type { components } from '@/api/schema';
import { deleteRequest, getFile, postRequest, saveFile } from '@/lib/http';
import { PupilsKeys } from '../types';
import { RecordKeys, useSave, type PupilDocumentListDto, type PupilDocumentType } from './api';
import { downscaleImage, PHOTO_MAX_EDGE, SCAN_MAX_EDGE } from './downscale';

/** Photograph and document-scan calls (spec 6.5.4, 6.5.8, 9.6). */

type S = components['schemas'];

const multipart = (file: File) => {
  const form = new FormData();
  form.append('file', file);
  return form;
};

/**
 * The current photograph as an object URL, fetched like the settings logo rather than by a plain <img src>, so a refusal
 * surfaces as an error. Keyed by the upload time, so a replacement refetches.
 */
export const usePupilPhotoUrl = (pupilId: string, updatedAt: string | null | undefined, thumbnail = false) =>
  useQuery({
    queryKey: [RecordKeys.Photo, pupilId, thumbnail, updatedAt],
    queryFn: async () => {
      const file = await getFile(`/api/v1/pupils/${pupilId}/photo${thumbnail ? '/thumbnail' : ''}`, 'photo.jpg');
      return URL.createObjectURL(file.blob);
    },
    enabled: !!updatedAt,
    staleTime: Infinity,
    retry: false,
  });

/** The photograph changes the pupil (its `photoUpdatedAtUtc`) and what the record still lacks. */
function usePhotoMutation<TArg>(pupilId: string, run: (arg: TArg) => Promise<unknown>) {
  const queryClient = useQueryClient();
  return useMutation({
    mutationKey: [RecordKeys.Photo, 'save', pupilId],
    mutationFn: run,
    onSuccess: () => {
      void queryClient.invalidateQueries({ queryKey: [PupilsKeys.Detail, pupilId] });
      void queryClient.invalidateQueries({ queryKey: [PupilsKeys.List] });
      void queryClient.invalidateQueries({ queryKey: [RecordKeys.Completeness, pupilId] });
    },
  });
}

/** Spec 6.5.4: downscaled to 800 px here, then cropped, re-encoded and stripped by the server. */
export const useUploadPhoto = (pupilId: string) =>
  usePhotoMutation(pupilId, async (file: File) =>
    postRequest<S['PupilPhotoDto'], FormData>(`/api/v1/pupils/${pupilId}/photo`, multipart(await downscaleImage(file, PHOTO_MAX_EDGE)), {
      headers: { 'Idempotency-Key': crypto.randomUUID() },
    }),
  );

export const useRemovePhoto = (pupilId: string) =>
  usePhotoMutation(pupilId, () => deleteRequest(`/api/v1/pupils/${pupilId}/photo`));

/** Spec 6.5.8: one scan per checklist row. Attaching ticks an unticked row, so the list comes back whole. */
export const useUploadDocumentFile = (pupilId: string) =>
  useSave(RecordKeys.Documents, pupilId, async ({ documentType, file }: { documentType: PupilDocumentType; file: File }) =>
    postRequest<PupilDocumentListDto, FormData>(
      `/api/v1/pupils/${pupilId}/documents/${documentType}/file`,
      multipart(await downscaleImage(file, SCAN_MAX_EDGE)),
      { headers: { 'Idempotency-Key': crypto.randomUUID() } },
    ),
  );

export const useRemoveDocumentFile = (pupilId: string) =>
  useSave(RecordKeys.Documents, pupilId, (documentType: PupilDocumentType) =>
    deleteRequest<PupilDocumentListDto>(`/api/v1/pupils/${pupilId}/documents/${documentType}/file`),
  );

/** Downloads a scan under the server's own name for it. */
export async function downloadDocumentFile(pupilId: string, documentType: PupilDocumentType): Promise<void> {
  saveFile(await getFile(`/api/v1/pupils/${pupilId}/documents/${documentType}/file`, 'document'));
}
