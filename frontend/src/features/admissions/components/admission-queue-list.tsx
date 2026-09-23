import { useState } from 'react';
import { Link, useNavigate } from 'react-router';
import { paths } from '@/app/router/paths';
import { Button } from '@/components/ui/button';
import { useMe } from '@/features/auth/api';
import { CreatePupilDialog } from '@/features/pupils/components/create-pupil-dialog';
import { hasPrivilege } from '@/lib/auth/auth-session';
import { ApiError } from '@/lib/http';
import { useAdmissionsQueue } from '../api';
import type { AdmissionQueueRow } from '../types';
import { ApproveAdmissionDialog } from './approve-admission-dialog';
import { DeclineAdmissionDialog } from './decline-admission-dialog';

function pupilName(row: AdmissionQueueRow): string {
  return [row.surname, row.firstName, row.middleName].filter(Boolean).join(' ');
}

/**
 * The admissions queue itself (spec 6.5.15). Four required states
 * (CONVENTIONS.md §11) for the `GET /admissions` fetch, mirroring `ArmList`'s
 * shape. Items render in the server's own order — no client-side `.sort()`.
 *
 * `missing` is rendered as prose ("Still needed: …"), never the raw array —
 * and it is a known partial view (sections A/I only), so the screen says so
 * once, above the list; the full list is step 9 of the admission itself.
 */
export function AdmissionQueueList() {
  const admissions = useAdmissionsQueue();
  const me = useMe();
  const canDecide = !!me.data && hasPrivilege(me.data, 'pupil.admission.approve');
  const canCreate = !!me.data && hasPrivilege(me.data, 'pupil.create');
  const navigate = useNavigate();
  const [creating, setCreating] = useState(false);
  const [approving, setApproving] = useState<AdmissionQueueRow | null>(null);
  const [declining, setDeclining] = useState<AdmissionQueueRow | null>(null);

  const items = admissions.data?.pages.flatMap((page) => page.items) ?? [];

  if (admissions.isPending) {
    return <output className="text-sm text-muted-foreground">Loading admissions queue…</output>;
  }

  if (admissions.isError) {
    if (admissions.error instanceof ApiError && admissions.error.kind === 'unauthorized') return null;
    return (
      <div role="alert" className="flex flex-col items-start gap-3">
        <p className="text-sm text-destructive">{admissions.error.message}</p>
        <Button variant="outline" size="sm" onClick={() => void admissions.refetch()}>
          Try again
        </Button>
      </div>
    );
  }

  const newAdmission = canCreate ? (
    <>
      <Button className="self-start" onClick={() => setCreating(true)}>
        New admission
      </Button>
      {creating ? (
        <CreatePupilDialog onClose={() => setCreating(false)} onCreated={(pupil) => void navigate(paths.admissionFlow(pupil.id, 2))} />
      ) : null}
    </>
  ) : null;

  if (items.length === 0) {
    return (
      <div className="flex flex-col gap-4">
        {newAdmission}
        <p className="text-sm text-muted-foreground">No pending applications.</p>
      </div>
    );
  }

  return (
    <div className="flex flex-col gap-4">
      {newAdmission}
      <p className="text-xs text-muted-foreground">
        "Still needed" shows only the assessment result and the declaration. Open an admission for the
        full list: step 9 names every missing item.
      </p>

      <ul aria-label="Admissions queue" className="flex flex-col gap-2">
        {items.map((row) => (
          <li
            key={row.id}
            className="flex flex-wrap items-center justify-between gap-3 rounded-md border border-border bg-surface px-4 py-3 text-sm"
          >
            <div className="flex flex-col">
              <span className="font-medium text-foreground">{pupilName(row)}</span>
              <span className="text-xs text-muted-foreground">
                {row.levelAppliedFor ?? 'Level not yet set'} · Applied {row.dateApplicationReceived ?? '—'}
              </span>
              {row.missing && row.missing.length > 0 ? (
                <span className="text-xs text-warning">Still needed: {row.missing.join(', ')}</span>
              ) : null}
            </div>

            <div className="flex gap-2">
              <Link to={paths.admissionFlow(row.id)} className="inline-flex h-8 items-center rounded-md px-3 text-sm font-medium text-primary hover:underline">
                Continue
              </Link>
              {canDecide ? (
                <>
                  <Button variant="outline" size="sm" onClick={() => setDeclining(row)}>
                    Decline
                  </Button>
                  <Button size="sm" onClick={() => setApproving(row)}>
                    Approve
                  </Button>
                </>
              ) : null}
            </div>
          </li>
        ))}
      </ul>

      {admissions.hasNextPage ? (
        <Button
          variant="outline"
          size="sm"
          onClick={() => void admissions.fetchNextPage()}
          disabled={admissions.isFetchingNextPage}
        >
          {admissions.isFetchingNextPage ? 'Loading…' : 'Load more'}
        </Button>
      ) : null}

      {declining ? <DeclineAdmissionDialog pupil={declining} onClose={() => setDeclining(null)} /> : null}
      {approving ? <ApproveAdmissionDialog pupil={approving} onClose={() => setApproving(null)} /> : null}
    </div>
  );
}
