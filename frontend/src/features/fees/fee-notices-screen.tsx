import { useState } from 'react';
import { LoadingState } from '@/components/feedback/query-states';
import { PageTrail } from '@/components/layout/page-trail';
import { Button } from '@/components/ui/button';
import { paths } from '@/app/router/paths';
import { TermPicker } from '@/shared/pickers/term-picker';
import { useTermChoice } from '@/shared/pickers/use-term-choice';
import { FeeGridPanel } from './fee-grid-panel';
import { OutstandingPanel } from './outstanding-panel';

type Tab = 'lines' | 'outstanding';

/**
 * `/fees` — spec 6.2.13's next-term fee notice: the section's lines and amounts for a term, and each class's per-pupil
 * outstanding figures. A printed notice, not a finance module: nothing here invoices, receipts or blocks anything.
 */
export function FeeNoticesScreen() {
  const choice = useTermChoice();
  const [tab, setTab] = useState<Tab>('lines');

  return (
    <div className="flex flex-col gap-6">
      <PageTrail trail={[{ to: paths.feeNotices }]} />
      <header className="flex flex-col gap-1">
        <h1 className="font-display text-2xl font-semibold text-foreground">Fee notices</h1>
        <p className="text-sm text-muted-foreground">
          The next-term fees block printed at the foot of each result sheet. Choose the term whose sheets carry it.
        </p>
      </header>
      <TermPicker choice={choice} />
      <nav aria-label="Fee notice sections" className="flex gap-2 border-b border-border pb-2">
        <Button variant={tab === 'lines' ? 'secondary' : 'ghost'} size="sm" aria-pressed={tab === 'lines'} onClick={() => setTab('lines')}>
          Fee lines
        </Button>
        <Button
          variant={tab === 'outstanding' ? 'secondary' : 'ghost'}
          size="sm"
          aria-pressed={tab === 'outstanding'}
          onClick={() => setTab('outstanding')}
        >
          Outstanding figures
        </Button>
      </nav>
      {choice.isPending ? <LoadingState label="Loading terms…" /> : null}
      {!choice.isPending && choice.termId === '' ? (
        <p className="text-sm text-muted-foreground">Create a session and its terms first.</p>
      ) : null}
      {choice.termId !== '' && tab === 'lines' ? <FeeGridPanel key={choice.termId} termId={choice.termId} /> : null}
      {choice.termId !== '' && tab === 'outstanding' ? (
        <OutstandingPanel key={choice.termId} sessionId={choice.sessionId} termId={choice.termId} />
      ) : null}
    </div>
  );
}
