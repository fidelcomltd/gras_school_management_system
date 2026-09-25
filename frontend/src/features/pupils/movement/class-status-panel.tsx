import { useState } from 'react';
import { LoadingState, QueryErrorState } from '@/components/feedback/query-states';
import { Button } from '@/components/ui/button';
import { formatDate, lagosDateOf, lagosToday } from '@/shared/format/date';
import { errorText } from '../records/format';
import type { PupilStatus } from '../types';
import { usePupilEnrolments, useUndoStatusChange } from './api';
import { StatusDialog } from './status-dialog';
import { TransferDialog } from './transfer-dialog';

/** Where the pupil sits, where they sat before, and every status change (spec 6.5.14, 6.5.15), with the two actions. */
export function ClassStatusPanel({
  pupilId,
  status,
  declined,
  canTransfer,
  canChangeStatus,
}: {
  pupilId: string;
  status: PupilStatus;
  /** A declined application (withdrawn, never issued a number): it comes back through a new admission, not here. */
  declined: boolean;
  canTransfer: boolean;
  canChangeStatus: boolean;
}) {
  const history = usePupilEnrolments(pupilId);
  const undo = useUndoStatusChange(pupilId);
  const [dialog, setDialog] = useState<'transfer' | 'status' | null>(null);
  const [confirmUndo, setConfirmUndo] = useState(false);

  if (history.isPending) return <LoadingState label="Loading class history…" />;
  if (history.isError) return <QueryErrorState error={history.error} onRetry={() => void history.refetch()} />;

  const data = history.data;
  const current = data.enrolments.find((enrolment) => enrolment.effectiveTo === null);
  const showTransfer = canTransfer && status === 'Active' && !!current;
  const showStatus = canChangeStatus && status !== 'Pending' && !declined;
  // Only today's leaving change can be undone (human ruling 2026-09-25); anything else goes through Change status.
  const latest = data.statusChanges.at(-1);
  const undoable =
    canChangeStatus &&
    !!latest &&
    latest.toStatus === status &&
    (status === 'Transferred' || status === 'Withdrawn' || status === 'Graduated') &&
    lagosDateOf(latest.changedAtUtc) === lagosToday();

  return (
    <div className="flex flex-col gap-4">
      <div className="flex flex-wrap items-center justify-between gap-3 rounded-md border border-border bg-surface p-4 text-sm">
        <p className="text-foreground">
          <span className="text-muted-foreground">Status </span>
          {status}
          <span className="ml-4 text-muted-foreground">Class </span>
          {data.currentArmName ?? 'Not in a class'}
        </p>
        <div className="flex gap-2">
          {showTransfer ? (
            <Button variant="outline" onClick={() => setDialog('transfer')}>
              Move to another class
            </Button>
          ) : null}
          {showStatus ? (
            <Button variant="outline" onClick={() => setDialog('status')}>
              Change status
            </Button>
          ) : null}
        </div>
      </div>

      <section className="flex flex-col gap-2">
        <h2 className="text-sm font-semibold text-foreground">Classes</h2>
        {data.enrolments.length === 0 ? (
          <p className="text-sm text-muted-foreground">Not yet enrolled in a class.</p>
        ) : (
          <table className="w-full text-left text-sm">
            <thead className="text-muted-foreground">
              <tr>
                <th className="py-1 pr-4 font-medium">Class</th>
                <th className="py-1 pr-4 font-medium">Session</th>
                <th className="py-1 pr-4 font-medium">From</th>
                <th className="py-1 font-medium">To</th>
              </tr>
            </thead>
            <tbody>
              {data.enrolments.map((enrolment) => (
                <tr key={`${enrolment.armId}-${enrolment.effectiveFrom}`} className="border-t border-border">
                  <td className="py-1 pr-4">{enrolment.armName}</td>
                  <td className="py-1 pr-4">{enrolment.sessionName}</td>
                  <td className="py-1 pr-4">{formatDate(enrolment.effectiveFrom)}</td>
                  <td className="py-1">{enrolment.effectiveTo ? formatDate(enrolment.effectiveTo) : 'Now'}</td>
                </tr>
              ))}
            </tbody>
          </table>
        )}
      </section>

      {data.statusChanges.length > 0 ? (
        <section className="flex flex-col gap-2">
          <h2 className="text-sm font-semibold text-foreground">Status changes</h2>
          <ul className="flex flex-col gap-1 text-sm">
            {undoable ? (
              <li className="flex flex-wrap items-center gap-2 rounded-md bg-muted/40 px-3 py-2">
                <span className="text-muted-foreground">Recorded by mistake? It can be undone today.</span>
                {confirmUndo ? (
                  <>
                    <Button size="sm" disabled={undo.isPending} onClick={() => undo.mutate(null, { onSuccess: () => setConfirmUndo(false) })}>
                      {undo.isPending ? 'Undoing…' : 'Confirm undo'}
                    </Button>
                    <Button size="sm" variant="ghost" onClick={() => setConfirmUndo(false)}>
                      Keep it
                    </Button>
                  </>
                ) : (
                  <Button size="sm" variant="outline" onClick={() => setConfirmUndo(true)}>
                    Undo this change
                  </Button>
                )}
                {undo.error ? (
                  <span role="alert" className="text-destructive">
                    {errorText(undo.error)}
                  </span>
                ) : null}
              </li>
            ) : null}
            {data.statusChanges.map((change) => (
              <li key={`${change.changedAtUtc}-${change.toStatus}`}>
                {formatDate(change.effectiveDate)}: {change.fromStatus} to {change.toStatus}
                {change.armName ? ` (${change.armName})` : ''}
                {change.reason ? <span className="text-muted-foreground">. {change.reason}</span> : null}
              </li>
            ))}
          </ul>
        </section>
      ) : null}

      {dialog === 'transfer' && current ? (
        <TransferDialog
          pupilId={pupilId}
          currentArmId={current.armId}
          sessionId={current.sessionId}
          onClose={() => setDialog(null)}
        />
      ) : null}
      {dialog === 'status' ? <StatusDialog pupilId={pupilId} status={status} onClose={() => setDialog(null)} /> : null}
    </div>
  );
}
