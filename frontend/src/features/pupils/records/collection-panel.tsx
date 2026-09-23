import { useState } from 'react';
import { FormError, LoadingState, QueryErrorState } from '@/components/feedback/query-states';
import { Button } from '@/components/ui/button';
import { useBarredPersons, usePickupPersons, useSaveBarredPersons, useSavePickupPersons, type BarredPersonsDto } from './api';
import { TextField, YesNo } from './fields';
import { errorText, localPhone } from './format';

type PickupRow = { key: string; fullName: string; relationship: string; phone: string };
const blankPickup = (): PickupRow => ({ key: crypto.randomUUID(), fullName: '', relationship: '', phone: '' });

/**
 * Section E (spec 6.5.6): who may collect the child, and who must not. The barred list is shown only to staff with the
 * safeguarding privilege, and each time it is opened the server records who looked.
 */
export function CollectionPanel({
  pupilId,
  canEdit,
  canSeeBarred,
  canEditBarred,
}: {
  pupilId: string;
  canEdit: boolean;
  canSeeBarred: boolean;
  canEditBarred: boolean;
}) {
  const pickup = usePickupPersons(pupilId);
  const barred = useBarredPersons(pupilId, canSeeBarred);

  return (
    <div className="flex flex-col gap-6">
      <section aria-labelledby="pickup-heading" className="flex flex-col gap-3">
        <h2 id="pickup-heading" className="text-base font-semibold text-foreground">
          Authorised to collect the child
        </h2>
        {pickup.isPending ? (
          <LoadingState label="Loading pickup list…" />
        ) : pickup.isError ? (
          <QueryErrorState error={pickup.error} onRetry={() => void pickup.refetch()} />
        ) : (
          <PickupForm
            key={JSON.stringify(pickup.data.items)}
            pupilId={pupilId}
            initial={pickup.data.items.map((item) => ({ key: item.id, fullName: item.fullName, relationship: item.relationship, phone: localPhone(item.phone) }))}
            canEdit={canEdit}
          />
        )}
      </section>

      {canSeeBarred ? (
        <section aria-labelledby="barred-heading" className="flex flex-col gap-3 rounded-lg border border-destructive/40 p-3">
          <h2 id="barred-heading" className="text-base font-semibold text-foreground">
            Anyone who must NOT collect the child
          </h2>
          <p className="text-xs text-muted-foreground">Safeguarding information. Never printed or exported; every view is logged.</p>
          {barred.isPending ? (
            <LoadingState label="Loading…" />
          ) : barred.isError ? (
            <QueryErrorState error={barred.error} onRetry={() => void barred.refetch()} />
          ) : (
            <BarredForm key={JSON.stringify(barred.data)} pupilId={pupilId} data={barred.data} canEdit={canEditBarred} />
          )}
        </section>
      ) : null}
    </div>
  );
}

function PickupForm({ pupilId, initial, canEdit }: { pupilId: string; initial: PickupRow[]; canEdit: boolean }) {
  const save = useSavePickupPersons(pupilId);
  // Spec 6.5.6: three blank rows by default, matching the form's table.
  const [rows, setRows] = useState<PickupRow[]>(() => (initial.length > 0 ? initial : [blankPickup(), blankPickup(), blankPickup()]));
  const set = (index: number, field: Exclude<keyof PickupRow, 'key'>, value: string) =>
    setRows((current) => current.map((row, at) => (at === index ? { ...row, [field]: value } : row)));

  return (
    <div className="flex flex-col gap-3">
      <p className="text-sm text-muted-foreground">Optional. With nobody listed, only a recorded parent, guardian or emergency contact may collect the child.</p>
      <fieldset disabled={!canEdit} className="flex flex-col gap-2">
        {rows.map((row, index) => (
          <div key={row.key} className="grid gap-2 sm:grid-cols-3">
            <TextField label={`Person ${index + 1}: full name`} value={row.fullName} onChange={(value) => set(index, 'fullName', value)} />
            <TextField label={`Person ${index + 1}: relationship`} value={row.relationship} onChange={(value) => set(index, 'relationship', value)} />
            <TextField label={`Person ${index + 1}: phone`} value={row.phone} inputMode="tel" onChange={(value) => set(index, 'phone', value)} />
          </div>
        ))}
      </fieldset>
      <FormError message={errorText(save.error)} />
      {canEdit ? (
        <div className="flex gap-2">
          <Button variant="outline" size="sm" onClick={() => setRows((current) => [...current, blankPickup()])}>
            Add a person
          </Button>
          <Button size="sm" disabled={save.isPending} onClick={() => save.mutate(rows.filter((row) => row.fullName.trim() !== '').map(({ fullName, relationship, phone }) => ({ fullName, relationship, phone })))}>
            {save.isPending ? 'Saving…' : 'Save pickup list'}
          </Button>
        </div>
      ) : null}
    </div>
  );
}

function BarredForm({ pupilId, data, canEdit }: { pupilId: string; data: BarredPersonsDto; canEdit: boolean }) {
  const save = useSaveBarredPersons(pupilId);
  const [answer, setAnswer] = useState<boolean | null>(data.hasBarredPersons ?? null);
  const [persons, setPersons] = useState(() =>
    data.items.length > 0
      ? data.items.map((item) => ({ key: item.id, fullName: item.fullName, details: item.details ?? '' }))
      : [{ key: crypto.randomUUID(), fullName: '', details: '' }],
  );

  return (
    <div className="flex flex-col gap-3">
      <YesNo legend="Is there anyone who should not be allowed to collect the child?" value={answer} onChange={setAnswer} disabled={!canEdit} />
      {answer ? (
        <fieldset disabled={!canEdit} className="flex flex-col gap-2">
          {persons.map((person, index) => (
            <div key={person.key} className="grid gap-2 sm:grid-cols-2">
              <TextField
                label={`Barred person ${index + 1}: name`}
                value={person.fullName}
                onChange={(value) => setPersons((current) => current.map((row, at) => (at === index ? { ...row, fullName: value } : row)))}
              />
              <TextField
                label={`Barred person ${index + 1}: relevant information`}
                value={person.details}
                onChange={(value) => setPersons((current) => current.map((row, at) => (at === index ? { ...row, details: value } : row)))}
              />
            </div>
          ))}
        </fieldset>
      ) : null}
      <FormError message={errorText(save.error)} />
      {canEdit ? (
        <div className="flex gap-2">
          {answer ? (
            <Button variant="outline" size="sm" onClick={() => setPersons((current) => [...current, { key: crypto.randomUUID(), fullName: '', details: '' }])}>
              Add a name
            </Button>
          ) : null}
          <Button
            size="sm"
            disabled={answer === null || save.isPending}
            onClick={() =>
              save.mutate({
                hasBarredPersons: answer === true,
                persons: answer ? persons.filter((person) => person.fullName.trim() !== '').map((person) => ({ fullName: person.fullName, details: person.details || null })) : [],
              })
            }
          >
            {save.isPending ? 'Saving…' : 'Save answer'}
          </Button>
        </div>
      ) : null}
    </div>
  );
}
