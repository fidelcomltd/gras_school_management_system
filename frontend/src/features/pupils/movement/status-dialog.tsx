import { useState } from 'react';
import { FormError } from '@/components/feedback/query-states';
import { Button } from '@/components/ui/button';
import { Dialog, DialogContent, DialogDescription, DialogFooter, DialogHeader, DialogTitle } from '@/components/ui/dialog';
import { Field, FieldDescription, FieldLabel } from '@/components/ui/field';
import { Input } from '@/components/ui/input';
import { Select, SelectContent, SelectItem, SelectTrigger } from '@/components/ui/select';
import { ArmSelect } from '@/features/admissions/components/arm-select';
import { useSessions } from '@/features/sessions/api';
import { ApiError } from '@/lib/http';
import { lagosToday } from '@/shared/format/date';
import { errorText } from '../records/format';
import type { PupilStatus } from '../types';
import { canCommit, useActiveArms, useChangePupilStatus, type PupilMovementOutcome } from './api';
import { Consequences } from './consequences';

const dateInput = 'h-9 w-44 rounded-md border border-input bg-background px-2 text-sm text-foreground';

const LABELS: Partial<Record<PupilStatus, string>> = {
  Transferred: 'Transferred to another school',
  Withdrawn: 'Withdrawn',
  Graduated: 'Graduated',
  Active: 'Reactivate (back in a class)',
};

/** Spec 6.5.14's transitions out of each status. A pending admission is handled from the admissions queue. */
function targetsFor(status: PupilStatus): PupilStatus[] {
  if (status === 'Active') return ['Transferred', 'Withdrawn', 'Graduated'];
  if (status === 'Transferred' || status === 'Withdrawn' || status === 'Graduated') return ['Active'];
  return [];
}

/** `POST /api/v1/pupils/{id}/status` (spec 6.5.14): leave, graduate or reactivate, with a dry run before the change. */
export function StatusDialog({ pupilId, status, onClose }: { pupilId: string; status: PupilStatus; onClose: () => void }) {
  const targets = targetsFor(status);
  const [today] = useState(lagosToday);
  const [target, setTarget] = useState<PupilStatus>(targets[0] ?? 'Active');
  const [effectiveDate, setEffectiveDate] = useState(today);
  const [reason, setReason] = useState('');
  const [armId, setArmId] = useState('');
  const [preview, setPreview] = useState<PupilMovementOutcome | null>(null);
  const [commitKey, setCommitKey] = useState(() => crypto.randomUUID());
  const changeStatus = useChangePupilStatus(pupilId);

  const reactivating = target === 'Active';
  const reasonRequired = !reactivating || status === 'Graduated';
  const sessions = useSessions('Active');
  const activeSessionId = sessions.data?.pages[0]?.items[0]?.id ?? '';
  const armChoices = useActiveArms(activeSessionId);

  const change = (apply: () => void) => {
    apply();
    setPreview(null);
    changeStatus.reset();
  };

  const body = (dryRun: boolean, idempotencyKey: string) => ({
    id: pupilId,
    targetStatus: target,
    effectiveDate,
    reason: reason.trim() === '' ? null : reason.trim(),
    armId: reactivating ? armId : null,
    dryRun,
    idempotencyKey,
  });

  const ready = (!reasonRequired || reason.trim() !== '') && effectiveDate !== '' && (!reactivating || armId !== '');

  const check = () => changeStatus.mutate(body(true, crypto.randomUUID()), { onSuccess: setPreview });

  const commit = () =>
    changeStatus.mutate(body(false, commitKey), {
      onSuccess: onClose,
      onError: (error) => {
        if (error instanceof ApiError && error.status !== undefined && error.status >= 400 && error.status < 500) {
          setCommitKey(crypto.randomUUID());
        }
      },
    });

  return (
    <Dialog open onOpenChange={(open) => !open && onClose()}>
      <DialogContent>
        <DialogHeader>
          <DialogTitle>Change status</DialogTitle>
          <DialogDescription>
            {reactivating
              ? 'The pupil keeps their registration number and all their history, and joins the class from the date given.'
              : 'A pupil who leaves drops off score entry and this term’s results from the effective date; their marks are kept. ' +
                'If the school wants a result for a pupil leaving before the examination, keep them active until the results are ' +
                'published and change the status afterwards.'}
          </DialogDescription>
        </DialogHeader>

        <div className="flex flex-col gap-4">
          <FormError message={errorText(changeStatus.error)} />
          {targets.length > 1 ? (
            <Field>
              <FieldLabel>New status</FieldLabel>
              <Select
                items={targets.map((value) => ({ value, label: LABELS[value] ?? value }))}
                value={target}
                onValueChange={(next) => next && change(() => setTarget(next as PupilStatus))}
              >
                <SelectTrigger aria-label="New status" />
                <SelectContent>
                  {targets.map((value) => (
                    <SelectItem key={value} value={value}>
                      {LABELS[value] ?? value}
                    </SelectItem>
                  ))}
                </SelectContent>
              </Select>
            </Field>
          ) : null}

          {reactivating ? (
            <Field>
              <FieldLabel>Class</FieldLabel>
              <ArmSelect value={armId} onChange={(value) => change(() => setArmId(value))} arms={armChoices} />
            </Field>
          ) : null}

          <Field>
            <FieldLabel>Effective date</FieldLabel>
            <input
              type="date"
              aria-label="Effective date"
              className={dateInput}
              value={effectiveDate}
              max={today}
              onChange={(event) => change(() => setEffectiveDate(event.target.value))}
            />
          </Field>

          <Field>
            <FieldLabel>{reasonRequired ? 'Reason' : 'Reason (optional)'}</FieldLabel>
            <Input value={reason} maxLength={500} onChange={(event) => change(() => setReason(event.target.value))} />
            <FieldDescription>Shown on the pupil’s record. Keep health details out of it.</FieldDescription>
          </Field>

          {preview ? <Consequences outcome={preview} /> : null}
        </div>

        <DialogFooter>
          <Button type="button" variant="ghost" onClick={onClose}>
            Cancel
          </Button>
          {preview ? (
            <Button type="button" disabled={changeStatus.isPending || !canCommit(preview)} onClick={commit}>
              {changeStatus.isPending ? 'Saving…' : 'Confirm change'}
            </Button>
          ) : (
            <Button type="button" disabled={changeStatus.isPending || !ready} onClick={check}>
              {changeStatus.isPending ? 'Checking…' : 'Check what changes'}
            </Button>
          )}
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
}
