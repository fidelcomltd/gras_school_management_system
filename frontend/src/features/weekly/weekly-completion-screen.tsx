import { useState } from 'react';
import { LoadingState, QueryErrorState } from '@/components/feedback/query-states';
import { useMe } from '@/features/auth/api';
import { hasPrivilege } from '@/lib/auth/auth-session';
import { TermPicker } from '@/shared/pickers/term-picker';
import { useTermChoice } from '@/shared/pickers/use-term-choice';
import { useWeeklyCompletion, useWeeklyIllness } from './api';
import { formatDate, weekLabel } from './types';

const lagosDateTime = (iso: string) =>
  new Date(iso).toLocaleString('en-GB', { day: '2-digit', month: '2-digit', hour: '2-digit', minute: '2-digit', timeZone: 'Africa/Lagos' });

/**
 * `/weekly/completion` — spec 6.10.12: which classes wrote what, per week, and (for safeguarding staff) the pupils with
 * symptoms noted on two or more days of the term.
 */
export function WeeklyCompletionScreen() {
  const term = useTermChoice();
  const me = useMe();
  const [weekFilter, setWeekFilter] = useState<{ termId: string; week: number } | null>(null);
  const week = weekFilter?.termId === term.termId ? weekFilter.week : null;
  const completion = useWeeklyCompletion(term.termId, week);
  const canSeeIllness = !!me.data && hasPrivilege(me.data, 'pupil.safeguarding.view');
  const illness = useWeeklyIllness(term.termId, canSeeIllness);

  const weeks = [...new Map((completion.data?.items ?? []).map((row) => [row.weekNumber, row])).values()];

  const table = () => {
    if (term.isPending || completion.isPending) return <LoadingState label="Loading the report…" />;
    if (completion.isError) return <QueryErrorState error={completion.error} onRetry={() => void completion.refetch()} />;
    if (completion.data.items.length === 0) return <p className="text-sm text-muted-foreground">No classes or weeks in this term.</p>;
    return (
      <div className="overflow-x-auto">
        <table className="w-full min-w-[48rem] text-sm">
          <thead className="text-muted-foreground">
            <tr>
              <th scope="col" className="py-2 text-left font-medium">Class</th>
              <th scope="col" className="py-2 text-left font-medium">Week</th>
              <th scope="col" className="py-2 font-medium">Pupils with notes</th>
              <th scope="col" className="py-2 font-medium">Lines filled</th>
              <th scope="col" className="py-2 font-medium">Published</th>
              <th scope="col" className="py-2 text-left font-medium">Last edited</th>
            </tr>
          </thead>
          <tbody>
            {completion.data.items.map((row) => (
              <tr key={`${row.armId}-${row.weekNumber}`} className="border-t border-border">
                <th scope="row" className="py-1.5 text-left font-normal text-foreground">{row.armName}</th>
                <td className="py-1.5">{weekLabel(row)}</td>
                <td className="py-1.5 text-center">
                  {row.pupilsWithNotes} of {row.pupilsOnRoll}
                </td>
                <td className="py-1.5 text-center">
                  {row.cellsFilled} of {row.cellsAvailable}
                </td>
                <td className="py-1.5 text-center">{row.published ? 'Yes' : 'No'}</td>
                <td className="py-1.5 text-muted-foreground">
                  {row.lastEditedAt ? `${row.lastEditedBy ?? 'Unknown'}, ${lagosDateTime(row.lastEditedAt)}` : 'Nothing written'}
                </td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
    );
  };

  return (
    <div className="flex flex-col gap-6">
      <header className="flex flex-col gap-1">
        <h1 className="font-display text-2xl font-semibold text-foreground">Weekly report completion</h1>
        <p className="text-sm text-muted-foreground">Which classes have written their weekly notes, week by week.</p>
      </header>

      <div className="flex flex-wrap items-center gap-3">
        <TermPicker choice={term} />
        <label className="flex items-center gap-2 text-sm text-foreground">
          Week
          <select
            className="h-9 rounded-md border border-input bg-background px-2"
            value={week ?? ''}
            onChange={(event) => setWeekFilter(event.target.value === '' ? null : { termId: term.termId, week: Number(event.target.value) })}
          >
            <option value="">All weeks</option>
            {(week === null ? weeks : [{ weekNumber: week, startDate: '', endDate: '' }]).map((option) => (
              <option key={option.weekNumber} value={option.weekNumber}>
                Week {option.weekNumber}
              </option>
            ))}
          </select>
        </label>
      </div>

      {table()}

      {canSeeIllness ? (
        <section aria-labelledby="illness-heading" className="flex flex-col gap-2">
          <h2 id="illness-heading" className="text-lg font-semibold text-foreground">
            Illness observations
          </h2>
          <p className="text-sm text-muted-foreground">Pupils with symptoms noted on two or more days this term. Restricted to safeguarding staff.</p>
          {illness.isPending ? (
            <LoadingState label="Loading observations…" />
          ) : illness.isError ? (
            <QueryErrorState error={illness.error} onRetry={() => void illness.refetch()} />
          ) : illness.data.items.length === 0 ? (
            <p className="text-sm text-muted-foreground">No pupil has symptoms noted on two or more days.</p>
          ) : (
            <ul className="flex flex-col gap-2">
              {illness.data.items.map((row) => (
                <li key={row.pupilId} className="rounded-md border border-border p-3 text-sm">
                  <span className="font-medium text-foreground">{row.displayName}</span>{' '}
                  <span className="text-muted-foreground">{row.armName}</span>
                  <ul className="mt-1 text-muted-foreground">
                    {row.observations.map((observation) => (
                      <li key={`${observation.date}-${observation.text}`}>
                        {formatDate(observation.date)}: {observation.text}
                      </li>
                    ))}
                  </ul>
                </li>
              ))}
            </ul>
          )}
        </section>
      ) : null}
    </div>
  );
}
