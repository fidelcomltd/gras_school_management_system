import { useState } from 'react';
import { FormError } from '@/components/feedback/query-states';
import { Button } from '@/components/ui/button';
import { Dialog, DialogContent, DialogDescription, DialogFooter, DialogHeader, DialogTitle } from '@/components/ui/dialog';
import { Field, FieldDescription, FieldLabel } from '@/components/ui/field';
import { ArmSelect } from '@/features/admissions/components/arm-select';
import { ApiError } from '@/lib/http';
import { lagosToday } from '@/shared/format/date';
import { errorText } from '../records/format';
import { canCommit, useActiveArms, useTransferPupil, type PupilMovementOutcome } from './api';
import { Consequences } from './consequences';

const dateInput = 'h-9 w-44 rounded-md border border-input bg-background px-2 text-sm text-foreground';

/**
 * `POST /api/v1/pupils/{id}/transfer` (spec 6.5.17, 06 §6.4.4): a dry run first, so the office sees which result sets
 * the move sends back for recompute or which published results block it, then the move itself.
 */
export function TransferDialog({
  pupilId,
  currentArmId,
  sessionId,
  onClose,
}: {
  pupilId: string;
  currentArmId: string;
  sessionId: string;
  onClose: () => void;
}) {
  const [today] = useState(lagosToday);
  const [armId, setArmId] = useState('');
  const [effectiveDate, setEffectiveDate] = useState(today);
  const [preview, setPreview] = useState<PupilMovementOutcome | null>(null);
  // One key per decision, reused on a retry after a dropped response; a refusal (4xx) wrote nothing, so a corrected
  // attempt draws a fresh one. A dry run always gets its own key, or the commit would replay the preview.
  const [commitKey, setCommitKey] = useState(() => crypto.randomUUID());
  const transfer = useTransferPupil(pupilId);
  const choices = useActiveArms(sessionId).filter((arm) => arm.id !== currentArmId);

  const change = (apply: () => void) => {
    apply();
    setPreview(null);
    transfer.reset();
  };

  const check = () =>
    transfer.mutate(
      { id: pupilId, armId, effectiveDate, dryRun: true, idempotencyKey: crypto.randomUUID() },
      { onSuccess: setPreview },
    );

  const commit = () =>
    transfer.mutate(
      { id: pupilId, armId, effectiveDate, dryRun: false, idempotencyKey: commitKey },
      {
        onSuccess: onClose,
        onError: (error) => {
          if (error instanceof ApiError && error.status !== undefined && error.status >= 400 && error.status < 500) {
            setCommitKey(crypto.randomUUID());
          }
        },
      },
    );

  return (
    <Dialog open onOpenChange={(open) => !open && onClose()}>
      <DialogContent>
        <DialogHeader>
          <DialogTitle>Move to another class</DialogTitle>
          <DialogDescription>
            Marks already entered travel with the pupil. Moving a pupil after the examination has been marked will rank
            them against a class they did not sit the term with. Consider transferring at the end of term.
          </DialogDescription>
        </DialogHeader>

        <div className="flex flex-col gap-4">
          <FormError message={errorText(transfer.error)} />
          <Field>
            <FieldLabel>New class</FieldLabel>
            <ArmSelect value={armId} onChange={(value) => change(() => setArmId(value))} arms={choices} />
          </Field>
          <Field>
            <FieldLabel>First day in the new class</FieldLabel>
            <input
              type="date"
              aria-label="First day in the new class"
              className={dateInput}
              value={effectiveDate}
              max={today}
              onChange={(event) => change(() => setEffectiveDate(event.target.value))}
            />
            <FieldDescription>The pupil leaves the old class the day before.</FieldDescription>
          </Field>
          {preview ? <Consequences outcome={preview} /> : null}
        </div>

        <DialogFooter>
          <Button type="button" variant="ghost" onClick={onClose}>
            Cancel
          </Button>
          {preview ? (
            <Button type="button" disabled={transfer.isPending || !canCommit(preview)} onClick={commit}>
              {transfer.isPending ? 'Moving…' : 'Move pupil'}
            </Button>
          ) : (
            <Button type="button" disabled={transfer.isPending || armId === '' || effectiveDate === ''} onClick={check}>
              {transfer.isPending ? 'Checking…' : 'Check what changes'}
            </Button>
          )}
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
}
