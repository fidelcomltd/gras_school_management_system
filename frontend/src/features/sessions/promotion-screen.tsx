import { useMemo, useState } from 'react';
import { useParams } from 'react-router';
import { GraduationCap, TriangleAlert } from 'lucide-react';
import { paths } from '@/app/router/paths';
import { EmptyState } from '@/components/feedback/empty-state';
import { LoadingState } from '@/components/feedback/query-states';
import { PageTrail, TrailedError, WithTrail } from '@/components/layout/page-trail';
import { Button } from '@/components/ui/button';
import { useMe } from '@/features/auth/api';
import { hasPrivilege } from '@/lib/auth/auth-session';
import { cn } from '@/lib/utils/cn';
import { CommitPromotionDialog, ReversePromotionDialog } from './components/promotion-dialogs';
import { usePromotionPreview } from './promotion-api';
import {
  assignedCounts,
  canChangeOutcome,
  destinationLevel,
  initialDrafts,
  OUTCOME_LABELS,
  outcomeChoices,
  rowProblem,
  withOutcome,
  type Drafts,
  type Outcome,
} from './promotion-decisions';
import type { PromotionBatchDto, PromotionPreviewDto, PromotionRowDto } from './types';

const selectClass = 'h-8 w-full min-w-32 rounded-md border border-input bg-surface px-2 text-sm text-foreground disabled:opacity-60';

/** `/sessions/:id/promotion` — spec 6.3.7's review screen, or the committed batch with its reversal. */
export function PromotionScreen() {
  const { id = '' } = useParams<{ id: string }>();
  const preview = usePromotionPreview(id);
  const trailFor = (name: string) => [{ to: paths.sessions }, { to: paths.sessionDetail(id), label: name }, { label: 'Promotion' }];

  if (preview.isPending) {
    return <WithTrail trail={trailFor('Session')}><LoadingState label="Loading promotion…" /></WithTrail>;
  }

  if (preview.isError) {
    return <TrailedError trail={trailFor('Session')} error={preview.error} onRetry={() => void preview.refetch()} />;
  }

  const data = preview.data;
  const target = data.targetSession?.name;
  return (
    <div className="flex flex-col gap-6">
      <PageTrail trail={trailFor(data.sourceSession.name)} />
      <header className="flex flex-col gap-1">
        <h1 className="font-display text-2xl font-semibold text-foreground">Promotion from {data.sourceSession.name}</h1>
        <p className="text-sm text-muted-foreground">
          {target ? `Into ${target}. ` : ''}Every active pupil moves to their next class, repeats, or graduates, in one step.
        </p>
      </header>
      {data.committedBatch ? (
        <CommittedBatch batch={data.committedBatch} />
      ) : (
        // Keyed so a refetch after a reversal starts the review afresh from the new proposals.
        <PromotionReview key={data.rows.map((row) => row.pupilId).join()} preview={data} />
      )}
    </div>
  );
}

function CommittedBatch({ batch }: { batch: PromotionBatchDto }) {
  const me = useMe();
  const canReverse = !!me.data && me.data.isSuperAdmin && hasPrivilege(me.data, 'promotion.reverse');
  const [reversing, setReversing] = useState(false);
  const tallies: [string, number][] = [
    ['Promoted', Number(batch.promoted)],
    ['Promoted on trial', Number(batch.promotedOnTrial)],
    ['Repeating', Number(batch.repeated)],
    ['Graduated', Number(batch.graduated)],
  ];

  return (
    <section className="flex flex-col gap-4 rounded-lg border border-border bg-surface p-5">
      <div className="flex flex-wrap items-start justify-between gap-3">
        <div>
          <h2 className="font-semibold text-foreground">Promotion committed into {batch.targetSessionName}</h2>
          <p className="text-sm text-muted-foreground">
            {new Date(batch.committedAtUtc).toLocaleString('en-NG', { timeZone: 'Africa/Lagos', dateStyle: 'medium', timeStyle: 'short' })}
          </p>
        </div>
        {canReverse ? (
          <Button variant="outline" onClick={() => setReversing(true)}>
            Reverse promotion
          </Button>
        ) : null}
      </div>
      <dl className="grid grid-cols-2 gap-3 sm:grid-cols-4">
        {tallies.map(([label, count]) => (
          <div key={label} className="rounded-md bg-muted/50 px-3 py-2">
            <dt className="text-xs text-muted-foreground">{label}</dt>
            <dd className="text-lg font-semibold text-foreground">{count}</dd>
          </div>
        ))}
      </dl>
      <p className="text-sm text-muted-foreground">
        Reversal is possible only until the first mark is entered or a result pin is used in {batch.targetSessionName}. After that,
        move individual pupils between arms instead.
      </p>
      {reversing ? <ReversePromotionDialog batch={batch} onClose={() => setReversing(false)} /> : null}
    </section>
  );
}

function PromotionReview({ preview }: { preview: PromotionPreviewDto }) {
  const [drafts, setDrafts] = useState<Drafts>(() => initialDrafts(preview));
  const [armFilter, setArmFilter] = useState('');
  const [committing, setCommitting] = useState(false);
  const counts = useMemo(() => assignedCounts(drafts), [drafts]);
  const currentArms = useMemo(
    () => [...new Map(preview.rows.map((row) => [row.currentArmId, row.currentArmName])).entries()],
    [preview.rows],
  );
  const rows = armFilter ? preview.rows.filter((row) => row.currentArmId === armFilter) : preview.rows;
  const problems = preview.rows.filter((row) => rowProblem(row, drafts[row.pupilId]) !== null).length;
  const blocked = preview.blockers.length > 0;
  const overCapacity = preview.targetArms.filter((arm) => Number(arm.enrolledCount) + (counts.get(arm.armId) ?? 0) > Number(arm.capacity));

  const setOutcome = (row: PromotionRowDto, outcome: Outcome) =>
    setDrafts((current) => withOutcome(current, row, outcome, preview.targetArms));
  const setField = (pupilId: string, field: 'targetArmId' | 'reason', value: string) =>
    setDrafts((current) => {
      const draft = current[pupilId] ?? { outcome: null, targetArmId: null, reason: '' };
      return { ...current, [pupilId]: { ...draft, [field]: value } };
    });

  return (
    <div className="flex flex-col gap-5">
      {blocked ? (
        <section role="alert" className="flex flex-col gap-2 rounded-lg border border-destructive/40 bg-destructive/10 p-4 text-sm text-destructive">
          <h2 className="flex items-center gap-2 font-semibold">
            <TriangleAlert className="size-4" aria-hidden="true" /> Promotion cannot run yet
          </h2>
          <ul className="list-disc pl-5">
            {preview.blockers.map((blocker) => (
              <li key={`${blocker.code}:${blocker.message}`}>{blocker.message}</li>
            ))}
          </ul>
        </section>
      ) : null}

      {preview.targetArms.length > 0 ? (
        <section aria-labelledby="destination-arms" className="flex flex-col gap-2">
          <h2 id="destination-arms" className="text-sm font-semibold text-foreground">
            Destination arms
          </h2>
          <ul className="flex flex-wrap gap-2">
            {preview.targetArms.map((arm) => {
              const total = Number(arm.enrolledCount) + (counts.get(arm.armId) ?? 0);
              const over = total > Number(arm.capacity);
              return (
                <li
                  key={arm.armId}
                  className={cn('rounded-md border px-3 py-1.5 text-sm', over ? 'border-warning bg-warning-subtle text-warning' : 'border-border text-foreground')}
                >
                  {arm.name}: {total} / {arm.capacity}
                  {over ? <span className="sr-only"> (over capacity)</span> : null}
                </li>
              );
            })}
          </ul>
          {overCapacity.length > 0 ? (
            <p className="text-sm text-warning">Over capacity: {overCapacity.map((arm) => arm.name).join(', ')}. You can still commit.</p>
          ) : null}
        </section>
      ) : null}

      {preview.rows.length === 0 ? (
        <EmptyState icon={GraduationCap} title="No active pupils to promote." />
      ) : (
        <>
          <div className="flex flex-wrap items-center justify-between gap-3">
            <label className="flex items-center gap-2 text-sm text-foreground">
              Class
              <select className={cn(selectClass, 'w-auto')} value={armFilter} onChange={(event) => setArmFilter(event.target.value)}>
                <option value="">All classes ({preview.rows.length})</option>
                {currentArms.map(([armId, name]) => (
                  <option key={armId} value={armId}>
                    {name}
                  </option>
                ))}
              </select>
            </label>
            <div className="flex items-center gap-3">
              {problems > 0 && !blocked ? (
                <span className="text-sm text-muted-foreground">
                  {problems} {problems === 1 ? 'row needs' : 'rows need'} attention
                </span>
              ) : null}
              <Button disabled={blocked || problems > 0} onClick={() => setCommitting(true)}>
                Commit promotion
              </Button>
            </div>
          </div>

          <div className="overflow-x-auto rounded-lg border border-border">
            <table className="w-full min-w-240 text-left text-sm">
              <thead className="bg-muted/50 text-xs text-muted-foreground uppercase">
                <tr>
                  <th scope="col" className="px-3 py-2 font-semibold">Pupil</th>
                  <th scope="col" className="px-3 py-2 font-semibold">Current arm</th>
                  <th scope="col" className="px-3 py-2 font-semibold">Average</th>
                  {preview.coreSubjects.map((subject) => (
                    <th key={subject.subjectId} scope="col" className="px-3 py-2 font-semibold">
                      {subject.name}
                    </th>
                  ))}
                  <th scope="col" className="px-3 py-2 font-semibold">Proposed</th>
                  <th scope="col" className="px-3 py-2 font-semibold">Outcome</th>
                  <th scope="col" className="px-3 py-2 font-semibold">Target arm</th>
                </tr>
              </thead>
              <tbody>
                {rows.map((row) => {
                  const draft = drafts[row.pupilId];
                  const level = destinationLevel(row, draft?.outcome ?? null);
                  const arms = preview.targetArms.filter((arm) => arm.classLevelId === level);
                  const problem = rowProblem(row, draft);
                  return (
                    <tr key={row.pupilId} className="border-t border-border align-top">
                      <th scope="row" className="px-3 py-2 font-medium text-foreground">
                        {row.displayName}
                        <span className="block text-xs font-normal text-muted-foreground">{row.registrationNumber}</span>
                      </th>
                      <td className="px-3 py-2">{row.currentArmName}</td>
                      <td className="px-3 py-2">{row.annualAverage ?? <span className="text-warning">No annual result</span>}</td>
                      {row.coreResults.map((result) => (
                        <td key={result.subjectId} className={cn('px-3 py-2', result.passed === false && 'text-destructive')}>
                          {result.mean ?? '–'}
                        </td>
                      ))}
                      <td className="px-3 py-2">{row.proposedOutcome ? OUTCOME_LABELS[row.proposedOutcome] : '–'}</td>
                      <td className="px-3 py-2">
                        <select
                          aria-label={`Outcome for ${row.displayName}`}
                          className={selectClass}
                          value={draft?.outcome ?? ''}
                          disabled={blocked || !canChangeOutcome(row, preview.canDecide)}
                          onChange={(event) => setOutcome(row, event.target.value as Outcome)}
                        >
                          {draft?.outcome ? null : <option value="">Choose…</option>}
                          {outcomeChoices(row, preview.canDecide).map((outcome) => (
                            <option key={outcome} value={outcome}>
                              {OUTCOME_LABELS[outcome]}
                            </option>
                          ))}
                        </select>
                        {draft?.outcome === 'PromotedOnTrial' ? (
                          <input
                            aria-label={`Reason for promoting ${row.displayName} on trial`}
                            placeholder="Reason (required)"
                            maxLength={500}
                            className="mt-2 h-8 w-full rounded-md border border-input bg-surface px-2 text-sm"
                            value={draft.reason}
                            onChange={(event) => setField(row.pupilId, 'reason', event.target.value)}
                          />
                        ) : null}
                        {problem && !blocked ? (
                          <span className="mt-1 block text-xs text-destructive">{problem}</span>
                        ) : null}
                      </td>
                      <td className="px-3 py-2">
                        {level === null ? (
                          <span className="text-muted-foreground">{draft?.outcome === 'Graduated' ? 'Leaves the school' : '–'}</span>
                        ) : (
                          <select
                            aria-label={`Target arm for ${row.displayName}`}
                            className={selectClass}
                            value={draft?.targetArmId ?? ''}
                            disabled={blocked}
                            onChange={(event) => setField(row.pupilId, 'targetArmId', event.target.value)}
                          >
                            {draft?.targetArmId ? null : <option value="">Choose…</option>}
                            {arms.map((arm) => (
                              <option key={arm.armId} value={arm.armId}>
                                {arm.name}
                              </option>
                            ))}
                          </select>
                        )}
                      </td>
                    </tr>
                  );
                })}
              </tbody>
            </table>
          </div>
        </>
      )}

      {preview.excluded.length > 0 ? (
        <details className="rounded-lg border border-border p-4 text-sm">
          <summary className="cursor-pointer font-medium text-foreground">
            {preview.excluded.length} {preview.excluded.length === 1 ? 'pupil is' : 'pupils are'} no longer active and will not be promoted
          </summary>
          <ul className="mt-3 flex flex-col gap-1 text-muted-foreground">
            {preview.excluded.map((pupil) => (
              <li key={pupil.pupilId}>
                {pupil.displayName} {pupil.registrationNumber ? `(${pupil.registrationNumber})` : ''}: {pupil.status}
              </li>
            ))}
          </ul>
        </details>
      ) : null}

      {committing ? (
        <CommitPromotionDialog preview={preview} drafts={drafts} overCapacity={overCapacity} onClose={() => setCommitting(false)} />
      ) : null}
    </div>
  );
}
