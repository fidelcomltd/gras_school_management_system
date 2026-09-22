import { useState } from 'react';
import { LoadingState } from '@/components/feedback/query-states';
import { useMe } from '@/features/auth/api';
import { useLevels, useSections } from '@/features/classes/api';
import { hasPrivilegeInArm } from '@/lib/auth/auth-session';
import { cn } from '@/lib/utils/cn';
import { TermPicker } from '@/shared/pickers/term-picker';
import { useTermChoice } from '@/shared/pickers/use-term-choice';
import { LabelledSelect } from '@/shared/pickers/labelled-select';
import { AttendancePanel, RatingsPanel, RemarksPanel } from './components/records-panels';
import { useClassChoice } from './hooks/use-class-choice';

const TABS = [
  { id: 'ratings', label: 'Ratings', privilege: 'result.trait.enter' },
  { id: 'attendance', label: 'Attendance', privilege: 'result.attendance.enter' },
  { id: 'teacher', label: "Teacher's remarks", privilege: 'result.remark.classteacher' },
  { id: 'head', label: "Head teacher's remarks", privilege: 'result.remark.headteacher' },
] as const;

type TabId = (typeof TABS)[number]['id'];

/** `/results/class` — everything recorded per pupil beside the marks (spec 6.7.7): ratings, attendance and the two remarks. */
export function ClassRecordsScreen() {
  const term = useTermChoice();
  const klass = useClassChoice(term.sessionId, 'result.view');
  const levels = useLevels();
  const sections = useSections();
  const me = useMe();
  const [tab, setTab] = useState<TabId>('ratings');

  const level = levels.data?.pages.flatMap((page) => page.items).find((l) => l.id === klass.arm?.classLevelId);
  const section = sections.data?.sections.find((s) => s.id === level?.sectionId);
  const can = (privilege: string) => !!me.data && hasPrivilegeInArm(me.data, privilege, klass.armId);
  const current = TABS.find((t) => t.id === tab) ?? TABS[0];

  const panel = () => {
    if (term.isPending || klass.isPending) return <LoadingState label="Loading classes…" />;
    if (!term.termId) return <p className="text-sm text-muted-foreground">Create a session first.</p>;
    if (!klass.armId) return <p className="text-sm text-muted-foreground">There are no classes you can see in this session.</p>;
    const canEdit = can(current.privilege);
    switch (tab) {
      case 'ratings':
        return section ? (
          <RatingsPanel armId={klass.armId} termId={term.termId} ratesTraits={section.ratesTraits} canEdit={canEdit} />
        ) : (
          <LoadingState label="Loading ratings…" />
        );
      case 'attendance':
        return <AttendancePanel armId={klass.armId} termId={term.termId} canEdit={canEdit} />;
      case 'teacher':
        return <RemarksPanel kind="ClassTeacher" armId={klass.armId} termId={term.termId} canEdit={canEdit} />;
      case 'head':
        return <RemarksPanel kind="HeadTeacher" armId={klass.armId} termId={term.termId} canEdit={canEdit} />;
    }
  };

  return (
    <div className="flex flex-col gap-6">
      <header className="flex flex-col gap-1">
        <h1 className="font-display text-2xl font-semibold text-foreground">Class records</h1>
        <p className="text-sm text-muted-foreground">Ratings, attendance and remarks for each pupil this term.</p>
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

      <div role="tablist" aria-label="Record" className="flex flex-wrap gap-1 border-b border-border">
        {TABS.map((t) => (
          <button
            key={t.id}
            type="button"
            role="tab"
            id={`tab-${t.id}`}
            aria-selected={tab === t.id}
            aria-controls="records-panel"
            className={cn(
              '-mb-px border-b-2 px-3 py-2 text-sm font-medium',
              tab === t.id ? 'border-primary text-primary' : 'border-transparent text-muted-foreground hover:text-foreground',
            )}
            onClick={() => setTab(t.id)}
          >
            {t.label}
          </button>
        ))}
      </div>

      <div role="tabpanel" id="records-panel" aria-labelledby={`tab-${tab}`}>
        {panel()}
      </div>
    </div>
  );
}
