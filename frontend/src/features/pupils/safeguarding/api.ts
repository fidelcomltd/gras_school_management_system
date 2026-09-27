import { useMutation } from '@tanstack/react-query';
import { apiGet } from '@/api/client';
import type { components } from '@/api/schema';
import { getFile, saveFile } from '@/lib/http';
import { PupilsKeys } from '../types';

export type SafeguardingSheetDto = components['schemas']['SafeguardingSheetDto'];
export type SafeguardingSheetRowDto = components['schemas']['SafeguardingSheetRowDto'];

/**
 * Generates the class safeguarding sheet (spec 15 section 10.2). A mutation, not a cached query: every generation is an
 * audited act on the server, so each Show sheet asks again (fresh data, a fresh audit record) and nothing is re-shown from
 * a cache without being recorded.
 */
export function useGenerateSafeguardingSheet() {
  return useMutation({
    mutationKey: [PupilsKeys.Safeguarding, 'generate'],
    mutationFn: (armId: string) => apiGet('/api/v1/reports/safeguarding', { armId }),
  });
}

/** The printable PDF, saved under the server's own name. Also audited. */
export function useDownloadSafeguardingSheet() {
  return useMutation({
    mutationKey: [PupilsKeys.Safeguarding, 'download'],
    mutationFn: async (armId: string) =>
      saveFile(await getFile(`/api/v1/reports/safeguarding/pdf?armId=${encodeURIComponent(armId)}`, 'safeguarding-sheet.pdf')),
  });
}
