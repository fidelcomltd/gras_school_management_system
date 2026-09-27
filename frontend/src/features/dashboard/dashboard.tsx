import type { ReactNode } from 'react';
import { ChevronRight } from 'lucide-react';
import { Link } from 'react-router';
import { paths } from '@/app/router/paths';
import { Spinner } from '@/components/ui/spinner';
import { useIncompleteRecords } from '@/features/pupils/incomplete/api';
import { useReport } from '@/features/reports/api';
import type { ReportDto } from '@/features/reports/definitions';
import { useWeeklyIllness } from '@/features/weekly/api';
import { hasPrivilege, type AuthSession } from '@/lib/auth/auth-session';
import { useTermChoice } from '@/shared/pickers/use-term-choice';

/** One figure with its context and where to go next. Loads and fails on its own, so one slow report never blanks the page. */
function Tile({
  title,
  to,
  pending,
  failed,
  value,
  children,
}: {
  title: string;
  to: string;
  pending: boolean;
  failed: boolean;
  value?: string | undefined;
  children?: ReactNode;
}) {
  return (
    <section aria-label={title} className="flex flex-col gap-2 rounded-md border border-border bg-surface p-4">
      <div className="flex items-center justify-between gap-2">
        <h2 className="text-sm font-semibold text-muted-foreground">{title}</h2>
        <Link to={to} className="flex items-center gap-1 text-xs font-medium text-primary hover:underline">
          Open
          <ChevronRight aria-hidden="true" className="size-3" />
        </Link>
      </div>
      {pending ? (
        <Spinner className="size-5" />
      ) : failed ? (
        <p className="text-sm text-destructive">Could not load this. Open it to try again.</p>
      ) : (
        <>
          {value !== undefined ? <p className="font-display text-3xl font-semibold text-foreground tabular-nums">{value}</p> : null}
          {children}
        </>
      )}
    </section>
  );
}

const count = (report: ReportDto | undefined) => report?.rows.filter((row) => row.kind === 'Data') ?? [];

/**
 * The head teacher's dashboard (spec 15; spec 20's illness marker): this term's results by state and the classes still
 * entering marks, the school's roll, admissions waiting, records to chase, and pupils with illness noted on several days.
 * Every tile reads an existing report, shows only to a holder of that report's privileges, and links to the full report.
 */
export function Dashboard({ session }: { session: AuthSession }) {
  const can = (privilege: string) => hasPrivilege(session, privilege);
  const term = useTermChoice();
  const reports = can('report.view');
  const illness = reports && can('pupil.safeguarding.view');

  const progress = useReport('result-entry-progress', { termId: term.termId }, reports && term.termId !== '');
  const roll = useReport('enrolment-summary', { sessionId: term.sessionId }, reports && term.sessionId !== '');
  const admissions = useReport('admissions-pipeline', {}, reports);
  const records = useIncompleteRecords(reports);
  const ill = useWeeklyIllness(term.termId, illness);

  if (!reports) {
    return <p className="text-sm text-muted-foreground">Choose a page from the menu to begin.</p>;
  }

  const classes = count(progress.data);
  const byState = classes.reduce<Record<string, number>>((tally, row) => {
    const state = row.cells[8] ?? 'Not started';
    tally[state] = (tally[state] ?? 0) + 1;
    return tally;
  }, {});
  // "x of y" mark cells: a class is still entering marks while x is short of y.
  const entering = classes.filter((row) => {
    const [done, total] = (row.cells[3] ?? '').split(' of ').map(Number);
    return done !== undefined && total !== undefined && done < total;
  });
  const total = roll.data?.rows.find((row) => row.kind === 'Total');
  const waiting = count(admissions.data);
  const oldest = Math.max(0, ...waiting.map((row) => Number(row.cells[3] ?? 0)));

  return (
    <div className="grid gap-3 sm:grid-cols-2 xl:grid-cols-3">
      <Tile
        title={`Results, ${term.term?.name ?? 'this term'}`}
        to={paths.report('result-entry-progress')}
        pending={term.isPending || progress.isPending}
        failed={progress.isError}
        value={`${byState['Published'] ?? 0} of ${classes.length}`}
      >
        <p className="text-sm text-muted-foreground">classes published</p>
        <ul className="flex flex-wrap gap-x-3 gap-y-1 text-sm text-foreground">
          {Object.entries(byState)
            .filter(([state]) => state !== 'Published')
            .map(([state, number]) => (
              <li key={state}>
                {state}: <span className="font-medium tabular-nums">{number}</span>
              </li>
            ))}
        </ul>
        {entering.length > 0 ? (
          <p className="text-sm text-warning">
            Still entering marks: {entering.slice(0, 4).map((row) => row.cells[0]).join(', ')}
            {entering.length > 4 ? ` and ${entering.length - 4} more` : ''}
          </p>
        ) : null}
      </Tile>

      <Tile
        title="Pupils on roll"
        to={paths.report('enrolment-summary')}
        pending={term.isPending || roll.isPending}
        failed={roll.isError}
        value={total?.cells[3] ?? '0'}
      >
        <p className="text-sm text-muted-foreground">
          {total ? `${total.cells[1] ?? '0'} boys, ${total.cells[2] ?? '0'} girls; ${total.cells[5] ?? '0'} places left` : 'No classes this session yet.'}
        </p>
      </Tile>

      <Tile title="Admissions waiting" to={paths.admissions} pending={admissions.isPending} failed={admissions.isError} value={String(waiting.length)}>
        <p className="text-sm text-muted-foreground">
          {waiting.length === 0 ? 'Nothing pending.' : `The oldest has waited ${oldest} ${oldest === 1 ? 'day' : 'days'}.`}
        </p>
      </Tile>

      <Tile
        title="Records to chase"
        to={paths.incompleteRecords}
        pending={records.isPending}
        failed={records.isError}
        value={String(records.data?.pupils.length ?? 0)}
      >
        <p className="text-sm text-muted-foreground">
          {records.data ? `of ${records.data.pupilsChecked} active pupils have something missing` : null}
        </p>
      </Tile>

      {illness ? (
        <Tile
          title="Illness noted on several days"
          to={paths.weeklyCompletion}
          pending={term.isPending || ill.isPending}
          failed={ill.isError}
          value={String(ill.data?.items.length ?? 0)}
        >
          <ul className="flex flex-col gap-0.5 text-sm text-foreground">
            {(ill.data?.items ?? []).slice(0, 4).map((item) => (
              <li key={item.pupilId}>
                {item.displayName} <span className="text-muted-foreground">({item.armName}, {item.observations.length} days)</span>
              </li>
            ))}
          </ul>
        </Tile>
      ) : null}
    </div>
  );
}
