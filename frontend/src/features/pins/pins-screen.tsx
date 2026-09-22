import { useState } from 'react';
import { Link, useNavigate } from 'react-router';
import { paths } from '@/app/router/paths';
import { LoadingState, QueryErrorState } from '@/components/feedback/query-states';
import { Button } from '@/components/ui/button';
import { useMe } from '@/features/auth/api';
import { LabelledSelect } from '@/shared/pickers/labelled-select';
import { hasPrivilege } from '@/lib/auth/auth-session';
import { useTermChoice } from '@/shared/pickers/use-term-choice';
import { usePinBatches } from './api';
import { GenerateBatchDialog } from './components/generate-batch-dialog';

/** `/pins` — result-checking pin batches for a session (spec 6.8). */
export function PinsScreen() {
  const choice = useTermChoice();
  const batches = usePinBatches(choice.sessionId);
  const me = useMe();
  const navigate = useNavigate();
  const [generating, setGenerating] = useState(false);
  const canGenerate = !!me.data && hasPrivilege(me.data, 'pin.generate');

  const body = () => {
    if (choice.isPending) return <LoadingState label="Loading sessions…" />;
    if (!choice.sessionId) return <p className="text-sm text-muted-foreground">Create a session first.</p>;
    if (batches.isPending) return <LoadingState label="Loading pin batches…" />;
    if (batches.isError) return <QueryErrorState error={batches.error} onRetry={() => void batches.refetch()} />;
    const items = batches.data.pages.flatMap((page) => page.items);
    if (items.length === 0) return <p className="text-sm text-muted-foreground">No pins generated for this session yet.</p>;
    return (
      <ul className="flex flex-col gap-2">
        {items.map((batch) => (
          <li key={batch.id}>
            <Link
              to={paths.pinBatch(batch.id)}
              className="flex flex-wrap items-center justify-between gap-4 rounded-md border border-border bg-surface px-4 py-3 text-sm hover:bg-muted"
            >
              <span className="font-medium text-foreground">{batch.name}</span>
              <span className="text-muted-foreground">
                {batch.pinCount} pins · {batch.pinsUsed} used · {batch.state}
              </span>
            </Link>
          </li>
        ))}
      </ul>
    );
  };

  return (
    <div className="flex flex-col gap-6">
      <header className="flex flex-wrap items-center justify-between gap-4">
        <div className="flex flex-col gap-1">
          <h1 className="font-display text-2xl font-semibold text-foreground">Result pins</h1>
          <p className="text-sm text-muted-foreground">Pins parents use to check results. Any pin opens any pupil, up to its number of uses.</p>
        </div>
        {canGenerate && choice.sessionId ? <Button onClick={() => setGenerating(true)}>Generate pins</Button> : null}
      </header>
      <LabelledSelect
        label="Session"
        placeholder="Session"
        value={choice.sessionId}
        options={choice.sessions.map((session) => ({ value: session.id, label: session.name }))}
        onChange={choice.setSessionId}
        className="w-40"
      />
      {body()}
      {generating ? (
        <GenerateBatchDialog
          sessionId={choice.sessionId}
          onClose={() => setGenerating(false)}
          onCreated={(batch) => void navigate(paths.pinBatch(batch.id))}
        />
      ) : null}
    </div>
  );
}
