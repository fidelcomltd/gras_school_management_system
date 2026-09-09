import { Link } from 'react-router';
import { paths } from '@/app/router/paths';
import { Button } from '@/components/ui/button';
import { useMe } from '@/features/auth/api';
import { hasPrivilege } from '@/lib/auth/auth-session';
import { ApiError } from '@/lib/http';
import { useArms, type ArmsFilters } from '../api';
import { useFormTeacherNames } from '../hooks/use-form-teacher-names';
import { FormTeacherLabel } from './form-teacher-label';

/**
 * The arms themselves (spec 6.4.5). Four required states (CONVENTIONS.md
 * §11) for the `GET /arms` fetch, mirroring `SessionsListScreen`'s shape.
 *
 * Rendering rules the task card calls out by name:
 * - `displayName` is rendered VERBATIM from the server, never composed from
 *   `classLevel` + `label` — spec 6.4.3 mandates one implementation
 *   (`ArmDisplayName`), and a client-side recomposition is exactly the
 *   failure mode a result sheet reading "Primary 1 A" instead of the
 *   server's own "Primary 1A" would be.
 * - Items render in the SERVER's order — no client-side `.sort()` — because
 *   `ListArms` already promises level-chain order then label collated
 *   naturally (Primary 5 between 4 and 6, Primary 10 after Primary 2, never
 *   alphabetical).
 * - Only `capacity` is shown, never `enrolled / capacity`: enrolments do not
 *   exist in this contract yet (Phase 2), and TASK-0035's standing rule is
 *   to never fabricate a `0` for something not actually tracked.
 * - `status` renders as whatever string the server sent, unrecognised
 *   members included (§8) — no lookup table that could throw on a miss.
 */
export function ArmList({ filters }: { filters: ArmsFilters }) {
  const arms = useArms(filters);
  const me = useMe();
  const canUpdate = !!me.data && hasPrivilege(me.data, 'arm.update');
  const canResolveFormTeacherNames = !!me.data && hasPrivilege(me.data, 'admin.view');

  const items = arms.data?.pages.flatMap((page) => page.items) ?? [];
  const formTeacherNames = useFormTeacherNames(
    items.map((arm) => arm.formTeacherAdminId),
    canResolveFormTeacherNames,
  );

  if (arms.isPending) {
    return <output className="text-sm text-muted-foreground">Loading arms…</output>;
  }

  if (arms.isError) {
    if (arms.error instanceof ApiError && arms.error.kind === 'unauthorized') return null;
    return (
      <div role="alert" className="flex flex-col items-start gap-3">
        <p className="text-sm text-destructive">{arms.error.message}</p>
        <Button variant="outline" size="sm" onClick={() => void arms.refetch()}>
          Try again
        </Button>
      </div>
    );
  }

  if (items.length === 0) {
    return <p className="text-sm text-muted-foreground">No arms found.</p>;
  }

  return (
    <div className="flex flex-col gap-4">
      <ul aria-label="Arms" className="flex flex-col gap-2">
        {items.map((arm) => (
          <li
            key={arm.id}
            className="flex flex-wrap items-center justify-between gap-3 rounded-md border border-border bg-surface px-4 py-3 text-sm"
          >
            <Link to={paths.armDetail(arm.id)} className="flex flex-col hover:underline">
              <span className="font-medium text-foreground">{arm.displayName}</span>
              <span className="text-xs text-muted-foreground">
                Capacity {arm.capacity} · {arm.status}
              </span>
            </Link>
            <FormTeacherLabel
              formTeacherAdminId={arm.formTeacherAdminId}
              name={arm.formTeacherAdminId ? formTeacherNames[arm.formTeacherAdminId] : undefined}
              canResolve={canResolveFormTeacherNames}
            />
            {canUpdate ? (
              <Button variant="outline" size="sm" render={<Link to={paths.armDetail(arm.id)} />}>
                Manage
              </Button>
            ) : null}
          </li>
        ))}
      </ul>

      {arms.hasNextPage ? (
        <Button
          variant="outline"
          size="sm"
          onClick={() => void arms.fetchNextPage()}
          disabled={arms.isFetchingNextPage}
        >
          {arms.isFetchingNextPage ? 'Loading…' : 'Load more'}
        </Button>
      ) : null}
    </div>
  );
}
