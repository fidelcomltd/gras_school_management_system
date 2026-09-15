import { Button } from '@/components/ui/button';
import { Dialog, DialogContent, DialogDescription, DialogHeader, DialogTitle } from '@/components/ui/dialog';
import type { AdmissionQueueRow } from '../types';

/**
 * `POST /api/v1/admissions/{id}/approve` — BLOCKED (TASK-0064, reported to
 * the orchestrator rather than shimmed, per the card's own out-of-scope
 * rule and `rules/contract.md` §5).
 *
 * `ApproveAdmissionCommand.armId` must reference an arm for THIS record's
 * `class_admitted_into` level in the record's OWN `sessionId` — spec 6.5.11
 * step 9's "the selector lists the level's arms for the active session".
 * Both ids are opaque and neither is reachable from this screen: `GET
 * /admissions` (`ListAdmissionsQueue`) returns `PupilDto` rows whose
 * `admission` object is `null` on this read path (see `PupilDto.admission`'s
 * own contract description, and the `CursorPageOfPupilDto` example, which
 * shows `"admission": null` on a queue-shaped row) — the row carries only
 * `levelAppliedFor` (a display NAME) and no `sessionId` at all.
 *
 * Matching `levelAppliedFor` against `GET /levels` by name, or assuming "the
 * currently active session", were both considered and rejected: the first
 * guesses an identity relationship the contract doesn't state, the second
 * contradicts the spec's own distinction between a record's target session
 * and whatever session happens to be active right now. Both are exactly the
 * kind of guess-across-the-boundary `CLAUDE.md` §3 and this card's own
 * out-of-scope note rule out. `GET /pupils/{id}` doesn't help either — the
 * same property description lists "single-get" among the null-returning
 * paths.
 *
 * Needs a contract addition (e.g. `sessionId`/`classAdmittedInto` on the
 * queue row) before the arm selector — and therefore this whole dialog — can
 * be built. Decline has no such dependency and is fully implemented
 * (`DeclineAdmissionDialog`).
 */
export function ApproveAdmissionDialog({
  pupil,
  onClose,
}: {
  pupil: AdmissionQueueRow;
  onClose: () => void;
}) {
  return (
    <Dialog open onOpenChange={(open) => !open && onClose()}>
      <DialogContent>
        <DialogHeader>
          <DialogTitle>
            Approve {pupil.surname} {pupil.firstName}
          </DialogTitle>
        </DialogHeader>

        <DialogDescription>
          Approval needs to offer an arm for this applicant's class, but this screen cannot yet
          determine which session and class level the application was submitted for — that
          information isn't returned by the admissions queue today. This has been reported as a
          contract gap (TASK-0064); approval will be enabled once it's resolved.
        </DialogDescription>

        <div className="mt-6 flex justify-end">
          <Button type="button" variant="outline" onClick={onClose}>
            Close
          </Button>
        </div>
      </DialogContent>
    </Dialog>
  );
}
