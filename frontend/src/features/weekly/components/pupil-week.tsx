import { useState } from 'react';
import { cn } from '@/lib/utils/cn';
import { LabelledSelect } from '@/shared/pickers/labelled-select';
import type { WeeklyDraft } from '../hooks/use-weekly-draft';
import { PHRASE_KEY, WEEKLY_FIELDS, formatDate, type WeeklyGridDto } from '../types';
import { EditedByOther, IllnessMarker } from './weekly-markers';

/**
 * The per-pupil tab (spec 6.10.7): one pupil's whole week as the paper form lays it out, five day panels of eight lines.
 * Writes the same cells as the line grid, through the same draft.
 */
export function PupilWeek({
  grid,
  draft,
  editable,
  accountId,
}: {
  grid: WeeklyGridDto;
  draft: WeeklyDraft;
  editable: boolean;
  accountId: string | undefined;
}) {
  const [choice, setChoice] = useState<string | null>(null);
  const row = grid.rows.find((candidate) => candidate.pupilId === choice) ?? grid.rows[0];

  if (!row) return <p className="text-sm text-muted-foreground">No active pupils in this class.</p>;

  return (
    <div className="flex flex-col gap-4">
      {WEEKLY_FIELDS.map((meta) => (
        <datalist key={meta.field} id={`weekly-pupil-phrases-${meta.field}`}>
          {grid.phrases[PHRASE_KEY[meta.field]].map((phrase) => (
            <option key={phrase} value={phrase}>
              {phrase}
            </option>
          ))}
        </datalist>
      ))}
      <div className="flex flex-wrap items-center gap-3">
        <LabelledSelect
          label="Pupil"
          placeholder="Pupil"
          value={row.pupilId}
          options={grid.rows.map((candidate) => ({ value: candidate.pupilId, label: candidate.displayName }))}
          onChange={setChoice}
          className="w-64"
        />
        <IllnessMarker days={Number(row.illnessDays)} />
      </div>

      <div className="grid gap-4 md:grid-cols-2 xl:grid-cols-3">
        {row.days.map((panel) => (
          <section key={panel.dayOfWeek} aria-label={`${panel.dayOfWeek} ${formatDate(panel.date)}`} className="rounded-lg border border-border p-3">
            <h3 className="mb-2 flex items-baseline justify-between text-sm font-semibold text-foreground">
              {panel.dayOfWeek}
              <span className="font-normal text-muted-foreground">
                {formatDate(panel.date)} · Week {Number(grid.weekNumber)}
              </span>
            </h3>
            <div className="flex flex-col gap-2">
              {WEEKLY_FIELDS.map((meta) => {
                const tooLong = draft.isTooLong(row.pupilId, panel.dayOfWeek, meta.field);
                const id = `weekly-${row.pupilId}-${panel.dayOfWeek}-${meta.field}`;
                return (
                  <label key={meta.field} htmlFor={id} className="flex flex-col gap-0.5 text-xs text-muted-foreground">
                    {meta.label}
                    <input
                      id={id}
                      list={`weekly-pupil-phrases-${meta.field}`}
                      aria-invalid={tooLong}
                      disabled={!editable}
                      className={cn('h-8 rounded-md border border-input bg-background px-2 text-sm text-foreground', tooLong && 'border-destructive')}
                      value={draft.value(row.pupilId, panel.dayOfWeek, meta.field)}
                      onChange={(event) => draft.set(row.pupilId, panel.dayOfWeek, meta.field, event.target.value)}
                      onBlur={draft.flush}
                    />
                    {tooLong ? <span className="text-destructive">At most {meta.max} characters.</span> : null}
                  </label>
                );
              })}
            </div>
            <EditedByOther day={panel} accountId={accountId} />
          </section>
        ))}
      </div>
    </div>
  );
}
