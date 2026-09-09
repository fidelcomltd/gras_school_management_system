import { useQueries } from '@tanstack/react-query';
import { apiGet } from '@/api/client';
import { AdminsKeys } from '@/features/admins/types';

const ADMIN_PATH = '/api/v1/admins/{id}';

/**
 * Resolves `formTeacherAdminId` → `staffName` for the arms currently on
 * screen (TASK-0045's "known backend gap": `ArmDto` carries only the opaque
 * id, spec 6.4.5 wants the name).
 *
 * Deliberately reuses `features/admins/api.ts`'s already-gated `admin.view`
 * lookup (`GET /admins/{id}`, same query key — `AdminsKeys.Detail` — so the
 * cache is shared with the Admins feature) rather than inventing a second
 * lookup surface, and is bounded to the DISTINCT ids actually visible on the
 * current page — never the full admin collection. `enabled` gates the whole
 * thing on the caller holding `admin.view`: without it these calls would
 * just 403 for no benefit, so the caller sees the honest "not visible to
 * you" gap instead (`FormTeacherLabel`) rather than a wall of failed
 * requests.
 */
export function useFormTeacherNames(
  ids: (string | null)[],
  enabled: boolean,
): Record<string, string | undefined> {
  const unique = Array.from(new Set(ids.filter((id): id is string => id !== null)));

  const results = useQueries({
    queries: unique.map((id) => ({
      queryKey: [AdminsKeys.Detail, id],
      queryFn: () => apiGet(ADMIN_PATH, undefined, { pathParams: { id } }),
      enabled,
      retry: false,
    })),
  });

  const names: Record<string, string | undefined> = {};
  unique.forEach((id, index) => {
    names[id] = results[index]?.data?.staffName;
  });
  return names;
}
