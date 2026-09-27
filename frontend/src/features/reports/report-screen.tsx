import { useState } from 'react';
import { Download } from 'lucide-react';
import { useParams } from 'react-router';
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
import { tableReport, type TableReport } from './definitions';
import { ReportTable } from './report-table';

const STATES = [
  { value: '', label: 'Any state' },
  { value: 'NotStarted', label: 'Not started' },
  { value: 'Draft', label: 'Draft' },
  { value: 'AwaitingApproval', label: 'Awaiting approval' },
  { value: 'ReturnedForCorrection', label: 'Returned for correction' },
  { value: 'Approved', label: 'Approved' },
  { value: 'Published', label: 'Published' },
  { value: 'Withdrawn', label: 'Withdrawn' },
];

/** `/reports/:key` — one of the shared-table reports, its filters, the table, and CSV / PDF export. */
export function ReportScreen() {
  const { key } = useParams();
  const definition = tableReport(key);
  // Keyed on the report so moving between reports starts from fresh filters.
  return definition ? <ReportView key={definition.key} definition={definition} /> : <NotFoundScreen />;
}

function ReportView({ definition }: { definition: TableReport }) {
  const term = useTermChoice();
  const klass = useClassChoice(term.sessionId, 'report.view', true);
  const me = useMe();
  const [scope, setScope] = useState<string | null>(null);
  const [top, setTop] = useState('');
  const [levelId, setLevelId] = useState('');
  const [state, setState] = useState('');

  const levels = [...new Map(klass.arms.map((arm) => [arm.classLevelId, arm.classLevel])).entries()].map(([value, label]) => ({
    value,
    label,
  }));
  const scopeOptions = [
    ...klass.arms.map((arm) => ({ value: `arm:${arm.id}`, label: arm.displayName })),
    ...levels.map((level) => ({ value: `level:${level.value}`, label: `All of ${level.label}` })),
  ];
  // A choice from another session's classes falls back to the first class, as the class picker does.
  const scopeValue = scope !== null && scopeOptions.some((option) => option.value === scope) ? scope : (scopeOptions[0]?.value ?? '');
  const [scopeKind, scopeId] = scopeValue.split(':');
  // A required level falls back to the first, like the class picker; an optional one means "all levels" when blank.
  const requiredLevel = levels.some((level) => level.value === levelId) ? levelId : (levels[0]?.value ?? '');
  const topNumber = Number(top);

  const params: ReportParams = { termId: term.termId };
  let ready = term.termId !== '';
  switch (definition.filters) {
    case 'term-arm':
      params.armId = klass.armId;
      ready &&= klass.armId !== '';
      break;
    case 'term-scope':
    case 'term-scope-top':
      if (scopeKind === 'level' && scopeId) params.levelId = scopeId;
      else if (scopeId) params.armId = scopeId;
      if (definition.filters === 'term-scope-top' && Number.isInteger(topNumber) && topNumber > 0) params.top = Math.min(topNumber, 500);
      ready &&= !!scopeId;
      break;
    case 'term-level':
      params.levelId = requiredLevel;
      ready &&= requiredLevel !== '';
      break;
    case 'term-level-state':
    case 'term-level-any':
      if (levelId) params.levelId = levelId;
      if (state && definition.filters === 'term-level-state') params.state = state;
      break;
  }

  const report = useReport(definition.key, params, ready);
  const exporter = useExportReport(definition.key);
  const canExport = !!me.data && hasPrivilege(me.data, 'report.export');

  const body = () => {
    if (term.isPending || klass.isPending) return <LoadingState label="Loading classes…" />;
    if (!term.termId) return <p className="text-sm text-muted-foreground">Create a session first.</p>;
    if (klass.isError) return <QueryErrorState error={new Error('The classes could not be loaded.')} onRetry={klass.retry} />;
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
        <TermPicker choice={term} />
        {definition.filters === 'term-arm' ? (
          <LabelledSelect
            label="Class"
            placeholder="Class"
            value={klass.armId}
            options={klass.arms.map((arm) => ({ value: arm.id, label: arm.displayName }))}
            onChange={klass.setArmId}
            className="w-44"
          />
        ) : null}
        {definition.filters === 'term-scope' || definition.filters === 'term-scope-top' ? (
          <>
            <LabelledSelect
              label="Class or level"
              placeholder="Class or level"
              value={scopeValue}
              options={scopeOptions}
              onChange={setScope}
              className="w-52"
            />
            {definition.filters === 'term-scope-top' ? (
              <label htmlFor="report-top" className="flex items-center gap-2 text-sm text-foreground">
                Top
                <Input
                  id="report-top"
                  type="number"
                  min={1}
                  max={500}
                  value={top}
                  onChange={(event) => setTop(event.target.value)}
                  aria-label="Top positions"
                  className="w-24"
                  placeholder="All"
                />
              </label>
            ) : null}
          </>
        ) : null}
        {definition.filters === 'term-level' ? (
          <LabelledSelect label="Level" placeholder="Level" value={requiredLevel} options={levels} onChange={setLevelId} className="w-44" />
        ) : null}
        {definition.filters === 'term-level-state' || definition.filters === 'term-level-any' ? (
          <>
            <LabelledSelect
              label="Level"
              placeholder="All levels"
              value={levelId || 'all'}
              options={[{ value: 'all', label: 'All levels' }, ...levels]}
              onChange={(next) => setLevelId(next === 'all' ? '' : next)}
              className="w-44"
            />
            {definition.filters === 'term-level-state' ? (
              <LabelledSelect
                label="State"
                placeholder="Any state"
                value={state || 'any'}
                options={STATES.map((option) => ({ value: option.value || 'any', label: option.label }))}
                onChange={(next) => setState(next === 'any' ? '' : next)}
                className="w-52"
              />
            ) : null}
          </>
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
