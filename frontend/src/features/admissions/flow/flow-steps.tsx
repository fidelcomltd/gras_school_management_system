import { useState } from 'react';
import { Link } from 'react-router';
import { paths } from '@/app/router/paths';
import { FormError, LoadingState, QueryErrorState } from '@/components/feedback/query-states';
import { Button } from '@/components/ui/button';
import { useUpdatePupil } from '@/features/pupils/api';
import { TextField } from '@/features/pupils/records/fields';
import { errorText } from '@/features/pupils/records/format';
import { pupilName, type PupilDto } from '@/features/pupils/types';
import { useAdmissionRecord, useUpdateAdmissionRecord } from '../api';
import type { AdmissionRecordDto } from '../types';

/** Today in Lagos (fixed UTC+1), as the date input wants it. */
function lagosToday(): string {
  return new Date(Date.now() + 60 * 60 * 1000).toISOString().slice(0, 10);
}

/** Step 6 (section G): one free-text box, skippable with Next. */
export function OtherInformationStep({ pupil, onSaved }: { pupil: PupilDto; onSaved: () => void }) {
  const update = useUpdatePupil(pupil.id);
  const [text, setText] = useState(pupil.otherInformation ?? '');
  const unchanged = text === (pupil.otherInformation ?? '');
  const save = () =>
    update.mutate(
      {
        id: pupil.id,
        surname: null,
        firstName: null,
        middleName: null,
        sex: null,
        dateOfBirth: null,
        nationality: null,
        stateOfOrigin: null,
        lga: null,
        homeAddress: null,
        previousSchool: null,
        previousClass: null,
        registrationNumber: null,
        otherInformation: text,
      },
      { onSuccess: onSaved },
    );

  return (
    <div className="flex flex-col gap-3">
      <TextField label="Any other important information about the child" multiline value={text} onChange={setText} />
      <FormError message={errorText(update.error)} />
      <Button className="self-start" disabled={update.isPending || unchanged} onClick={save}>
        {update.isPending ? 'Saving…' : 'Save and continue'}
      </Button>
    </div>
  );
}

/** Step 8 (section I): the declaring parent, the date, and a tick that the signed paper form exists. */
export function DeclarationStep({ pupilId, onSaved }: { pupilId: string; onSaved: () => void }) {
  const record = useAdmissionRecord(pupilId);
  if (record.isPending) return <LoadingState label="Loading the declaration…" />;
  if (record.isError) return <QueryErrorState error={record.error} onRetry={() => void record.refetch()} />;
  return <DeclarationForm pupilId={pupilId} record={record.data} onSaved={onSaved} />;
}

function DeclarationForm({ pupilId, record, onSaved }: { pupilId: string; record: AdmissionRecordDto; onSaved: () => void }) {
  const update = useUpdateAdmissionRecord(pupilId);
  const [name, setName] = useState(record.declarationName ?? '');
  const [signed, setSigned] = useState(record.declarationSigned);
  const [date, setDate] = useState(record.declarationDate ?? '');
  const [today] = useState(lagosToday);
  const incomplete = signed && (name.trim() === '' || date === '');

  // A null field is left unchanged by the PATCH, so only section I is sent.
  const save = () =>
    update.mutate(
      {
        sessionId: null,
        dateApplicationReceived: null,
        dateAdmitted: null,
        classAdmittedInto: null,
        admissionType: null,
        admissionTypeNote: null,
        assessmentRequired: null,
        assessmentResultRemarks: null,
        assignedClassTeacher: null,
        headOfSchoolConfirmed: null,
        headOfSchoolName: null,
        declarationName: name.trim(),
        declarationSigned: signed,
        declarationDate: signed ? date : null,
      },
      { onSuccess: onSaved },
    );

  return (
    <div className="flex max-w-xl flex-col gap-3">
      <p className="text-sm text-muted-foreground">The signed paper form is the record. This step notes who signed it and when.</p>
      <TextField label="Name of the parent or guardian making the declaration" value={name} onChange={setName} />
      <label className="flex items-center gap-2 text-sm text-foreground">
        <input
          type="checkbox"
          checked={signed}
          onChange={(event) => {
            setSigned(event.target.checked);
            if (event.target.checked && date === '') setDate(today);
          }}
        />
        The declaration on the paper form has been signed
      </label>
      {signed ? (
        <label className="flex flex-col gap-1 text-xs text-muted-foreground">
          Date signed
          <input
            type="date"
            className="h-9 w-44 rounded-md border border-input bg-background px-2 text-sm text-foreground"
            value={date}
            max={today}
            onChange={(event) => setDate(event.target.value)}
          />
        </label>
      ) : null}
      <FormError message={errorText(update.error)} />
      <Button className="self-start" disabled={update.isPending || incomplete} onClick={save}>
        {update.isPending ? 'Saving…' : 'Save and continue'}
      </Button>
    </div>
  );
}

/** What the flow shows once the admission is no longer pending: the issued number, large, with a copy button. */
export function AdmittedNotice({ pupil }: { pupil: PupilDto }) {
  const number = pupil.registrationNumber;
  return (
    <div className="flex flex-col items-start gap-4">
      <h1 className="font-display text-2xl font-semibold text-foreground">{pupilName(pupil)}</h1>
      {pupil.status === 'Active' && number ? (
        <>
          <output className="flex flex-col gap-1 text-sm text-foreground">
            Admitted. Registration number:
            <span className="font-display text-4xl font-semibold tracking-wide">{number}</span>
          </output>
          <Button variant="outline" size="sm" onClick={() => void navigator.clipboard?.writeText(number)}>
            Copy number
          </Button>
        </>
      ) : (
        <p className="text-sm text-muted-foreground">This admission is no longer pending (status: {pupil.status}).</p>
      )}
      <div className="flex gap-3">
        <Link to={paths.pupilDetail(pupil.id)} className="text-sm text-primary hover:underline">
          Open the pupil record
        </Link>
        <Link to={paths.admissions} className="text-sm text-primary hover:underline">
          Back to the admissions queue
        </Link>
      </div>
    </div>
  );
}
