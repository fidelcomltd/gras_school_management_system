import { useState } from 'react';
import { cn } from '@/lib/utils/cn';
import { LevelList } from './components/level-list';
import { SectionList } from './components/section-list';

type Tab = 'levels' | 'sections';

/**
 * `/classes` — levels and sections share one screen (see `types.ts`'s header
 * comment for why they share a folder). Local tab state only; no separate
 * routes, per CONVENTIONS.md's "client state is Zustand or local `useState`".
 */
export function ClassesScreen() {
  const [tab, setTab] = useState<Tab>('levels');

  return (
    <div className="flex flex-col gap-6">
      <header className="flex flex-col gap-1">
        <h1 className="font-display text-2xl font-semibold text-foreground">Classes</h1>
        <p className="text-sm text-muted-foreground">
          The progression chain of levels, and the sections they belong to.
        </p>
      </header>

      <div role="tablist" aria-label="Classes" className="flex gap-1 border-b border-border">
        <TabButton active={tab === 'levels'} onClick={() => setTab('levels')}>
          Levels
        </TabButton>
        <TabButton active={tab === 'sections'} onClick={() => setTab('sections')}>
          Sections
        </TabButton>
      </div>

      {tab === 'levels' ? <LevelList /> : <SectionList />}
    </div>
  );
}

function TabButton({
  active,
  onClick,
  children,
}: {
  active: boolean;
  onClick: () => void;
  children: string;
}) {
  return (
    <button
      type="button"
      role="tab"
      aria-selected={active}
      onClick={onClick}
      className={cn(
        '-mb-px border-b-2 px-3 py-2 text-sm font-medium transition-colors',
        active
          ? 'border-primary text-primary'
          : 'border-transparent text-muted-foreground hover:text-foreground',
      )}
    >
      {children}
    </button>
  );
}
