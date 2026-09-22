import { LoadingState, QueryErrorState } from '@/components/feedback/query-states';
import { useMe } from '@/features/auth/api';
import { hasPrivilegeInArm } from '@/lib/auth/auth-session';
import { TermPicker } from '@/shared/pickers/term-picker';
import { useTermChoice } from '@/shared/pickers/use-term-choice';
import { useReadiness } from './api-workflow';
import { LabelledSelect } from '@/shared/pickers/labelled-select';
import { ReadinessGrid } from './components/readiness-grid';
import { WorkflowActions } from './components/workflow-actions';
import { useClassChoice } from './hooks/use-class-choice';
import { STATE_LABEL } from './types';

/** `/results` — a class's results for a term: how complete they are, and the next step (spec 6.7.5, 6.7.8–6.7.11). */
export function ProgressScreen() {
  const term = useTermChoice();
  const klass = useClassChoice(term.sessionId, 'result.view');
  const readiness = useReadiness(klass.armId, term.termId);
  const me = useMe();
  const can = (privilege: string) => !!me.data && hasPrivilegeInArm(me.data, privilege, klass.armId);

  const body = () => {
    if (term.isPending || klass.isPending) return <LoadingState label="Loading classes…" />;
    if (!term.termId) return <p className="text-sm text-muted-foreground">Create a session first.</p>;
    if (!klass.armId) return <p className="text-sm text-muted-foreground">There are no classes you can see in this session.</p>;
    if (readiness.isPending) return <LoadingState label="Loading results…" />;
    if (readiness.isError) return <QueryErrorState error={readiness.error} onRetry={() => void readiness.refetch()} />;

    const data = readiness.data;
    const set = data.resultSet;
    return (
      <div className="flex flex-col gap-5">
        <section aria-label="Status" className="flex flex-col gap-2 rounded-md border border-border bg-surface p-4">
          <p className="text-sm text-foreground">
            Status: <strong>{set ? STATE_LABEL[set.state] : 'Not started'}</strong>
          </p>
          {set?.needsRecompute ? (
            <p className="text-sm text-warning">Marks have changed since the last computation. Compute again before submitting.</p>
          ) : null}
          {set?.returnReason ? <p className="text-sm text-foreground">Returned: {set.returnReason}</p> : null}
          {data.blockers.length > 0 ? (
            <ul className="list-disc pl-5 text-sm text-muted-foreground">
              {data.blockers.map((blocker) => (
                <li key={blocker.code}>{blocker.message}</li>
              ))}
            </ul>
          ) : null}
          {set ? (
            <WorkflowActions
              armId={klass.armId}
              termId={term.termId}
              isThirdTerm={Number(term.term?.ordinal) === 3}
              resultSet={set}
              canSubmitNow={data.canSubmit}
              can={can}
            />
          ) : (
            <p className="text-sm text-muted-foreground">Results start when the first mark, rating or remark is saved.</p>
          )}
        </section>
        {data.pupils.length === 0 ? (
          <p className="text-sm text-muted-foreground">No active pupils in this class.</p>
        ) : (
          <ReadinessGrid readiness={data} />
        )}
      </div>
    );
  };

  return (
    <div className="flex flex-col gap-6">
      <header className="flex flex-col gap-1">
        <h1 className="font-display text-2xl font-semibold text-foreground">Results</h1>
        <p className="text-sm text-muted-foreground">How complete a class's results are, and taking them through approval to parents.</p>
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
      </div>
      {body()}
    </div>
  );
}
