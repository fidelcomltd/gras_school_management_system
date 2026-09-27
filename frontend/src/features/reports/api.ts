import { useMutation, useQuery } from '@tanstack/react-query';
import { apiGet } from '@/api/client';
import { getFile, saveFile } from '@/lib/http';
import type { ReportDto, ReportKey } from './definitions';

export const ReportsKeys = {
  Report: 'reports.report',
  Export: 'reports.export',
} as const;

/** Every filter any report takes; each report sends only its own. */
export interface ReportParams {
  termId?: string;
  sessionId?: string;
  pupilId?: string;
  armId?: string;
  levelId?: string;
  top?: number;
  state?: string;
  outcome?: string;
  status?: string;
  sex?: string;
  documentType?: string;
  minDays?: number;
}

/** Only the filters that are set: an absent one is omitted, never sent blank. */
function set<T extends object>(values: T): { [K in keyof T]?: Exclude<T[K], undefined | ''> } {
  return Object.fromEntries(Object.entries(values).filter(([, value]) => value !== undefined && value !== '')) as {
    [K in keyof T]?: Exclude<T[K], undefined | ''>;
  };
}

function fetchReport(key: ReportKey, params: ReportParams, signal: AbortSignal): Promise<ReportDto> {
  const { termId = '', sessionId = '', pupilId = '', armId, levelId, top, state, outcome, status, sex, documentType, minDays } = params;
  switch (key) {
    case 'broadsheet':
      return apiGet('/api/v1/reports/broadsheet', { termId, ...set({ armId }) }, { signal });
    case 'merit-list':
      return apiGet('/api/v1/reports/merit-list', { termId, ...set({ armId, levelId, top }) }, { signal });
    case 'result-entry-progress':
      return apiGet('/api/v1/reports/result-entry-progress', { termId, ...set({ levelId, state }) }, { signal });
    case 'grade-distribution':
      return apiGet('/api/v1/reports/grade-distribution', { termId, ...set({ armId, levelId }) }, { signal });
    case 'subject-performance':
      return apiGet('/api/v1/reports/subject-performance', { termId, ...set({ levelId }) }, { signal });
    case 'development-summary':
      return apiGet('/api/v1/reports/development-summary', { termId, ...set({ armId }) }, { signal });
    case 'fee-notice-audit':
      return apiGet('/api/v1/reports/fee-notice-audit', { termId, ...set({ levelId }) }, { signal });
    case 'annual-cumulative':
      return apiGet('/api/v1/reports/annual-cumulative', { sessionId, ...set({ armId, levelId }) }, { signal });
    case 'promotion-list':
      return apiGet('/api/v1/reports/promotion-list', { sessionId, ...set({ levelId, outcome }) }, { signal });
    case 'pupil-record':
      return apiGet('/api/v1/reports/pupil-record', { pupilId }, { signal });
    case 'nominal-roll':
      return apiGet('/api/v1/reports/nominal-roll', { sessionId, ...set({ armId, levelId, status, sex }) }, { signal });
    case 'enrolment-summary':
      return apiGet('/api/v1/reports/enrolment-summary', { sessionId, ...set({ status }) }, { signal });
    case 'guardian-contacts':
      return apiGet('/api/v1/reports/guardian-contacts', { sessionId, ...set({ armId }) }, { signal });
    case 'outstanding-documents':
      return apiGet('/api/v1/reports/outstanding-documents', { sessionId, ...set({ armId, levelId, documentType }) }, { signal });
    case 'admissions-pipeline':
      return apiGet('/api/v1/reports/admissions-pipeline', set({ levelId, minDays }), { signal });
  }
}

/** The report for these filters; `ready` is false until the screen has every filter it needs. */
export function useReport(key: ReportKey, params: ReportParams, ready: boolean) {
  return useQuery({
    queryKey: [ReportsKeys.Report, key, params],
    queryFn: ({ signal }) => fetchReport(key, params, signal),
    enabled: ready,
  });
}

/** CSV or PDF under the server's own name. Needs `report.export`; every export is audited on the server. */
export function useExportReport(key: ReportKey) {
  return useMutation({
    mutationKey: [ReportsKeys.Export, key],
    mutationFn: async ({ params, format }: { params: ReportParams; format: 'csv' | 'pdf' }) => {
      const query = new URLSearchParams({ format });
      for (const [name, value] of Object.entries(params)) {
        if (value !== undefined && value !== '') query.set(name, String(value));
      }
      saveFile(await getFile(`/api/v1/reports/${key}/export?${query.toString()}`, `${key}.${format}`));
    },
  });
}
