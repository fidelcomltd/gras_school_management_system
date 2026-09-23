import { useState } from 'react';
import { Link } from 'react-router';
import { LoadingState, QueryErrorState } from '@/components/feedback/query-states';
import { paths } from '@/app/router/paths';
import { useMe } from '@/features/auth/api';
import { hasPrivilege, hasPrivilegeInArm } from '@/lib/auth/auth-session';
import { LabelledSelect } from '@/shared/pickers/labelled-select';
import { TermPicker } from '@/shared/pickers/term-picker';
import { useClassChoice } from '@/shared/pickers/use-class-choice';
import { useTermChoice } from '@/shared/pickers/use-term-choice';
import { gridKey, useWeeklyGrid } from './api';
import { WeekEditor } from './components/week-editor';
import { weekLabel } from './types';

/** `/weekly` — weekly report sheets (spec 6.10): one arm, one week, entered line by line or pupil by pupil. */
export function WeeklyScreen() {
  const term = useTermChoice();
  const klass = useClassChoice(term.sessionId, 'weekly.view');
  const me = useMe();
  const scope = `${klass.armId}|${term.termId}`;
  const [weekChoice, setWeekChoice] = useState<{ scope: string; week: number } | null>(null);
  const week = weekChoice?.scope === scope ? weekChoice.week : null;
  const queryKey = gridKey(klass.armId, term.termId, week);
  const grid = useWeeklyGrid(klass.armId, term.termId, week);

  const body = () => {
    if (term.isPending || klass.isPending) return <LoadingState label="Loading classes…" />;
    if (!term.termId) return <p className="text-sm text-muted-foreground">Create a session first.</p>;
    if (!klass.armId) return <p className="text-sm text-muted-foreground">There are no classes you can see in this session.</p>;
    if (grid.isPending) return <LoadingState label="Loading the week…" />;
    if (grid.isError) return <QueryErrorState error={grid.error} onRetry={() => void grid.refetch()} />;
    if (grid.data.weeks.length === 0) return <p className="text-sm text-muted-foreground">This term has no weeks. Check its dates.</p>;
    return (
      <WeekEditor
        key={`${scope}|${grid.data.weekNumber}`}
        grid={grid.data}
        queryKey={queryKey}
        canEnter={!!me.data && hasPrivilegeInArm(me.data, 'weekly.enter', klass.armId)}
        canPublish={!!me.data && hasPrivilegeInArm(me.data, 'weekly.publish', klass.armId)}
        accountId={me.data?.accountId}
      />
    );
  };

  return (
    <div className="flex flex-col gap-6">
      <header className="flex flex-wrap items-end justify-between gap-2">
        <div className="flex flex-col gap-1">
          <h1 className="font-display text-2xl font-semibold text-foreground">Weekly reports</h1>
          <p className="text-sm text-muted-foreground">A short note per school day for each pupil. Nothing here is scored.</p>
        </div>
        {me.data && hasPrivilege(me.data, 'report.view') ? (
          <Link to={paths.weeklyCompletion} className="text-sm text-primary underline-offset-4 hover:underline">
            Completion report
          </Link>
        ) : null}
      </header>

      <div className="flex flex-wrap gap-3">
        <TermPicker choice={term} />
        <LabelledSelect
          label="Class"
          placeholder="Class"
          value={klass.armId}
          options={klass.arms.map((arm) => ({ value: arm.id, label: arm.displayName }))}
          onChange={klass.setArmId}
          className="w-40"
        />
        {grid.data ? (
          <LabelledSelect
            label="Week"
            placeholder="Week"
            value={String(grid.data.weekNumber)}
            options={grid.data.weeks.map((option) => ({
              value: String(option.weekNumber),
              label: `${weekLabel(option)}${option.published ? ' · published' : ''}${option.outsideTerm ? ' · outside term' : ''}`,
            }))}
            onChange={(value) => setWeekChoice({ scope, week: Number(value) })}
            className="w-96"
          />
        ) : null}
      </div>

      {body()}
    </div>
  );
}
