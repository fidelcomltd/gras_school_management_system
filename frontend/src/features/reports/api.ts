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
  termId: string;
  armId?: string;
  levelId?: string;
  top?: number;
  state?: string;
}

function fetchReport(key: ReportKey, params: ReportParams, signal: AbortSignal): Promise<ReportDto> {
  const { termId, armId, levelId, top, state } = params;
  switch (key) {
    case 'broadsheet':
      return apiGet('/api/v1/reports/broadsheet', { termId, ...(armId ? { armId } : {}) }, { signal });
    case 'merit-list':
      return apiGet(
        '/api/v1/reports/merit-list',
        { termId, ...(armId ? { armId } : {}), ...(levelId ? { levelId } : {}), ...(top ? { top } : {}) },
        { signal },
      );
    case 'result-entry-progress':
      return apiGet(
        '/api/v1/reports/result-entry-progress',
        { termId, ...(levelId ? { levelId } : {}), ...(state ? { state } : {}) },
        { signal },
      );
    case 'grade-distribution':
      return apiGet(
        '/api/v1/reports/grade-distribution',
        { termId, ...(armId ? { armId } : {}), ...(levelId ? { levelId } : {}) },
        { signal },
      );
    case 'subject-performance':
      return apiGet('/api/v1/reports/subject-performance', { termId, ...(levelId ? { levelId } : {}) }, { signal });
    case 'development-summary':
      return apiGet('/api/v1/reports/development-summary', { termId, ...(armId ? { armId } : {}) }, { signal });
    case 'fee-notice-audit':
      return apiGet('/api/v1/reports/fee-notice-audit', { termId, ...(levelId ? { levelId } : {}) }, { signal });
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
