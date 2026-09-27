import { useState } from 'react';
import { Download } from 'lucide-react';
import { useParams, useSearchParams } from 'react-router';
import { paths } from '@/app/router/paths';
import { FormError, LoadingState, QueryErrorState } from '@/components/feedback/query-states';
import { NotFoundScreen } from '@/components/feedback/not-found-screen';
import { PageTrail } from '@/components/layout/page-trail';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { useMe } from '@/features/auth/api';
import { hasPrivilege } from '@/lib/auth/auth-session';
import { ApiError } from '@/lib/http';
import { LabelledSelect } from '@/shared/pickers/labelled-select';
import { TermPicker } from '@/shared/pickers/term-picker';
import { useClassChoice } from '@/shared/pickers/use-class-choice';
import { useTermChoice } from '@/shared/pickers/use-term-choice';
import { useExportReport, useReport, type ReportParams } from './api';
import { tableReport, type ReportControl, type TableReport } from './definitions';
import { ReportTable } from './report-table';

/** The optional select filters: the first option means "no filter". */
const CHOICES: Partial<Record<ReportControl, { label: string; options: { value: string; label: string }[] }>> = {
  state: {
    label: 'State',
    options: [
      { value: '', label: 'Any state' },
      { value: 'NotStarted', label: 'Not started' },
      { value: 'Draft', label: 'Draft' },
      { value: 'AwaitingApproval', label: 'Awaiting approval' },
      { value: 'ReturnedForCorrection', label: 'Returned for correction' },
      { value: 'Approved', label: 'Approved' },
      { value: 'Published', label: 'Published' },
      { value: 'Withdrawn', label: 'Withdrawn' },
    ],
  },
  outcome: {
    label: 'Outcome',
    options: [
      { value: '', label: 'Any outcome' },
      { value: 'Promoted', label: 'Promoted' },
      { value: 'Repeat', label: 'Repeat' },
      { value: 'PromotedOnTrial', label: 'Promoted on trial' },
      { value: 'Graduated', label: 'Graduated' },
    ],
  },
  status: {
    label: 'Status',
    options: [
      { value: '', label: 'Active' },
      { value: 'Transferred', label: 'Transferred' },
      { value: 'Withdrawn', label: 'Withdrawn' },
      { value: 'Graduated', label: 'Graduated' },
    ],
  },
  sex: {
    label: 'Sex',
    options: [
      { value: '', label: 'Both' },
      { value: 'Male', label: 'Male' },
      { value: 'Female', label: 'Female' },
    ],
  },
  documentType: {
    label: 'Document',
    options: [
      { value: '', label: 'Any document' },
      { value: 'BirthCertificate', label: 'Birth certificate' },
      { value: 'PassportPhotograph', label: 'Passport photograph' },
      { value: 'PreviousSchoolResult', label: 'Previous school result' },
      { value: 'TransferLetter', label: 'Transfer letter' },
    ],
  },
};

const NONE = '__none';

/** `/reports/:key` — one of the shared-table reports, its filters, the table, and CSV / PDF export. */
export function ReportScreen() {
  const { key } = useParams();
  const definition = tableReport(key);
  // Keyed on the report so moving between reports starts from fresh filters.
  return definition ? <ReportView key={definition.key} definition={definition} /> : <NotFoundScreen />;
}

function ReportView({ definition }: { definition: TableReport }) {
  const has = (control: ReportControl) => definition.controls.includes(control);
  const term = useTermChoice();
  const klass = useClassChoice(term.sessionId, definition.privilege, true);
  const me = useMe();
  const [searchParams] = useSearchParams();
  const [scope, setScope] = useState<string | null>(null);
  const [levelId, setLevelId] = useState('');
  const [top, setTop] = useState('');
  const [minDays, setMinDays] = useState('');
  const [choices, setChoices] = useState<Partial<Record<ReportControl, string>>>({});

  const levels = [...new Map(klass.arms.map((arm) => [arm.classLevelId, arm.classLevel])).entries()].map(([value, label]) => ({ value, label }));
  const scopeOptions = [
    ...(has('scopeAll') ? [{ value: '', label: 'Whole school' }] : []),
    ...klass.arms.map((arm) => ({ value: `arm:${arm.id}`, label: arm.displayName })),
    ...levels.map((level) => ({ value: `level:${level.value}`, label: `All of ${level.label}` })),
  ];
  // A choice from another session's classes falls back to the first option, as the class picker does.
  const scopeValue = scope !== null && scopeOptions.some((option) => option.value === scope) ? scope : (scopeOptions[0]?.value ?? '');
  const [scopeKind, scopeId] = scopeValue.split(':');
  // A required level falls back to the first; an optional one means "all levels" when blank.
  const requiredLevel = levels.some((level) => level.value === levelId) ? levelId : (levels[0]?.value ?? '');
  const pupilId = searchParams.get('pupilId') ?? '';
  const whole = (value: string, max: number) => (Number.isInteger(Number(value)) && Number(value) > 0 ? Math.min(Number(value), max) : undefined);

  const params: ReportParams = {};
  let ready = true;
  if (has('term')) {
    params.termId = term.termId;
    ready &&= term.termId !== '';
  }
  if (has('session')) {
    params.sessionId = term.sessionId;
    ready &&= term.sessionId !== '';
  }
  if (has('arm')) {
    params.armId = klass.armId;
    ready &&= klass.armId !== '';
  }
  if (has('scope') || has('scopeAll')) {
    if (scopeKind === 'level' && scopeId) params.levelId = scopeId;
    else if (scopeKind === 'arm' && scopeId) params.armId = scopeId;
    if (has('scope')) ready &&= !!scopeId;
  }
  if (has('level')) {
    params.levelId = requiredLevel;
    ready &&= requiredLevel !== '';
  }
  if (has('levelAll') && levelId) params.levelId = levelId;
  const topValue = has('top') ? whole(top, 500) : undefined;
  if (topValue !== undefined) params.top = topValue;
  const minDaysValue = has('minDays') ? whole(minDays, 3650) : undefined;
  if (minDaysValue !== undefined) params.minDays = minDaysValue;
  for (const control of ['state', 'outcome', 'status', 'sex', 'documentType'] as const) {
    const value = choices[control];
    if (has(control) && value) params[control] = value;
  }
  if (has('pupil')) {
    params.pupilId = pupilId;
    ready &&= pupilId !== '';
  }

  const report = useReport(definition.key, params, ready);
  const exporter = useExportReport(definition.key);
  const canExport = !!me.data && hasPrivilege(me.data, 'report.export');
  const needsClasses = has('arm') || has('scope') || has('scopeAll') || has('level') || has('levelAll');

  const body = () => {
    if (has('pupil') && !pupilId) {
      return <p className="text-sm text-muted-foreground">Open a pupil’s record and choose Cumulative record to see this report.</p>;
    }
    if ((has('term') || has('session') || needsClasses) && (term.isPending || (needsClasses && klass.isPending))) {
      return <LoadingState label="Loading classes…" />;
    }
    if ((has('term') || has('session')) && !term.sessionId) return <p className="text-sm text-muted-foreground">Create a session first.</p>;
    if (has('term') && !term.termId) return <p className="text-sm text-muted-foreground">This session has no terms yet.</p>;
    if (needsClasses && klass.isError) {
      return <QueryErrorState error={new Error('The classes could not be loaded.')} onRetry={klass.retry} />;
    }
    if (!ready) return <p className="text-sm text-muted-foreground">There are no classes you can report on in this session.</p>;
    if (report.isPending) return <LoadingState label="Building the report…" />;
    if (report.isError) return <QueryErrorState error={report.error} onRetry={() => void report.refetch()} />;
    const data = report.data;
    return (
      <div className="flex flex-col gap-3">
        <p className="text-sm text-muted-foreground">{data.filters.join(' · ')}</p>
        <ReportTable report={data} />
        {data.notes.map((note) => (
          <p key={note} className="text-sm text-muted-foreground">
            {note}
          </p>
        ))}
      </div>
    );
  };

  return (
    <div className="flex flex-col gap-6">
      <PageTrail trail={[{ to: paths.reports }, { label: definition.title }]} />
      <header className="flex flex-col gap-1">
        <h1 className="font-display text-2xl font-semibold text-foreground">{definition.title}</h1>
        <p className="text-sm text-muted-foreground">{definition.description}</p>
      </header>
      <div className="flex flex-wrap items-end gap-3">
        {has('term') ? <TermPicker choice={term} /> : null}
        {has('session') ? (
          <LabelledSelect
            label="Session"
            placeholder="Session"
            value={term.sessionId}
            options={term.sessions.map((session) => ({ value: session.id, label: session.name }))}
            onChange={term.setSessionId}
            className="w-40"
          />
        ) : null}
        {has('arm') ? (
          <LabelledSelect
            label="Class"
            placeholder="Class"
            value={klass.armId}
            options={klass.arms.map((arm) => ({ value: arm.id, label: arm.displayName }))}
            onChange={klass.setArmId}
            className="w-44"
          />
        ) : null}
        {has('scope') || has('scopeAll') ? (
          <LabelledSelect
            label="Class or level"
            placeholder="Class or level"
            value={scopeValue || NONE}
            options={scopeOptions.map((option) => ({ value: option.value || NONE, label: option.label }))}
            onChange={(next) => setScope(next === NONE ? '' : next)}
            className="w-52"
          />
        ) : null}
        {has('level') ? (
          <LabelledSelect label="Level" placeholder="Level" value={requiredLevel} options={levels} onChange={setLevelId} className="w-44" />
        ) : null}
        {has('levelAll') ? (
          <LabelledSelect
            label="Level"
            placeholder="All levels"
            value={levelId || NONE}
            options={[{ value: NONE, label: 'All levels' }, ...levels]}
            onChange={(next) => setLevelId(next === NONE ? '' : next)}
            className="w-44"
          />
        ) : null}
        {(['state', 'outcome', 'status', 'sex', 'documentType'] as const).map((control) => {
          const choice = CHOICES[control];
          return has(control) && choice ? (
            <LabelledSelect
              key={control}
              label={choice.label}
              placeholder={choice.options[0]?.label ?? choice.label}
              value={choices[control] || NONE}
              options={choice.options.map((option) => ({ value: option.value || NONE, label: option.label }))}
              onChange={(next) => setChoices((current) => ({ ...current, [control]: next === NONE ? '' : next }))}
              className="w-48"
            />
          ) : null;
        })}
        {has('top') ? (
          <label htmlFor="report-top" className="flex items-center gap-2 text-sm text-foreground">
            Top
            <Input id="report-top" type="number" min={1} max={500} value={top} onChange={(event) => setTop(event.target.value)} className="w-24" placeholder="All" />
          </label>
        ) : null}
        {has('minDays') ? (
          <label htmlFor="report-min-days" className="flex items-center gap-2 text-sm text-foreground">
            At least
            <Input
              id="report-min-days"
              type="number"
              min={0}
              max={3650}
              value={minDays}
              onChange={(event) => setMinDays(event.target.value)}
              className="w-24"
              placeholder="0"
            />
            days old
          </label>
        ) : null}
        {canExport && report.isSuccess ? (
          <div className="flex gap-2">
            <Button variant="outline" disabled={exporter.isPending} onClick={() => exporter.mutate({ params, format: 'csv' })}>
              <Download aria-hidden="true" />
              CSV
            </Button>
            <Button variant="outline" disabled={exporter.isPending} onClick={() => exporter.mutate({ params, format: 'pdf' })}>
              <Download aria-hidden="true" />
              PDF
            </Button>
          </div>
        ) : null}
      </div>
      <FormError message={exporter.error instanceof ApiError ? exporter.error.message : null} />
      {body()}
    </div>
  );
}
