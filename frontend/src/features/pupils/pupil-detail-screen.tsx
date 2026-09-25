import { useState } from 'react';
import { Link, useNavigate, useParams } from 'react-router';
import { paths } from '@/app/router/paths';
import { LoadingState, QueryErrorState } from '@/components/feedback/query-states';
import { Button } from '@/components/ui/button';
import { useMe } from '@/features/auth/api';
import { hasPrivilege } from '@/lib/auth/auth-session';
import { cn } from '@/lib/utils/cn';
import { formatDate } from '@/shared/format/date';
import { usePupil } from './api';
import { CorrectNumberDialog } from './components/correct-number-dialog';
import { EditPupilDialog } from './components/edit-pupil-dialog';
import { ClassStatusPanel } from './movement/class-status-panel';
import { CollectionPanel } from './records/collection-panel';
import { ContactsPanel } from './records/contacts-panel';
import { CompletenessCard, DocumentsPanel } from './records/documents-panel';
import { HealthPanel } from './records/health-panel';
import { pupilName, type PupilDto } from './types';

type Tab = 'details' | 'class' | 'contacts' | 'collection' | 'health' | 'documents';

/** Spec 6.5.11's steps held on a tab here; the rest (declaration, approval) open in the admission flow. */
const TAB_FOR_STEP: Partial<Record<number, Tab>> = { 3: 'contacts', 4: 'collection', 5: 'health', 7: 'documents' };

const ROWS: { label: string; value: (pupil: PupilDto) => string | null }[] = [
  { label: 'Registration number', value: (pupil) => pupil.registrationNumber ?? 'Issued on admission approval' },
  { label: 'Status', value: (pupil) => pupil.status },
  { label: 'Sex', value: (pupil) => pupil.sex },
  { label: 'Date of birth', value: (pupil) => `${formatDate(pupil.dateOfBirth)} (age ${pupil.ageYears})` },
  { label: 'Nationality', value: (pupil) => pupil.nationality },
  { label: 'State of origin', value: (pupil) => pupil.stateOfOrigin },
  { label: 'LGA', value: (pupil) => pupil.lga },
  { label: 'Home address', value: (pupil) => pupil.homeAddress },
  { label: 'Previous school', value: (pupil) => pupil.previousSchool },
  { label: 'Previous class', value: (pupil) => pupil.previousClass },
  { label: 'Other information', value: (pupil) => pupil.otherInformation },
];

/** `/pupils/:id` — one pupil's record (spec 6.5), with edit and the reasoned number correction. */
export function PupilDetailScreen() {
  const { id = '' } = useParams();
  const pupil = usePupil(id);
  const me = useMe();
  const [dialog, setDialog] = useState<'edit' | 'number' | null>(null);
  const [tab, setTab] = useState<Tab>('details');
  const navigate = useNavigate();

  if (pupil.isPending) {
    return <LoadingState label="Loading pupil…" />;
  }

  if (pupil.isError) {
    return <QueryErrorState error={pupil.error} onRetry={() => void pupil.refetch()} />;
  }

  const record = pupil.data;
  const canEdit = !!me.data && hasPrivilege(me.data, 'pupil.update');
  const canCorrect = !!me.data && hasPrivilege(me.data, 'pupil.regnumber.correct') && record.registrationNumber !== null;
  const can = (privilege: string) => !!me.data && hasPrivilege(me.data, privilege);
  const tabs: { id: Tab; label: string; shown: boolean }[] = [
    { id: 'details', label: 'Details', shown: true },
    { id: 'class', label: 'Class and status', shown: record.status !== 'Pending' },
    { id: 'contacts', label: 'Contacts', shown: can('contact.view') },
    { id: 'collection', label: 'Collection', shown: can('contact.view') },
    { id: 'health', label: 'Health', shown: can('pupil.safeguarding.view') },
    { id: 'documents', label: 'Documents', shown: true },
  ];
  const visible = tabs.filter((candidate) => candidate.shown);
  const current = visible.some((candidate) => candidate.id === tab) ? tab : 'details';

  return (
    <div className="flex flex-col gap-6">
      <Link to={paths.pupils} className="text-sm text-primary hover:underline">
        ← All pupils
      </Link>
      <header className="flex flex-wrap items-center justify-between gap-4">
        <h1 className="font-display text-2xl font-semibold text-foreground">{pupilName(record)}</h1>
        <div className="flex gap-2">
          {canEdit ? (
            <Button variant="outline" onClick={() => setDialog('edit')}>
              Edit
            </Button>
          ) : null}
          {canCorrect ? (
            <Button variant="outline" onClick={() => setDialog('number')}>
              Correct registration number
            </Button>
          ) : null}
        </div>
      </header>

      {record.status === 'Pending' ? (
        <>
          <Link to={paths.admissionFlow(record.id)} className="self-start text-sm font-medium text-primary hover:underline">
            Continue the admission step by step →
          </Link>
          <CompletenessCard
            pupilId={record.id}
            onGo={(step) => {
              const target = TAB_FOR_STEP[step];
              if (target) setTab(target);
              else void navigate(paths.admissionFlow(record.id, step));
            }}
          />
        </>
      ) : null}

      <div role="tablist" aria-label="Pupil record" className="flex flex-wrap gap-1 border-b border-border">
        {visible.map((candidate) => (
          <button
            key={candidate.id}
            type="button"
            role="tab"
            id={`pupil-tab-${candidate.id}`}
            aria-selected={current === candidate.id}
            aria-controls="pupil-panel"
            className={cn(
              '-mb-px border-b-2 px-3 py-2 text-sm font-medium',
              current === candidate.id ? 'border-primary text-primary' : 'border-transparent text-muted-foreground hover:text-foreground',
            )}
            onClick={() => setTab(candidate.id)}
          >
            {candidate.label}
          </button>
        ))}
      </div>

      <div role="tabpanel" id="pupil-panel" aria-labelledby={`pupil-tab-${current}`}>
        {current === 'details' ? (
          <dl className="grid gap-x-6 gap-y-3 rounded-md border border-border bg-surface p-4 text-sm sm:grid-cols-[max-content_1fr]">
            {ROWS.map((row) => (
              <div key={row.label} className="contents">
                <dt className="text-muted-foreground">{row.label}</dt>
                <dd className="text-foreground">{row.value(record) ?? '—'}</dd>
              </div>
            ))}
          </dl>
        ) : null}
        {current === 'class' ? (
          <ClassStatusPanel
            pupilId={record.id}
            status={record.status}
            declined={record.registrationNumber === null}
            canTransfer={can('pupil.transfer')}
            canChangeStatus={can('pupil.status.update')}
          />
        ) : null}
        {current === 'contacts' ? <ContactsPanel pupilId={record.id} canEdit={can('contact.update')} /> : null}
        {current === 'collection' ? (
          <CollectionPanel
            pupilId={record.id}
            canEdit={can('contact.update')}
            canSeeBarred={can('pupil.safeguarding.view')}
            canEditBarred={can('pupil.safeguarding.update')}
          />
        ) : null}
        {current === 'health' ? <HealthPanel pupilId={record.id} canEdit={can('pupil.safeguarding.update')} /> : null}
        {current === 'documents' ? <DocumentsPanel pupilId={record.id} canEdit={can('pupil.document.manage')} /> : null}
      </div>

      {dialog === 'edit' ? <EditPupilDialog pupil={record} onClose={() => setDialog(null)} /> : null}
      {dialog === 'number' ? <CorrectNumberDialog pupil={record} onClose={() => setDialog(null)} /> : null}
    </div>
  );
}
