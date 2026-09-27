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

/** What a tile shows: loading, failed, a sentence instead of a figure (nothing to report), or its figure. */
type TileState = 'loading' | 'failed' | 'empty' | 'ready';

/** One figure with its context and where to go next. Loads and fails on its own, so one slow report never blanks the page. */
function Tile({
  title,
  to,
  state,
  empty,
  value,
  children,
}: {
  title: string;
  to: string;
  state: TileState;
  empty?: string;
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
      {state === 'loading' ? <Spinner className="size-5" /> : null}
      {state === 'failed' ? <p className="text-sm text-destructive">Could not load this. Open it to try again.</p> : null}
      {state === 'empty' ? <p className="text-sm text-muted-foreground">{empty}</p> : null}
      {state === 'ready' ? (
        <>
          {value !== undefined ? <p className="font-display text-3xl font-semibold text-foreground tabular-nums">{value}</p> : null}
          {children}
        </>
      ) : null}
    </section>
  );
}

/** A query's tile state; a query switched off for want of a session or term is "empty", never an endless spinner. */
function stateOf(query: { isLoading: boolean; isError: boolean; isSuccess: boolean }, needs: { pending: boolean; failed: boolean; missing: boolean }): TileState {
  if (needs.pending) return 'loading';
  if (needs.failed) return 'failed';
  if (needs.missing) return 'empty';
  if (query.isError) return 'failed';
  return query.isSuccess ? 'ready' : 'loading';
}

const dataRows = (report: ReportDto | undefined) => report?.rows.filter((row) => row.kind === 'Data') ?? [];

const listed = (names: string[], shown = 4) =>
  names.slice(0, shown).join(', ') + (names.length > shown ? ` and ${names.length - shown} more` : '');

/**
 * The head teacher's dashboard (spec 15; spec 20's illness marker). School-wide figures, so it shows only to a holder of
 * `report.view` over the whole school: a class-scoped holder would see their classes' numbers under school-wide headings.
 */
export function Dashboard({ session }: { session: AuthSession }) {
  const schoolWide =
    session.isSuperAdmin || session.effectivePrivileges.some((grant) => grant.privilege === 'report.view' && grant.scope === 'SchoolWide');
  return schoolWide ? (
    <Tiles illness={hasPrivilege(session, 'pupil.safeguarding.view')} />
  ) : (
    <p className="text-sm text-muted-foreground">Choose a page from the menu to begin.</p>
  );
}

/**
 * The tiles: this term's results by state and the classes still entering marks, the roll, admissions waiting, records to
 * chase, and pupils with illness noted on several days. Every tile reads an existing report and links to it. Between terms
 * the term is the latest one that has begun, not the first.
 */
function Tiles({ illness }: { illness: boolean }) {
  const term = useTermChoice();
  const current =
    term.terms.find((candidate) => candidate.state === 'Active') ??
    [...term.terms].reverse().find((candidate) => candidate.state !== 'Upcoming') ??
    term.terms[0];
  const termId = current?.id ?? '';

  const progress = useReport('result-entry-progress', { termId }, termId !== '');
  const roll = useReport('enrolment-summary', { sessionId: term.sessionId }, term.sessionId !== '');
  const admissions = useReport('admissions-pipeline', {}, true);
  const records = useIncompleteRecords(true);
  const ill = useWeeklyIllness(termId, illness);

  const period = { pending: term.isPending, failed: term.isError };
  const classes = dataRows(progress.data);
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
  const spaceLeft = Number(total?.cells[5] ?? 0);
  const waiting = dataRows(admissions.data);
  const oldest = Math.max(0, ...waiting.map((row) => Number(row.cells[3] ?? 0)));
  const flagged = ill.data?.items ?? [];

  return (
    <div className="grid gap-3 sm:grid-cols-2 xl:grid-cols-3">
      <Tile
        title={`Results, ${current?.name ?? 'this term'}`}
        to={paths.report('result-entry-progress')}
        state={stateOf(progress, { ...period, missing: termId === '' })}
        empty="No term has been set up yet."
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
          <p className="text-sm text-warning">Still entering marks: {listed(entering.map((row) => row.cells[0] ?? ''))}</p>
        ) : null}
      </Tile>

      <Tile
        title="Pupils on roll"
        to={paths.report('enrolment-summary')}
        state={stateOf(roll, { ...period, missing: term.sessionId === '' })}
        empty="No session has been set up yet."
        value={total?.cells[3] ?? '0'}
      >
        <p className="text-sm text-muted-foreground">
          {total
            ? `${total.cells[1] ?? '0'} boys, ${total.cells[2] ?? '0'} girls; ` +
              (spaceLeft < 0 ? `over capacity by ${-spaceLeft}` : `${spaceLeft} ${spaceLeft === 1 ? 'place' : 'places'} left`)
            : 'No classes this session yet.'}
        </p>
      </Tile>

      <Tile
        title="Admissions waiting"
        to={paths.report('admissions-pipeline')}
        state={stateOf(admissions, { pending: false, failed: false, missing: false })}
        value={String(waiting.length)}
      >
        <p className="text-sm text-muted-foreground">
          {waiting.length === 0 ? 'Nothing pending.' : `The oldest has waited ${oldest} ${oldest === 1 ? 'day' : 'days'}.`}
        </p>
      </Tile>

      <Tile
        title="Records to chase"
        to={paths.incompleteRecords}
        state={stateOf(records, { pending: false, failed: false, missing: false })}
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
          state={stateOf(ill, { ...period, missing: termId === '' })}
          empty="No term has been set up yet."
          value={String(flagged.length)}
        >
          <ul className="flex flex-col gap-0.5 text-sm text-foreground">
            {flagged.slice(0, 4).map((item) => (
              <li key={item.pupilId}>
                {item.displayName} <span className="text-muted-foreground">({item.armName}, {item.observations.length} days)</span>
              </li>
            ))}
          </ul>
          {flagged.length > 4 ? <p className="text-sm text-muted-foreground">and {flagged.length - 4} more</p> : null}
        </Tile>
      ) : null}
    </div>
  );
}
