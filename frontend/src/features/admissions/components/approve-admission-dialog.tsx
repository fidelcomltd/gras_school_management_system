import { Button } from '@/components/ui/button';
import { Dialog, DialogContent, DialogHeader, DialogTitle } from '@/components/ui/dialog';
import { ApiError } from '@/lib/http';
import { useAdmissionRecord } from '../api';
import type { AdmissionQueueRow } from '../types';
import { ApproveAdmissionForm } from './approve-admission-form';

/**
 * `POST /api/v1/admissions/{id}/approve`. Fetches the admission record first
 * (`GET /admissions/{id}`, TASK-0066) — the queue row (`AdmissionQueueRow`)
 * only carries `levelAppliedFor` as a display NAME, never the `sessionId`/
 * `classAdmittedInto` ids the arm selector needs; see `types.ts`'s own note
 * and TASK-0064's card Log for the contract gap this closed. Four required
 * states (CONVENTIONS.md §11) for that fetch, mirroring `ArmDetailScreen`'s
 * shape; the form itself is `ApproveAdmissionForm`, split out to stay under
 * CONVENTIONS.md §3's 180-line cap.
 */
export function ApproveAdmissionDialog({
  pupil,
  onClose,
}: {
  pupil: AdmissionQueueRow;
  onClose: () => void;
}) {
  const record = useAdmissionRecord(pupil.id);

  if (record.isError && record.error instanceof ApiError && record.error.kind === 'unauthorized') {
    return null;
  }

  return (
    <Dialog open onOpenChange={(open) => !open && onClose()}>
      <DialogContent>
        <DialogHeader>
          <DialogTitle>
            Approve {pupil.surname} {pupil.firstName}
          </DialogTitle>
        </DialogHeader>

        {record.isPending ? (
          <output className="text-sm text-muted-foreground">Loading application…</output>
        ) : record.isError ? (
          <div role="alert" className="flex flex-col items-start gap-3">
            <p className="text-sm text-destructive">{record.error.message}</p>
            <Button variant="outline" size="sm" onClick={() => void record.refetch()}>
              Try again
            </Button>
          </div>
        ) : (
          <ApproveAdmissionForm pupil={pupil} record={record.data} onClose={onClose} />
        )}
      </DialogContent>
    </Dialog>
  );
}
