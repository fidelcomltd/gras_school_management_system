import { useInfiniteQuery, useMutation } from '@tanstack/react-query';
import { apiGet } from '@/api/client';
import type { components } from '@/api/schema';
import { getFile, saveFile } from '@/lib/http';

/** One hook per endpoint, per CONVENTIONS.md §4 / `src/features/README.md`. */

export const AuditKeys = { List: 'audit.list', Export: 'audit.export' } as const;

export type AuditEventDto = components['schemas']['AuditEventDto'];
export type AuditOutcome = components['schemas']['AuditOutcome'];

export interface AuditFilters {
  from: string; // yyyy-mm-dd, Lagos date
  to: string;
  action: string;
  entityType: string;
  outcome: AuditOutcome | '';
}

/** Lagos dates to the UTC instants the API filters on (the school is on WAT, UTC+1, all year). */
function toQuery(filters: AuditFilters): Record<string, string> {
  const query: Record<string, string> = {};
  if (filters.from) query['fromUtc'] = new Date(`${filters.from}T00:00:00+01:00`).toISOString();
  if (filters.to) query['toUtc'] = new Date(`${filters.to}T23:59:59.999+01:00`).toISOString();
  if (filters.action.trim()) query['action'] = filters.action.trim();
  if (filters.entityType.trim()) query['entityType'] = filters.entityType.trim();
  if (filters.outcome) query['outcome'] = filters.outcome;
  return query;
}

/** Gated `audit.view` (spec 6.1.10): newest first, cursor-paginated. */
export function useAuditEvents(filters: AuditFilters) {
  return useInfiniteQuery({
    queryKey: [AuditKeys.List, filters],
    queryFn: ({ pageParam, signal }) =>
      apiGet('/api/v1/audit-events', { ...toQuery(filters), ...(pageParam !== undefined ? { cursor: pageParam } : {}) }, { signal }),
    initialPageParam: undefined as string | undefined,
    getNextPageParam: (lastPage) => lastPage.nextCursor ?? undefined,
  });
}

/** Gated `audit.export`: the filtered events as CSV, saved through the browser. */
export function useExportAudit() {
  return useMutation({
    mutationKey: [AuditKeys.Export],
    mutationFn: async (filters: AuditFilters) => {
      const query = new URLSearchParams(toQuery(filters)).toString();
      saveFile(await getFile(`/api/v1/audit-events/export${query ? `?${query}` : ''}`, 'audit-log.csv'));
    },
  });
}
