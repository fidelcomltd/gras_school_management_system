import { useState } from 'react';
import { FormError } from '@/components/feedback/query-states';
import { Button } from '@/components/ui/button';
import { Dialog, DialogContent, DialogFooter, DialogHeader, DialogTitle } from '@/components/ui/dialog';
import { ApiError } from '@/lib/http';
import { useCommitPromotion, useReversePromotion } from '../promotion-api';
import { OUTCOME_LABELS, REASON_MIN, type Drafts, type Outcome } from '../promotion-decisions';
import type { PromotionBatchDto, PromotionPreviewDto, PromotionTargetArmDto } from '../types';

const REASON_MAX = 500;

/** The last look before spec 6.3.7's one-transaction commit: tallies, capacity warnings, then commit. */
export function CommitPromotionDialog({
  preview,
  drafts,
  overCapacity,
  onClose,
}: {
  preview: PromotionPreviewDto;
  drafts: Drafts;
  overCapacity: PromotionTargetArmDto[];
  onClose: () => void;
}) {
  const commit = useCommitPromotion(preview.sourceSession.id);
  const target = preview.targetSession;
  const tally = new Map<Outcome, number>();
  for (const draft of Object.values(drafts)) {
    if (draft.outcome) tally.set(draft.outcome, (tally.get(draft.outcome) ?? 0) + 1);
  }

  const submit = () => {
    if (!target) return;
    commit.mutate(
      {
        sessionId: preview.sourceSession.id,
        targetSessionId: target.id,
        decisions: preview.rows.map((row) => {
          const draft = drafts[row.pupilId];
          const reason = draft?.reason.trim();
          return {
            pupilId: row.pupilId,
            outcome: draft?.outcome ?? 'Repeat',
            targetArmId: draft?.targetArmId ?? null,
            reason: reason ? reason : null,
          };
        }),
      },
      { onSuccess: onClose },
    );
  };

  return (
    <Dialog open onOpenChange={(open) => !open && !commit.isPending && onClose()}>
      <DialogContent>
        <DialogHeader>
          <DialogTitle>Commit promotion into {target?.name}</DialogTitle>
        </DialogHeader>
        <div className="flex flex-col gap-3 text-sm">
          <FormError message={commit.error instanceof ApiError ? commit.error.message : null} />
          <ul className="flex flex-col gap-1">
            {[...tally.entries()].map(([outcome, count]) => (
              <li key={outcome}>
                {OUTCOME_LABELS[outcome]}: <strong>{count}</strong>
              </li>
            ))}
          </ul>
          {overCapacity.length > 0 ? (
            <p className="text-warning">These arms will be over capacity: {overCapacity.map((arm) => arm.name).join(', ')}.</p>
          ) : null}
          <p className="text-muted-foreground">
            Every current enrolment closes on {preview.sourceSession.endDate} and the new ones start on {target?.startDate}. It is applied
            to all {preview.rows.length} pupils at once, or not at all.
          </p>
        </div>
        <DialogFooter>
          <Button type="button" variant="ghost" onClick={onClose} disabled={commit.isPending}>
            Cancel
          </Button>
          <Button type="button" onClick={submit} disabled={commit.isPending || !target}>
            {commit.isPending ? 'Committing…' : 'Commit promotion'}
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
}

/** `POST /promotion-batches/{id}/reverse`: a Super Admin's reason, refused verbatim once marks or pins exist in the new session. */
export function ReversePromotionDialog({ batch, onClose }: { batch: PromotionBatchDto; onClose: () => void }) {
  const reverse = useReversePromotion();
  const [reason, setReason] = useState('');
  const [touched, setTouched] = useState(false);
  const trimmed = reason.trim();
  const invalid = trimmed.length < REASON_MIN || trimmed.length > REASON_MAX;

  const submit = (event: React.FormEvent) => {
    event.preventDefault();
    setTouched(true);
    if (invalid) return;
    reverse.mutate({ batchId: batch.id, reason: trimmed }, { onSuccess: onClose });
  };

  return (
    <Dialog open onOpenChange={(open) => !open && !reverse.isPending && onClose()}>
      <DialogContent>
        <DialogHeader>
          <DialogTitle>Reverse the promotion into {batch.targetSessionName}</DialogTitle>
        </DialogHeader>
        <form onSubmit={submit} className="flex flex-col gap-4" noValidate>
          <FormError message={reverse.error instanceof ApiError ? reverse.error.message : null} />
          <p className="text-sm text-muted-foreground">
            The new enrolments are removed, the old ones reopened and graduates restored to active. The batch stays on record, marked
            reversed.
          </p>
          <label className="flex flex-col gap-1 text-sm font-medium text-foreground">
            Reason
            <textarea
              rows={3}
              maxLength={REASON_MAX}
              aria-invalid={touched && invalid}
              aria-describedby="reverse-reason-error"
              className="w-full rounded-md border border-input bg-surface px-3 py-2 text-sm font-normal text-foreground aria-invalid:border-destructive"
              value={reason}
              onChange={(event) => setReason(event.target.value)}
            />
          </label>
          {touched && invalid ? (
            <p id="reverse-reason-error" className="text-sm text-destructive">
              Give a reason of {REASON_MIN} to {REASON_MAX} characters.
            </p>
          ) : null}
          <DialogFooter>
            <Button type="button" variant="ghost" onClick={onClose} disabled={reverse.isPending}>
              Cancel
            </Button>
            <Button type="submit" variant="destructive" disabled={reverse.isPending}>
              {reverse.isPending ? 'Reversing…' : 'Reverse promotion'}
            </Button>
          </DialogFooter>
        </form>
      </DialogContent>
    </Dialog>
  );
}
