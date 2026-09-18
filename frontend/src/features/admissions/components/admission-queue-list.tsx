import { useState } from 'react';
import { Button } from '@/components/ui/button';
import { useMe } from '@/features/auth/api';
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
 * and it is a known partial view (sections A/I only; steps 2-8 have no
 * entity yet), so the screen says so once, above the list, rather than
 * implying each row's list is exhaustive.
 */
export function AdmissionQueueList() {
  const admissions = useAdmissionsQueue();
  const me = useMe();
  const canDecide = !!me.data && hasPrivilege(me.data, 'pupil.admission.approve');
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

  if (items.length === 0) {
    return <p className="text-sm text-muted-foreground">No pending applications.</p>;
  }

  return (
    <div className="flex flex-col gap-4">
      <p className="text-xs text-muted-foreground">
        "Still needed" reflects only what today's records can check (the assessment result and the
        declaration signature) — a blank list is not a promise nothing else is outstanding.
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

            {canDecide ? (
              <div className="flex gap-2">
                <Button variant="outline" size="sm" onClick={() => setDeclining(row)}>
                  Decline
                </Button>
                <Button size="sm" onClick={() => setApproving(row)}>
                  Approve
                </Button>
              </div>
            ) : null}
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
