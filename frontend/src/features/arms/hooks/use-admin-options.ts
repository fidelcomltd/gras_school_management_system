import { useQuery } from '@tanstack/react-query';
import { apiGet } from '@/api/client';

/**
 * Options for `EditArmDialog`'s form-teacher picker — active admins, first
 * page (`pageSize` 100, plenty for a single school's staff roster). Reuses
 * the already-gated `admin.view` `GET /admins` list rather than a new
 * lookup surface. `features/admins/api.ts`'s own `useAdmins` is an infinite
 * query; this needs one plain page, so it calls `apiGet` directly under its
 * own cache key rather than mixing infinite/non-infinite shapes under the
 * same entry.
 *
 * `enabled` gates this on the caller both needing the picker (holding
 * `arm.formteacher.assign`) AND being able to browse names at all (holding
 * `admin.view`) — TASK-0045's "never a bare GUID in front of a user" applies
 * to the INPUT side too, not only the display side: a caller who cannot
 * browse admin names gets `EditArmDialog`'s honest fallback (clear-only,
 * no free-text id field) instead of a wall of failed requests.
 */
export function useAdminOptions(enabled: boolean) {
  return useQuery({
    queryKey: ['arms.formTeacherOptions'],
    queryFn: ({ signal }) => apiGet('/api/v1/admins', { status: 'Active', pageSize: 100 }, { signal }),
    enabled,
  });
}
