import { useState } from 'react';
import { Link, useParams } from 'react-router';
import { paths } from '@/app/router/paths';
import { LoadingState, QueryErrorState } from '@/components/feedback/query-states';
import { Button } from '@/components/ui/button';
import { useMe } from '@/features/auth/api';
import { hasPrivilege } from '@/lib/auth/auth-session';
import { usePupil } from './api';
import { CorrectNumberDialog } from './components/correct-number-dialog';
import { EditPupilDialog } from './components/edit-pupil-dialog';
import { pupilName, type PupilDto } from './types';

function formatDate(iso: string): string {
  const [year, month, day] = iso.slice(0, 10).split('-');
  return `${day}/${month}/${year}`;
}

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

  if (pupil.isPending) {
    return <LoadingState label="Loading pupil…" />;
  }

  if (pupil.isError) {
    return <QueryErrorState error={pupil.error} onRetry={() => void pupil.refetch()} />;
  }

  const record = pupil.data;
  const canEdit = !!me.data && hasPrivilege(me.data, 'pupil.update');
  const canCorrect = !!me.data && hasPrivilege(me.data, 'pupil.regnumber.correct') && record.registrationNumber !== null;

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

      <dl className="grid gap-x-6 gap-y-3 rounded-md border border-border bg-surface p-4 text-sm sm:grid-cols-[max-content_1fr]">
        {ROWS.map((row) => (
          <div key={row.label} className="contents">
            <dt className="text-muted-foreground">{row.label}</dt>
            <dd className="text-foreground">{row.value(record) ?? '—'}</dd>
          </div>
        ))}
      </dl>

      {dialog === 'edit' ? <EditPupilDialog pupil={record} onClose={() => setDialog(null)} /> : null}
      {dialog === 'number' ? <CorrectNumberDialog pupil={record} onClose={() => setDialog(null)} /> : null}
    </div>
  );
}
