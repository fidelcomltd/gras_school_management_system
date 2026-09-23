import { useState, type FormEvent } from 'react';
import { Button } from '@/components/ui/button';
import { cn } from '@/lib/utils/cn';
import { LabelledSelect } from '@/shared/pickers/labelled-select';
import type { WeeklyDraft } from '../hooks/use-weekly-draft';
import { PHRASE_KEY, WEEKLY_DAYS, fieldMeta, formatDate, type WeeklyDay, type WeeklyField, type WeeklyGridDto } from '../types';
import { EditedByOther, IllnessMarker } from './weekly-markers';

/**
 * The primary entry surface (spec 6.10.7): one line for the whole arm, pupils down the side, weekdays across. Offers the
 * account's own earlier phrases, Fill across (one pupil's note to the rest of their week) and Fill down (one note to
 * every pupil for a day).
 */
export function LineGrid({
  grid,
  field,
  draft,
  editable,
  accountId,
}: {
  grid: WeeklyGridDto;
  field: WeeklyField;
  draft: WeeklyDraft;
  editable: boolean;
  accountId: string | undefined;
}) {
  const meta = fieldMeta(field);
  const listId = `weekly-phrases-${field}`;
  const phrases = grid.phrases[PHRASE_KEY[field]];
  const [fillDay, setFillDay] = useState<WeeklyDay>('Monday');
  const [fillText, setFillText] = useState('');

  if (grid.rows.length === 0) return <p className="text-sm text-muted-foreground">No active pupils in this class.</p>;

  const fillDown = (event: FormEvent) => {
    event.preventDefault();
    if (fillText.trim() === '') return;
    draft.setMany(grid.rows.map((row) => ({ pupilId: row.pupilId, day: fillDay, field, value: fillText })));
    setFillText('');
    draft.flushSoon();
  };

  const fillAcross = (pupilId: string) => {
    const days = WEEKLY_DAYS.map((day) => ({ day, text: draft.value(pupilId, day, field) }));
    const source = days.findIndex((entry) => entry.text.trim() !== '');
    const text = days[source]?.text;
    if (source < 0 || text === undefined) return;
    draft.setMany(
      days
        .slice(source + 1)
        .filter((entry) => entry.text.trim() === '')
        .map((entry) => ({ pupilId, day: entry.day, field, value: text })),
    );
  };

  return (
    <div className="flex flex-col gap-4">
      <datalist id={listId}>
        {phrases.map((phrase) => (
          <option key={phrase} value={phrase}>
            {phrase}
          </option>
        ))}
      </datalist>

      {editable ? (
        <form onSubmit={fillDown} className="flex flex-wrap items-center gap-2" aria-label="Fill down">
          <span className="text-sm font-medium text-foreground">Fill down</span>
          <LabelledSelect
            label="Day to fill"
            placeholder="Day"
            value={fillDay}
            options={WEEKLY_DAYS.map((day) => ({ value: day, label: day }))}
            onChange={(value) => setFillDay(value as WeeklyDay)}
            className="w-36"
          />
          <input
            aria-label={`${meta.label} for every pupil`}
            list={listId}
            maxLength={meta.max}
            placeholder="e.g. Class went on excursion"
            className="h-9 w-72 rounded-md border border-input bg-background px-2 text-sm"
            value={fillText}
            onChange={(event) => setFillText(event.target.value)}
          />
          <Button type="submit" variant="outline" size="sm" disabled={fillText.trim() === ''}>
            Apply to every pupil
          </Button>
        </form>
      ) : null}

      <div className="overflow-x-auto">
        <table className="w-full min-w-[56rem] text-sm">
          <caption className="sr-only">{meta.label} for each pupil, Monday to Friday</caption>
          <thead className="text-muted-foreground">
            <tr>
              <th scope="col" className="py-2 text-left font-medium">Pupil</th>
              {grid.rows[0]?.days.map((day) => (
                <th key={day.dayOfWeek} scope="col" className="py-2 font-medium">
                  {day.dayOfWeek} <span className="font-normal">{formatDate(day.date).slice(0, 5)}</span>
                </th>
              ))}
              {editable ? (
                <th scope="col" className="py-2 font-medium">
                  <span className="sr-only">Fill across</span>
                </th>
              ) : null}
            </tr>
          </thead>
          <tbody>
            {grid.rows.map((row) => (
              <tr key={row.pupilId} className="border-t border-border align-top">
                <th scope="row" className="py-1.5 pr-2 text-left font-normal text-foreground">
                  <span className="block">{row.displayName}</span>
                  <IllnessMarker days={Number(row.illnessDays)} />
                  {row.onRoll ? null : <span className="block text-xs text-muted-foreground">Has left this class</span>}
                </th>
                {row.days.map((panel) => {
                  const day = panel.dayOfWeek;
                  const tooLong = draft.isTooLong(row.pupilId, day, field);
                  return (
                    <td key={day} className="px-1 py-1.5">
                      <input
                        aria-label={`${meta.label}, ${day}, ${row.displayName}`}
                        aria-invalid={tooLong}
                        list={listId}
                        disabled={!editable}
                        className={cn(
                          'h-9 w-full min-w-32 rounded-md border border-input bg-background px-2',
                          tooLong && 'border-destructive',
                        )}
                        value={draft.value(row.pupilId, day, field)}
                        onChange={(event) => draft.set(row.pupilId, day, field, event.target.value)}
                        onBlur={draft.flush}
                      />
                      {tooLong ? <span className="text-xs text-destructive">At most {meta.max} characters.</span> : null}
                      <EditedByOther day={panel} accountId={accountId} />
                    </td>
                  );
                })}
                {editable ? (
                  <td className="py-1.5">
                    <Button variant="ghost" size="sm" aria-label={`Fill across for ${row.displayName}`} onClick={() => fillAcross(row.pupilId)}>
                      Fill across
                    </Button>
                  </td>
                ) : null}
              </tr>
            ))}
          </tbody>
        </table>
      </div>
    </div>
  );
}
