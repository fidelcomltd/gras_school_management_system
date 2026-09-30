import { useState } from 'react';
import { Link, useParams } from 'react-router';
import { Button } from '@/components/ui/button';
import { useMe } from '@/features/auth/api';
import { hasPrivilege } from '@/lib/auth/auth-session';
import { useSession } from './api';
import { EditSessionDialog } from './components/edit-session-dialog';
import { TermCard } from './components/term-card';
import { LoadingState } from '@/components/feedback/query-states';
import { PageTrail, TrailedError, WithTrail } from '@/components/layout/page-trail';
import { paths } from '@/app/router/paths';

/**
 * `/sessions/:id` — a session's own fields plus its three terms (spec 6.3.8,
 * 6.3.10). Four required states (CONVENTIONS.md §11) for the `GET
 * /sessions/{id}` fetch: loading, error, unauthorized, success below — no
 * distinct "empty" state, the same reasoning `SettingsScreen` documents for a
 * single-object read.
 */
export function SessionDetailScreen() {
  // `:id` is always present when this route matches (`sessions-routes.tsx`);
  // the fallback only avoids a conditional hook call, never a real request.
  const { id = '' } = useParams<{ id: string }>();
  const session = useSession(id);
  const me = useMe();
  const [showEdit, setShowEdit] = useState(false);

  if (session.isPending) {
    return <WithTrail trail={[{ to: paths.sessions }, { label: 'Session' }]}><LoadingState label="Loading session…" /></WithTrail>;
  }

  if (session.isError) {
    return <TrailedError trail={[{ to: paths.sessions }, { label: 'Session' }]} error={session.error} onRetry={() => void session.refetch()} />;
  }

  const detail = session.data;
  const canEditSession = !!me.data && hasPrivilege(me.data, 'session.update');
  const canOpenTerm = !!me.data && hasPrivilege(me.data, 'term.open');
  const canCloseTerm = !!me.data && hasPrivilege(me.data, 'term.close');
  const canReopenTerm = !!me.data && me.data.isSuperAdmin && hasPrivilege(me.data, 'term.close');
  const canPromote = !!me.data && hasPrivilege(me.data, 'promotion.run');
  const thirdTermClosed = detail.terms.some((term) => Number(term.ordinal) === 3 && term.state === 'Closed');

  return (
    <div className="flex flex-col gap-6">
      <PageTrail trail={[{ to: paths.sessions }, { label: detail.name }]} />
      <header className="flex flex-wrap items-center justify-between gap-4">
        <div className="flex flex-col gap-1">
          <h1 className="font-display text-2xl font-semibold text-foreground">{detail.name}</h1>
          <p className="text-sm text-muted-foreground">
            {detail.startDate} – {detail.endDate} · {detail.state}
          </p>
        </div>
        {canEditSession ? (
          <Button variant="outline" size="sm" onClick={() => setShowEdit(true)}>
            Edit session
          </Button>
        ) : null}
      </header>

      <ul className="flex flex-col gap-4">
        {detail.terms.map((term) => (
          <li key={term.id}>
            <TermCard
              sessionId={detail.id}
              term={term}
              canEdit={canEditSession}
              canOpen={canOpenTerm}
              canClose={canCloseTerm}
              canReopen={canReopenTerm}
              nextTermStart={detail.terms.find((next) => Number(next.ordinal) === Number(term.ordinal) + 1)?.startDate ?? null}
            />
          </li>
        ))}
      </ul>

      {canPromote ? (
        <section
          aria-labelledby="promotion-panel"
          className={thirdTermClosed ? 'rounded-lg border border-border bg-surface p-5' : 'rounded-lg border border-dashed border-border p-5 opacity-70'}
        >
          <h2 id="promotion-panel" className="font-semibold text-foreground">
            End-of-session promotion
          </h2>
          {thirdTermClosed ? (
            <div className="mt-2 flex flex-wrap items-center justify-between gap-3">
              <p className="text-sm text-muted-foreground">Move every active pupil into next session's classes, repeat or graduate them.</p>
              <Button render={<Link to={paths.sessionPromotion(detail.id)} />}>Review promotion</Button>
            </div>
          ) : (
            <p className="mt-2 text-sm text-muted-foreground">Promotion opens once Third Term is closed.</p>
          )}
        </section>
      ) : null}

      {showEdit ? (
        <EditSessionDialog session={detail} onClose={() => setShowEdit(false)} />
      ) : null}
    </div>
  );
}
