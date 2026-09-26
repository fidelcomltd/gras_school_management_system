import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import type { components } from '@/api/schema';
import { deleteRequest, getFile, postRequest, saveFile } from '@/lib/http';
import { PupilsKeys } from '../types';
import { RecordKeys, useSave, type PupilDocumentListDto, type PupilDocumentType } from './api';
import { assertFileSize, downscaleImage, PHOTO_MAX_BYTES, PHOTO_MAX_EDGE, SCAN_MAX_BYTES, SCAN_MAX_EDGE } from './downscale';

/** Photograph and document-scan calls (spec 6.5.4, 6.5.8, 9.6). */

type S = components['schemas'];

const multipart = (file: File) => {
  const form = new FormData();
  form.append('file', file);
  return form;
};

const dataUrl = (blob: Blob) =>
  new Promise<string>((resolve, reject) => {
    const reader = new FileReader();
    reader.onload = () => resolve(String(reader.result));
    reader.onerror = () => reject(reader.error ?? new Error('The photograph could not be read.'));
    reader.readAsDataURL(blob);
  });

/**
 * The current photograph as a `data:` URL, fetched like the settings logo rather than by a plain <img src>, so a refusal
 * surfaces as an error. Keyed by the upload time, so a replacement refetches. A data URL rather than an object URL: it is
 * garbage-collected with the query, where an object URL would pin every photograph viewed for the life of the tab.
 */
export const usePupilPhotoUrl = (pupilId: string, updatedAt: string | null | undefined, thumbnail = false) =>
  useQuery({
    queryKey: [RecordKeys.Photo, pupilId, thumbnail, updatedAt],
    queryFn: async () => dataUrl((await getFile(`/api/v1/pupils/${pupilId}/photo${thumbnail ? '/thumbnail' : ''}`, 'photo.jpg')).blob),
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
    postRequest<S['PupilPhotoDto'], FormData>(
      `/api/v1/pupils/${pupilId}/photo`,
      multipart(assertFileSize(await downscaleImage(file, PHOTO_MAX_EDGE), PHOTO_MAX_BYTES, 'photograph')),
      { headers: { 'Idempotency-Key': crypto.randomUUID() } },
    ),
  );

export const useRemovePhoto = (pupilId: string) =>
  usePhotoMutation(pupilId, () => deleteRequest(`/api/v1/pupils/${pupilId}/photo`));

/** Spec 6.5.8: one scan per checklist row. Attaching ticks an unticked row, so the list comes back whole. */
export const useUploadDocumentFile = (pupilId: string) =>
  useSave(RecordKeys.Documents, pupilId, async ({ documentType, file }: { documentType: PupilDocumentType; file: File }) =>
    postRequest<PupilDocumentListDto, FormData>(
      `/api/v1/pupils/${pupilId}/documents/${documentType}/file`,
      multipart(assertFileSize(await downscaleImage(file, SCAN_MAX_EDGE), SCAN_MAX_BYTES, 'file')),
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
