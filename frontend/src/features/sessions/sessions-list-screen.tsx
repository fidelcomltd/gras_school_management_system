import { useState } from 'react';
import { Link } from 'react-router';
import { paths } from '@/app/router/paths';
import { Button } from '@/components/ui/button';
import { useMe } from '@/features/auth/api';
import { hasPrivilege } from '@/lib/auth/auth-session';
import { ApiError } from '@/lib/http';
import { useSessions } from './api';
import { CreateSessionDialog } from './components/create-session-dialog';

/**
 * `/sessions` — the school-year list (spec 6.3.8), newest first. Four
 * required states (CONVENTIONS.md §11) for the `GET /sessions` fetch: loading,
 * empty, error, and unauthorized handled the same way `SettingsScreen` does.
 */
export function SessionsListScreen() {
  const sessions = useSessions();
  const me = useMe();
  const [showCreate, setShowCreate] = useState(false);
  const canCreate = !!me.data && hasPrivilege(me.data, 'session.create');

  if (sessions.isPending) {
    return <output className="text-sm text-muted-foreground">Loading sessions…</output>;
  }

  if (sessions.isError) {
    if (sessions.error instanceof ApiError && sessions.error.kind === 'unauthorized') {
      return null;
    }
    return (
      <div role="alert" className="flex flex-col items-start gap-3">
        <p className="text-sm text-destructive">{sessions.error.message}</p>
        <Button variant="outline" size="sm" onClick={() => void sessions.refetch()}>
          Try again
        </Button>
      </div>
    );
  }

  const items = sessions.data.pages.flatMap((page) => page.items);

  return (
    <div className="flex flex-col gap-6">
      <header className="flex flex-wrap items-center justify-between gap-4">
        <div className="flex flex-col gap-1">
          <h1 className="font-display text-2xl font-semibold text-foreground">Sessions</h1>
          <p className="text-sm text-muted-foreground">The school years and their three terms each.</p>
        </div>
        {canCreate ? <Button onClick={() => setShowCreate(true)}>New session</Button> : null}
      </header>

      {items.length === 0 ? (
        <p className="text-sm text-muted-foreground">No sessions yet.</p>
      ) : (
        <ul className="flex flex-col gap-2">
          {items.map((session) => (
            <li key={session.id}>
              <Link
                to={paths.sessionDetail(session.id)}
                className="flex items-center justify-between gap-4 rounded-md border border-border bg-surface px-4 py-3 text-sm hover:bg-muted"
              >
                <span className="font-medium text-foreground">{session.name}</span>
                <span className="text-muted-foreground">{session.state}</span>
              </Link>
            </li>
          ))}
        </ul>
      )}

      {sessions.hasNextPage ? (
        <Button
          variant="outline"
          size="sm"
          onClick={() => void sessions.fetchNextPage()}
          disabled={sessions.isFetchingNextPage}
        >
          {sessions.isFetchingNextPage ? 'Loading…' : 'Load more'}
        </Button>
      ) : null}

      {showCreate ? <CreateSessionDialog onClose={() => setShowCreate(false)} /> : null}
    </div>
  );
}
