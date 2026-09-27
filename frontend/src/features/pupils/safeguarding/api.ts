import { useQuery } from '@tanstack/react-query';
import { apiGet } from '@/api/client';
import type { components } from '@/api/schema';
import { getFile, saveFile } from '@/lib/http';
import { PupilsKeys } from '../types';

export type SafeguardingSheetDto = components['schemas']['SafeguardingSheetDto'];
export type SafeguardingSheetRowDto = components['schemas']['SafeguardingSheetRowDto'];

/**
 * The class safeguarding sheet (spec 15 section 10.2). Every generation is audited server-side, so it is fetched only on an
 * explicit request (`enabled`), never refetched behind the reader's back, and each class is kept once fetched.
 */
export function useSafeguardingSheet(armId: string, enabled: boolean) {
  return useQuery({
    queryKey: [PupilsKeys.Safeguarding, armId],
    queryFn: ({ signal }) => apiGet('/api/v1/reports/safeguarding', { armId }, { signal }),
    enabled: enabled && armId !== '',
    staleTime: Infinity,
    refetchOnWindowFocus: false,
    refetchOnReconnect: false,
    retry: false,
  });
}

/** The printable PDF, saved under the server's own name. Also audited. */
export async function downloadSafeguardingSheet(armId: string): Promise<void> {
  saveFile(await getFile(`/api/v1/reports/safeguarding/pdf?armId=${encodeURIComponent(armId)}`, 'safeguarding-sheet.pdf'));
}
