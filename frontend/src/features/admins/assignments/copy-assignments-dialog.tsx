import { useState } from 'react';
import { FormError, LoadingState, QueryErrorState } from '@/components/feedback/query-states';
import { Button } from '@/components/ui/button';
import { Dialog, DialogContent, DialogFooter, DialogHeader, DialogTitle } from '@/components/ui/dialog';
import { useSessions } from '@/features/sessions/api';
import { ApiError } from '@/lib/http';
import { useAllPages } from '@/lib/query/use-all-pages';
import { LabelledSelect } from '@/shared/pickers/labelled-select';
import { useCopyAssignments } from './api';
import type { AssignmentCopyResultDto, AssignmentCopyRowDto } from './types';

function Rows({ rows, showReason }: { rows: AssignmentCopyRowDto[]; showReason: boolean }) {
  return (
    <ul className="flex max-h-56 flex-col divide-y divide-border overflow-y-auto rounded-md border border-border">
      {rows.map((row) => (
        <li key={row.sourceAssignmentId} className="flex flex-col px-3 py-2 text-sm">
          <span className="font-medium text-foreground">
            {row.staffName} · {row.roleName}
          </span>
          <span className="text-xs text-muted-foreground">
            {row.scopeType === 'SchoolWide' ? 'Whole school' : row.armNames.join(', ')}
            {showReason && row.skipReason ? ` — ${row.skipReason}` : ''}
          </span>
        </li>
      ))}
    </ul>
  );
}

/**
 * Copies one session's role assignments into another (spec 4.2.2, 6.1.14): last year's allocation does not carry over on its
 * own. A preview (dry run) lists what would be created and what would be skipped, and why, before anything is written.
 */
export function CopyAssignmentsDialog({ onClose }: { onClose: () => void }) {
  const sessionsQuery = useSessions();
  const sessions = useAllPages(sessionsQuery);
  const copy = useCopyAssignments();
  const [fromChoice, setFromChoice] = useState<string | null>(null);
  const [toChoice, setToChoice] = useState<string | null>(null);
  // A preview belongs to the pair it was run for; one for another pair never shows or drives the copy.
  const [previewed, setPreviewed] = useState<{ fromId: string; toId: string; result: AssignmentCopyResultDto } | null>(null);
  const [done, setDone] = useState<AssignmentCopyResultDto | null>(null);

  const open = sessions.filter((session) => session.state !== 'Closed');
  const active = sessions.find((session) => session.state === 'Active');
  const fromId = fromChoice ?? active?.id ?? '';
  const toId = toChoice ?? open.find((session) => session.state === 'Upcoming')?.id ?? '';
  const ready = fromId !== '' && toId !== '' && fromId !== toId;
  const error = copy.error instanceof ApiError ? copy.error.message : null;
  const preview = previewed && previewed.fromId === fromId && previewed.toId === toId ? previewed.result : null;

  const runPreview = () => {
    const pair = { fromId, toId };
    copy.mutate({ fromSessionId: fromId, toSessionId: toId, dryRun: true }, { onSuccess: (result) => setPreviewed({ ...pair, result }) });
  };
  const runCopy = () => {
    if (previewed) {
      copy.mutate({ fromSessionId: previewed.fromId, toSessionId: previewed.toId, dryRun: false }, { onSuccess: setDone });
    }
  };

  const body = () => {
    if (sessionsQuery.isError) return <QueryErrorState error={sessionsQuery.error} onRetry={() => void sessionsQuery.refetch()} />;
    if (sessionsQuery.isPending || sessionsQuery.hasNextPage) return <LoadingState label="Loading sessions…" />;
    if (done) {
      return (
        <output className="block text-sm text-foreground">
          Copied {done.copied.length} assignment{done.copied.length === 1 ? '' : 's'} into {done.toSessionName}
          {done.skipped.length > 0 ? `; ${done.skipped.length} skipped.` : '.'}
        </output>
      );
    }
    return (
      <>
        <LabelledSelect
          label="Copy from"
          placeholder="Session"
          value={fromId}
          options={sessions.map((session) => ({ value: session.id, label: session.name }))}
          onChange={setFromChoice}
          className="w-full"
        />
        <LabelledSelect
          label="Copy into"
          placeholder="An open session"
          value={toId}
          options={open.map((session) => ({ value: session.id, label: session.name }))}
          onChange={setToChoice}
          className="w-full"
        />
        {preview ? (
          <div className="flex flex-col gap-3">
            <p className="text-sm text-foreground">
              {preview.copied.length} will be created in {preview.toSessionName}.
            </p>
            {preview.copied.length > 0 ? <Rows rows={preview.copied} showReason={false} /> : null}
            {preview.skipped.length > 0 ? (
              <>
                <p className="text-sm text-foreground">{preview.skipped.length} will be skipped:</p>
                <Rows rows={preview.skipped} showReason />
              </>
            ) : null}
          </div>
        ) : null}
      </>
    );
  };

  return (
    <Dialog open onOpenChange={(isOpen) => !isOpen && onClose()}>
      <DialogContent>
        <DialogHeader>
          <DialogTitle>Copy assignments to a new session</DialogTitle>
        </DialogHeader>
        <div className="flex flex-col gap-4">
          <FormError message={error} />
          {body()}
        </div>
        <DialogFooter>
          <Button variant="outline" onClick={onClose}>
            {done ? 'Close' : 'Cancel'}
          </Button>
          {done ? null : preview ? (
            <Button disabled={copy.isPending || preview.copied.length === 0} onClick={runCopy}>
              Copy {preview.copied.length} assignment{preview.copied.length === 1 ? '' : 's'}
            </Button>
          ) : (
            <Button disabled={!ready || copy.isPending} onClick={runPreview}>
              Preview
            </Button>
          )}
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
}
