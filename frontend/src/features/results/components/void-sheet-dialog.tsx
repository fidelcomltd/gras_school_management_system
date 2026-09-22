import { useState } from 'react';
import { FormError } from '@/components/feedback/query-states';
import { Button } from '@/components/ui/button';
import { Dialog, DialogContent, DialogDescription, DialogFooter, DialogHeader, DialogTitle } from '@/components/ui/dialog';
import { Field, FieldLabel } from '@/components/ui/field';
import { Input } from '@/components/ui/input';
import { ApiError } from '@/lib/http';
import { useVoidScoreSheet } from '../api';

const MIN_REASON = 10;

/** Voids a whole mark sheet with a reason (spec 6.7.4): rows are kept for the audit trail but no longer count. */
export function VoidSheetDialog({
  armId,
  subjectId,
  termId,
  subjectName,
  onClose,
}: {
  armId: string;
  subjectId: string;
  termId: string;
  subjectName: string;
  onClose: () => void;
}) {
  const voidSheet = useVoidScoreSheet(armId, subjectId, termId);
  const [reason, setReason] = useState('');
  const short = reason.trim().length < MIN_REASON;

  return (
    <Dialog open onOpenChange={(open) => !open && onClose()}>
      <DialogContent>
        <DialogHeader>
          <DialogTitle>Void the {subjectName} marks</DialogTitle>
          <DialogDescription>
            Every mark on this sheet stops counting and the sheet starts again empty. The old marks are kept in the
            record with your reason.
          </DialogDescription>
        </DialogHeader>
        <form
          className="flex flex-col gap-4"
          onSubmit={(event) => {
            event.preventDefault();
            if (!short) voidSheet.mutate({ armId, subjectId, termId, reason: reason.trim() }, { onSuccess: onClose });
          }}
        >
          <FormError message={voidSheet.error instanceof ApiError ? voidSheet.error.message : null} />
          <Field>
            <FieldLabel>Reason (at least {MIN_REASON} characters)</FieldLabel>
            <Input value={reason} onChange={(event) => setReason(event.target.value)} />
          </Field>
          <DialogFooter>
            <Button type="button" variant="ghost" onClick={onClose}>
              Cancel
            </Button>
            <Button type="submit" variant="destructive" disabled={short || voidSheet.isPending}>
              {voidSheet.isPending ? 'Voiding…' : 'Void marks'}
            </Button>
          </DialogFooter>
        </form>
      </DialogContent>
    </Dialog>
  );
}
