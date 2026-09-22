import { useState } from 'react';
import { Link, useParams } from 'react-router';
import { paths } from '@/app/router/paths';
import { FormError, LoadingState, QueryErrorState } from '@/components/feedback/query-states';
import { Button } from '@/components/ui/button';
import { useMe } from '@/features/auth/api';
import { hasPrivilege } from '@/lib/auth/auth-session';
import { ApiError } from '@/lib/http';
import { ReasonDialog } from '@/shared/dialogs/reason-dialog';
import { usePinAction, usePinBatch, usePinDownload } from './api';
import { PinTable } from './components/pin-table';
import { formatDateTime, type PinSummaryDto } from './types';

type Asking = { kind: 'revoke-batch' } | { kind: 'revoke-pin' | 'reinstate-pin'; pin: PinSummaryDto };

/** `/pins/:id` — one batch: print its slips and distribution list, mark it handed out, revoke it or single pins (6.8.9–6.8.11). */
export function PinBatchScreen() {
  const { id = '' } = useParams();
  const detail = usePinBatch(id);
  const action = usePinAction(id);
  const download = usePinDownload(id);
  const me = useMe();
  const [asking, setAsking] = useState<Asking | null>(null);
  const [openedAt] = useState(() => Date.now()); // the page's "now", fixed per visit
  const can = (privilege: string) => !!me.data && hasPrivilege(me.data, privilege);

  if (detail.isPending) return <LoadingState label="Loading pin batch…" />;
  if (detail.isError) return <QueryErrorState error={detail.error} onRetry={() => void detail.refetch()} />;

  const { batch, pins } = detail.data;
  const purged = new Date(batch.plaintextPurgeAt).getTime() < openedAt;
  const live = batch.state !== 'Revoked';
  const error = [download.error, !asking ? action.error : null].find((e) => e instanceof ApiError);

  return (
    <div className="flex flex-col gap-6">
      <Link to={paths.pins} className="text-sm text-primary hover:underline">
        ← All pin batches
      </Link>
      <header className="flex flex-col gap-1">
        <h1 className="font-display text-2xl font-semibold text-foreground">{batch.name}</h1>
        <p className="text-sm text-muted-foreground">
          {batch.pinCount} pins of {batch.pinLength} characters, {batch.maxUses} uses each · {batch.state}
          {batch.purposeNote ? ` · ${batch.purposeNote}` : ''}
        </p>
        {batch.revokeReason ? <p className="text-sm text-destructive">Revoked: {batch.revokeReason}</p> : null}
      </header>

      <dl className="flex flex-wrap gap-3 text-sm">
        {[
          ['Used', batch.pinsUsed],
          ['Used up', batch.pinsExhausted],
          ['Suspended', batch.pinsSuspended],
          ['Revoked', batch.pinsRevoked],
        ].map(([label, value]) => (
          <div key={label} className="rounded-md border border-border bg-surface px-3 py-2">
            <dt className="text-muted-foreground">{label}</dt>
            <dd className="font-medium text-foreground">{value}</dd>
          </div>
        ))}
      </dl>

      <FormError message={error?.message} />
      <div className="flex flex-wrap gap-2">
        {can('pin.print') && live ? (
          <>
            <Button disabled={download.isPending || purged} onClick={() => download.mutate('print')}>
              Print slips (PDF)
            </Button>
            <Button variant="outline" disabled={download.isPending} onClick={() => download.mutate('distribution-list')}>
              Distribution list (PDF)
            </Button>
          </>
        ) : null}
        {can('pin.generate') && batch.state === 'Printed' ? (
          <Button variant="outline" disabled={action.isPending} onClick={() => action.mutate({ kind: 'mark-distributed' })}>
            Mark as handed out
          </Button>
        ) : null}
        {can('pin.revoke') && live ? (
          <Button variant="ghost" onClick={() => setAsking({ kind: 'revoke-batch' })}>
            Revoke batch…
          </Button>
        ) : null}
      </div>
      <p className="text-sm text-muted-foreground">
        {purged
          ? 'The pin values were erased from the server after 30 days, so the slips can no longer be printed.'
          : `Slips can be printed until ${formatDateTime(batch.plaintextPurgeAt)}; after that the pin values are erased.`}
      </p>

      <PinTable
        pins={pins}
        canRevoke={can('pin.revoke') && live}
        onRevoke={(pin) => setAsking({ kind: 'revoke-pin', pin })}
        onReinstate={(pin) => setAsking({ kind: 'reinstate-pin', pin })}
      />

      {asking ? (
        <ReasonDialog
          title={asking.kind === 'revoke-batch' ? 'Revoke the whole batch' : asking.kind === 'revoke-pin' ? `Revoke pin ${asking.pin.prefix}…` : `Reinstate pin ${asking.pin.prefix}…`}
          description={
            asking.kind === 'revoke-batch'
              ? 'Every pin in it stops working at once, including any parent viewing results with one now.'
              : asking.kind === 'revoke-pin'
                ? 'This pin stops working at once.'
                : 'This pin works again for the uses it has left.'
          }
          action={asking.kind === 'reinstate-pin' ? 'Reinstate' : 'Revoke'}
          minLength={1}
          pending={action.isPending}
          error={action.error}
          onSubmit={(reason) =>
            action.mutate(asking.kind === 'revoke-batch' ? { kind: 'revoke-batch', reason } : { kind: asking.kind, pinId: asking.pin.id, reason }, {
              onSuccess: () => setAsking(null),
            })
          }
          onClose={() => setAsking(null)}
        />
      ) : null}
    </div>
  );
}
