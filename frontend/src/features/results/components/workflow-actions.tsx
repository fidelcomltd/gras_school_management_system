import { useState } from 'react';
import type { components } from '@/api/schema';
import { FormError } from '@/components/feedback/query-states';
import { Button } from '@/components/ui/button';
import { ApiError } from '@/lib/http';
import { useComputeAnnual, useTransition, type Transition } from '../api-workflow';
import { ReasonDialog } from './reason-dialog';

type ResultSetSummaryDto = components['schemas']['ResultSetSummaryDto'];

/**
 * The buttons for the result set's current state (spec 6.7.11), each shown only to a holder of its privilege. Refusals
 * (a precondition the spec words, e.g. "Marks have changed since the last computation") come back verbatim.
 */
export function WorkflowActions({
  armId,
  termId,
  isThirdTerm,
  resultSet,
  canSubmitNow,
  can,
}: {
  armId: string;
  termId: string;
  isThirdTerm: boolean;
  resultSet: ResultSetSummaryDto;
  canSubmitNow: boolean;
  can: (privilege: string) => boolean;
}) {
  const transition = useTransition(armId, termId);
  const annual = useComputeAnnual(armId);
  const [asking, setAsking] = useState<'return' | 'withdraw' | null>(null);
  const run = (name: Transition, reason?: string) =>
    transition.mutate({ transition: name, resultSetId: resultSet.id, reason }, { onSuccess: () => setAsking(null) });
  const state = resultSet.state;
  const busy = transition.isPending;
  const open = state === 'Draft' || state === 'ReturnedForCorrection';

  const button = (label: string, name: Transition, privilege: string, disabled = false, variant: 'primary' | 'outline' = 'outline') =>
    can(privilege) ? (
      <Button variant={variant} disabled={busy || disabled} onClick={() => run(name)}>
        {label}
      </Button>
    ) : null;

  return (
    <div className="flex flex-col gap-3">
      <FormError message={!asking && transition.error instanceof ApiError ? transition.error.message : null} />
      <FormError message={annual.error instanceof ApiError ? annual.error.message : null} />
      {annual.isSuccess ? (
        <output className="block text-sm text-success">
          Annual results computed for {annual.data.pupilCount} pupils ({annual.data.proposedPromoted} proposed for promotion,{' '}
          {annual.data.proposedRepeat} to repeat).
        </output>
      ) : null}
      <div className="flex flex-wrap gap-2">
        {open ? button('Compute results', 'compute', 'result.compute') : null}
        {open ? button('Submit for approval', 'submit', 'result.submit', !canSubmitNow, 'primary') : null}
        {state === 'AwaitingApproval' ? button('Approve', 'approve', 'result.approve', false, 'primary') : null}
        {state === 'Approved' ? button('Publish to parents', 'publish', 'result.publish', false, 'primary') : null}
        {(state === 'AwaitingApproval' || state === 'Approved') && can('result.return') ? (
          <Button variant="outline" disabled={busy} onClick={() => setAsking('return')}>
            Return for correction
          </Button>
        ) : null}
        {state === 'Published' && can('result.unpublish') ? (
          <Button variant="outline" disabled={busy} onClick={() => setAsking('withdraw')}>
            Withdraw from parents
          </Button>
        ) : null}
        {state === 'Withdrawn' ? button('Reopen for correction', 'reopen', 'result.unpublish') : null}
        {state === 'Published' && isThirdTerm && can('result.annual.compute') ? (
          <Button variant="outline" disabled={annual.isPending} onClick={() => annual.mutate()}>
            {annual.isPending ? 'Computing…' : 'Compute annual results'}
          </Button>
        ) : null}
      </div>

      {asking === 'return' ? (
        <ReasonDialog
          title="Return for correction"
          description="The class teacher sees your reason on the mark sheets and can edit again."
          action="Return"
          pending={busy}
          error={transition.error}
          onSubmit={(reason) => run('return', reason)}
          onClose={() => setAsking(null)}
        />
      ) : null}
      {asking === 'withdraw' ? (
        <ReasonDialog
          title="Withdraw from parents"
          description="Parents will see “being corrected” until the results are reopened, corrected and published again."
          action="Withdraw"
          pending={busy}
          error={transition.error}
          onSubmit={(reason) => run('withdraw', reason)}
          onClose={() => setAsking(null)}
        />
      ) : null}
    </div>
  );
}
