import { useState } from 'react';
import { Button } from '@/components/ui/button';
import { ApiError } from '@/lib/http';
import { useCloseTerm, useOpenTerm } from '../api';
import type { TermDto } from '../types';
import { EditTermDialog } from './edit-term-dialog';
import { ReopenTermDialog } from './reopen-term-dialog';

/**
 * One term's read view plus its transitions (spec 6.3.6). Precondition
 * failures from `open`/`close` return a naming message (spec 6.3.6) — shown
 * verbatim via `ApiError.message`, never mapped to a generic string (TASK-0042 AC).
 */
export function TermCard({
  sessionId,
  term,
  canEdit,
  canOpen,
  canClose,
  canReopen,
}: {
  sessionId: string;
  term: TermDto;
  canEdit: boolean;
  canOpen: boolean;
  canClose: boolean;
  canReopen: boolean;
}) {
  const [showEdit, setShowEdit] = useState(false);
  const [showReopen, setShowReopen] = useState(false);
  const openTerm = useOpenTerm(sessionId);
  const closeTerm = useCloseTerm(sessionId);

  const transitionError = openTerm.error ?? closeTerm.error;
  const transitionMessage = transitionError instanceof ApiError ? transitionError.message : null;

  return (
    <div className="flex flex-col gap-3 rounded-md border border-border bg-surface p-4">
      <div className="flex flex-wrap items-center justify-between gap-3">
        <div>
          <h2 className="text-sm font-semibold text-foreground">{term.name}</h2>
          <p className="text-xs text-muted-foreground">
            {term.startDate} – {term.endDate} · {term.state}
            {term.nextResumptionDate ? ` · resumes ${term.nextResumptionDate}` : ''}
            {term.timesSchoolOpened !== null ? ` · opened ${term.timesSchoolOpened} times` : ''}
          </p>
        </div>

        <div className="flex flex-wrap gap-2">
          {canEdit ? (
            <Button variant="outline" size="sm" onClick={() => setShowEdit(true)}>
              Edit
            </Button>
          ) : null}
          {canOpen && term.state === 'Upcoming' ? (
            <Button size="sm" disabled={openTerm.isPending} onClick={() => openTerm.mutate(term.id)}>
              {openTerm.isPending ? 'Opening…' : 'Open term'}
            </Button>
          ) : null}
          {canClose && term.state === 'Active' ? (
            <Button size="sm" disabled={closeTerm.isPending} onClick={() => closeTerm.mutate(term.id)}>
              {closeTerm.isPending ? 'Closing…' : 'Close term'}
            </Button>
          ) : null}
          {canReopen && term.state === 'Closed' ? (
            <Button variant="outline" size="sm" onClick={() => setShowReopen(true)}>
              Reopen
            </Button>
          ) : null}
        </div>
      </div>

      {transitionMessage ? (
        <p role="alert" className="text-xs font-medium text-destructive">
          {transitionMessage}
        </p>
      ) : null}

      {showEdit ? (
        <EditTermDialog sessionId={sessionId} term={term} onClose={() => setShowEdit(false)} />
      ) : null}
      {showReopen ? (
        <ReopenTermDialog sessionId={sessionId} term={term} onClose={() => setShowReopen(false)} />
      ) : null}
    </div>
  );
}
