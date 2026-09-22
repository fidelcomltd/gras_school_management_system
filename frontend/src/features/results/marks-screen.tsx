import { useState } from 'react';
import { LoadingState, QueryErrorState } from '@/components/feedback/query-states';
import { Button } from '@/components/ui/button';
import { useMe } from '@/features/auth/api';
import { hasPrivilegeInArm } from '@/lib/auth/auth-session';
import { TermPicker } from '@/shared/pickers/term-picker';
import { useTermChoice } from '@/shared/pickers/use-term-choice';
import { useArmSubjects, useScoreSheet } from './api';
import { LabelledSelect } from '@/shared/pickers/labelled-select';
import { ScoreSheetEditor } from './components/score-sheet-editor';
import { VoidSheetDialog } from './components/void-sheet-dialog';
import { useClassChoice } from './hooks/use-class-choice';
import { isEditable } from './types';

/** `/results/marks` — mark entry per class, subject and term (spec 6.7.4). */
export function MarksScreen() {
  const term = useTermChoice();
  const klass = useClassChoice(term.sessionId, 'result.view');
  const subjects = useArmSubjects(klass.armId, term.termId);
  const [subjectChoice, setSubjectChoice] = useState<string | null>(null);
  const [voiding, setVoiding] = useState(false);
  const me = useMe();

  const subjectList = subjects.data ?? [];
  const subjectId =
    subjectChoice !== null && subjectList.some((s) => s.subjectId === subjectChoice) ? subjectChoice : (subjectList[0]?.subjectId ?? '');
  const subject = subjectList.find((s) => s.subjectId === subjectId);
  const sheet = useScoreSheet(klass.armId, subjectId, term.termId);
  const canEnter = !!me.data && hasPrivilegeInArm(me.data, 'result.score.enter', klass.armId);
  const canVoid = !!me.data && hasPrivilegeInArm(me.data, 'result.score.void', klass.armId);

  const body = () => {
    if (term.isPending || klass.isPending) return <LoadingState label="Loading classes…" />;
    if (!term.termId) return <p className="text-sm text-muted-foreground">Create a session first.</p>;
    if (klass.arms.length === 0) return <p className="text-sm text-muted-foreground">There are no classes you can enter marks for in this session.</p>;
    if (subjects.isPending) return <LoadingState label="Loading subjects…" />;
    if (subjects.isError) return <QueryErrorState error={subjects.error} onRetry={() => void subjects.refetch()} />;
    if (subjectList.length === 0) return <p className="text-sm text-muted-foreground">No subjects are mapped to this class for this term.</p>;
    if (sheet.isPending) return <LoadingState label="Loading marks…" />;
    if (sheet.isError) return <QueryErrorState error={sheet.error} onRetry={() => void sheet.refetch()} />;
    return (
      <>
        <ScoreSheetEditor key={`${sheet.data.armId}:${sheet.data.subjectId}:${sheet.data.termId}:${sheet.data.version ?? 'new'}`} sheet={sheet.data} canEdit={canEnter} />
        {canVoid && sheet.data.version && isEditable(sheet.data.resultSet?.state) ? (
          <div>
            <Button variant="ghost" size="sm" onClick={() => setVoiding(true)}>
              Void this sheet…
            </Button>
          </div>
        ) : null}
      </>
    );
  };

  return (
    <div className="flex flex-col gap-6">
      <header className="flex flex-col gap-1">
        <h1 className="font-display text-2xl font-semibold text-foreground">Marks</h1>
        <p className="text-sm text-muted-foreground">Enter continuous assessment and examination marks, one subject at a time.</p>
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
        <LabelledSelect
          label="Subject"
          placeholder="Subject"
          value={subjectId}
          options={subjectList.map((s) => ({ value: s.subjectId, label: s.subjectName }))}
          onChange={setSubjectChoice}
        />
      </div>

      {body()}

      {voiding && subject ? (
        <VoidSheetDialog armId={klass.armId} subjectId={subjectId} termId={term.termId} subjectName={subject.subjectName} onClose={() => setVoiding(false)} />
      ) : null}
    </div>
  );
}
