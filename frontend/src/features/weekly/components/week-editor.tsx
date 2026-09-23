import { useState } from 'react';
import { FormError } from '@/components/feedback/query-states';
import { Button } from '@/components/ui/button';
import { ApiError } from '@/lib/http';
import { cn } from '@/lib/utils/cn';
import { LabelledSelect } from '@/shared/pickers/labelled-select';
import { useSetAutoPublish, useSetWeekPublished } from '../api';
import { useWeeklyDraft } from '../hooks/use-weekly-draft';
import { WEEKLY_FIELDS, weekLabel, type WeeklyField, type WeeklyGridDto } from '../types';
import { LineGrid } from './line-grid';
import { PupilWeek } from './pupil-week';

type View = 'line' | 'pupil';

/**
 * One arm's week: the two entry surfaces over one draft, the save status (spec 9.8.2's unsaved-work banner), and
 * publication (spec 6.10.8). Keyed by arm, term and week by its parent, so each week starts from the server's values.
 */
export function WeekEditor({
  grid,
  queryKey,
  canEnter,
  canPublish,
  accountId,
}: {
  grid: WeeklyGridDto;
  queryKey: readonly unknown[];
  canEnter: boolean;
  canPublish: boolean;
  accountId: string | undefined;
}) {
  const draft = useWeeklyDraft(grid, queryKey);
  const publication = useSetWeekPublished(grid.armId, grid.termId);
  const autoPublish = useSetAutoPublish(grid.armId, grid.termId);
  const [view, setView] = useState<View>('line');
  const [field, setField] = useState<WeeklyField>('Behaviour');
  const weekNumber = Number(grid.weekNumber);
  const current = grid.weeks.find((week) => Number(week.weekNumber) === weekNumber);

  const status = draft.saving
    ? 'Saving…'
    : draft.unsaved > 0
      ? `${draft.unsaved} unsaved ${draft.unsaved === 1 ? 'change' : 'changes'}.`
      : 'All changes saved.';

  return (
    <div className="flex flex-col gap-4">
      {grid.locked ? (
        <output className="block rounded-md bg-muted px-3 py-2 text-sm text-foreground">
          This term is closed, so its weekly notes can be read but no longer changed.
        </output>
      ) : null}
      {grid.outsideTerm ? (
        <output className="block rounded-md bg-accent/20 px-3 py-2 text-sm text-foreground">
          Week {weekNumber} falls outside the term's new dates. Its notes are kept.
        </output>
      ) : null}

      <div className="flex flex-wrap items-center justify-between gap-3">
        <div className="flex flex-col">
          <span className="text-sm font-medium text-foreground">{current ? weekLabel(current) : `Week ${weekNumber}`}</span>
          <span className="text-sm text-muted-foreground">{grid.published ? 'Published to parents.' : 'Not published. Parents cannot see it yet.'}</span>
        </div>
        {canPublish ? (
          <div className="flex flex-wrap items-center gap-3">
            <label className="flex items-center gap-2 text-sm text-foreground">
              <input
                type="checkbox"
                checked={grid.autoPublish}
                disabled={autoPublish.isPending}
                onChange={(event) => autoPublish.mutate(event.target.checked)}
              />
              Publish automatically at 17:00 on Fridays
            </label>
            <Button
              variant={grid.published ? 'outline' : 'primary'}
              size="sm"
              disabled={publication.isPending || draft.unsaved > 0}
              onClick={() => publication.mutate({ weekNumber, publish: !grid.published })}
            >
              {grid.published ? 'Unpublish week' : 'Publish week'}
            </Button>
          </div>
        ) : null}
      </div>
      <FormError message={publication.error instanceof ApiError ? publication.error.message : null} />
      <FormError message={autoPublish.error instanceof ApiError ? autoPublish.error.message : null} />

      <div className="flex flex-wrap items-end justify-between gap-3">
        <div role="tablist" aria-label="Entry view" className="flex gap-1 border-b border-border">
          {(
            [
              ['line', 'By line'],
              ['pupil', 'By pupil'],
            ] as const
          ).map(([id, label]) => (
            <button
              key={id}
              type="button"
              role="tab"
              id={`weekly-tab-${id}`}
              aria-selected={view === id}
              aria-controls="weekly-panel"
              className={cn(
                '-mb-px border-b-2 px-3 py-2 text-sm font-medium',
                view === id ? 'border-primary text-primary' : 'border-transparent text-muted-foreground hover:text-foreground',
              )}
              onClick={() => setView(id)}
            >
              {label}
            </button>
          ))}
        </div>
        {view === 'line' ? (
          <LabelledSelect
            label="Line"
            placeholder="Line"
            value={field}
            options={WEEKLY_FIELDS.map((meta) => ({ value: meta.field, label: meta.label }))}
            onChange={(value) => setField(value as WeeklyField)}
            className="w-52"
          />
        ) : null}
      </div>

      {draft.error ? (
        <div role="alert" className="flex flex-wrap items-center gap-3 rounded-md bg-destructive/10 px-3 py-2 text-sm text-destructive">
          Could not save: {draft.error}{' '}
          {draft.stalled ? 'Your notes are kept on this page. Correct them and they will save.' : 'Your notes are kept on this page and will be retried.'}
          <Button variant="outline" size="sm" onClick={draft.retry}>
            Retry now
          </Button>
        </div>
      ) : null}

      <div role="tabpanel" id="weekly-panel" aria-labelledby={`weekly-tab-${view}`}>
        {view === 'line' ? (
          <LineGrid grid={grid} field={field} draft={draft} editable={canEnter} accountId={accountId} />
        ) : (
          <PupilWeek grid={grid} draft={draft} editable={canEnter} accountId={accountId} />
        )}
      </div>

      {canEnter ? (
        <div className="flex items-center gap-3">
          <Button variant="outline" size="sm" disabled={draft.unsaved === 0 || draft.saving} onClick={draft.flush}>
            Save now
          </Button>
          <output aria-live="polite" className="text-sm text-muted-foreground">
            {status}
          </output>
        </div>
      ) : null}
    </div>
  );
}
