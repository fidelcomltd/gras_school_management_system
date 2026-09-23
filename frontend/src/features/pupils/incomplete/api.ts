import { useQuery } from '@tanstack/react-query';
import { apiGet } from '@/api/client';
import type { components } from '@/api/schema';

/** Spec 6.5.12: the incomplete-records report, the office's chasing list. */

type S = components['schemas'];
type ReportDto = S['IncompleteRecordsReportDto'];
export type IncompleteRecord = Omit<S['IncompleteRecordDto'], 'chasedPercent'> & { chasedPercent: number };
export type IncompleteRecordsCount = Omit<S['IncompleteRecordsCountDto'], 'count'> & { count: number };
export interface IncompleteRecordsReport {
  sessionName: string | null;
  pupilsChecked: number;
  counts: IncompleteRecordsCount[];
  pupils: IncompleteRecord[];
}

export const IncompleteRecordsKeys = { Report: 'reports.incompleteRecords' } as const;

/** The integers normalised once, here (the schema allows numeric strings). */
function toReport(dto: ReportDto): IncompleteRecordsReport {
  return {
    sessionName: dto.sessionName ?? null,
    pupilsChecked: Number(dto.pupilsChecked),
    counts: dto.counts.map((count) => ({ ...count, count: Number(count.count) })),
    pupils: dto.pupils.map((pupil) => ({ ...pupil, chasedPercent: Number(pupil.chasedPercent) })),
  };
}

/** Gated `report.view` in the handler; an arm-scoped grant gets only its arms. Fetched whole and filtered on screen. */
export function useIncompleteRecords() {
  return useQuery({
    queryKey: [IncompleteRecordsKeys.Report],
    queryFn: async ({ signal }) => toReport(await apiGet('/api/v1/reports/incomplete-records', {}, { signal })),
  });
}
