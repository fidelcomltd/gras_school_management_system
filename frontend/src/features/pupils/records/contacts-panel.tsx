import { useState } from 'react';
import { FormError, LoadingState, QueryErrorState } from '@/components/feedback/query-states';
import { Button } from '@/components/ui/button';
import { ApiError } from '@/lib/http';
import { useContacts, useSaveContacts, type ContactRole, type PupilContactDto, type PupilContactInput } from './api';
import { TextField } from './fields';

const SLOTS: { role: ContactRole; title: string; adult: boolean; parent: boolean }[] = [
  { role: 'Father', title: 'Father', adult: true, parent: true },
  { role: 'Mother', title: 'Mother', adult: true, parent: true },
  { role: 'Guardian', title: 'Guardian (if applicable)', adult: true, parent: false },
  { role: 'EmergencyPrimary', title: 'Primary emergency contact', adult: false, parent: false },
  { role: 'EmergencyAlternate', title: 'Alternative emergency contact', adult: false, parent: false },
];

type Draft = Record<ContactRole, { fullName: string; relationship: string; phone: string; whatsappNumber: string; occupation: string; email: string }>;

const blank = { fullName: '', relationship: '', phone: '', whatsappNumber: '', occupation: '', email: '' };

/** "+2348031234567" back to the form people type, "08031234567". */
const local = (phone: string | null | undefined) => (phone ? phone.replace(/^\+234/, '0') : '');

function toDraft(items: PupilContactDto[]): Draft {
  const draft = Object.fromEntries(SLOTS.map((slot) => [slot.role, { ...blank }])) as Draft;
  for (const item of items) {
    draft[item.role] = {
      fullName: item.fullName,
      relationship: item.relationship ?? '',
      phone: local(item.phone),
      whatsappNumber: local(item.whatsappNumber),
      occupation: item.occupation ?? '',
      email: item.email ?? '',
    };
  }
  return draft;
}

/** Sections C and D (spec 6.5.5): one card per slot; a slot with no name is not recorded. */
export function ContactsPanel({ pupilId, canEdit }: { pupilId: string; canEdit: boolean }) {
  const contacts = useContacts(pupilId);
  if (contacts.isPending) return <LoadingState label="Loading contacts…" />;
  if (contacts.isError) return <QueryErrorState error={contacts.error} onRetry={() => void contacts.refetch()} />;
  return <ContactsForm key={JSON.stringify(contacts.data.items)} pupilId={pupilId} items={contacts.data.items} canEdit={canEdit} />;
}

function ContactsForm({ pupilId, items, canEdit }: { pupilId: string; items: PupilContactDto[]; canEdit: boolean }) {
  const save = useSaveContacts(pupilId);
  const [draft, setDraft] = useState<Draft>(() => toDraft(items));
  const [primary, setPrimary] = useState<ContactRole | null>(() => items.find((item) => item.isPrimaryContact)?.role ?? null);

  const set = (role: ContactRole, field: keyof Draft[ContactRole], value: string) =>
    setDraft((current) => ({ ...current, [role]: { ...current[role], [field]: value } }));
  const copyInto = (from: ContactRole) =>
    setDraft((current) => ({ ...current, EmergencyPrimary: { ...blank, fullName: current[from].fullName, phone: current[from].phone, relationship: from } }));

  const recorded = SLOTS.filter((slot) => draft[slot.role].fullName.trim() !== '');
  const adults = recorded.filter((slot) => slot.adult);
  const effectivePrimary = adults.some((slot) => slot.role === primary) ? primary : (adults[0]?.role ?? null);

  const submit = () => {
    const contacts: PupilContactInput[] = recorded.map((slot) => {
      const entry = draft[slot.role];
      return {
        role: slot.role,
        fullName: entry.fullName,
        relationship: slot.parent ? null : entry.relationship || null,
        phone: entry.phone,
        whatsappNumber: slot.parent ? entry.whatsappNumber || null : null,
        occupation: slot.parent ? entry.occupation || null : null,
        email: entry.email || null,
        isPrimaryContact: slot.role === effectivePrimary,
      };
    });
    save.mutate(contacts);
  };

  return (
    <div className="flex flex-col gap-4">
      <p className="text-sm text-muted-foreground">
        At least one of father, mother or guardian, and the primary emergency contact, are needed before the admission can be approved.
      </p>
      <div className="grid gap-4 lg:grid-cols-2">
        {SLOTS.map((slot) => {
          const entry = draft[slot.role];
          return (
            <fieldset key={slot.role} className="flex flex-col gap-2 rounded-lg border border-border p-3" disabled={!canEdit}>
              <legend className="px-1 text-sm font-semibold text-foreground">{slot.title}</legend>
              {slot.role === 'EmergencyPrimary' && canEdit ? (
                <div className="flex gap-2">
                  <Button type="button" variant="ghost" size="sm" disabled={draft.Father.fullName.trim() === ''} onClick={() => copyInto('Father')}>
                    Copy from father
                  </Button>
                  <Button type="button" variant="ghost" size="sm" disabled={draft.Mother.fullName.trim() === ''} onClick={() => copyInto('Mother')}>
                    Copy from mother
                  </Button>
                </div>
              ) : null}
              <TextField label={`${slot.title}: full name`} value={entry.fullName} onChange={(value) => set(slot.role, 'fullName', value)} />
              {slot.parent ? null : (
                <TextField label={`${slot.title}: relationship to the pupil`} value={entry.relationship} onChange={(value) => set(slot.role, 'relationship', value)} />
              )}
              <TextField label={`${slot.title}: phone`} value={entry.phone} inputMode="tel" placeholder="08031234567" onChange={(value) => set(slot.role, 'phone', value)} />
              {slot.parent ? (
                <>
                  <TextField label={`${slot.title}: WhatsApp`} value={entry.whatsappNumber} inputMode="tel" onChange={(value) => set(slot.role, 'whatsappNumber', value)} />
                  {canEdit && entry.phone !== '' && entry.whatsappNumber !== entry.phone ? (
                    <Button type="button" variant="ghost" size="sm" className="self-start" onClick={() => set(slot.role, 'whatsappNumber', entry.phone)}>
                      WhatsApp same as phone
                    </Button>
                  ) : null}
                  <TextField label={`${slot.title}: occupation`} value={entry.occupation} onChange={(value) => set(slot.role, 'occupation', value)} />
                </>
              ) : null}
              <TextField label={`${slot.title}: email (optional)`} value={entry.email} inputMode="email" onChange={(value) => set(slot.role, 'email', value)} />
              {slot.adult && entry.fullName.trim() !== '' ? (
                <label className="flex items-center gap-2 text-sm text-foreground">
                  <input type="radio" name="primary-contact" checked={effectivePrimary === slot.role} onChange={() => setPrimary(slot.role)} />
                  Primary contact (telephoned first; receives the pin slip)
                </label>
              ) : null}
            </fieldset>
          );
        })}
      </div>
      <FormError message={save.error instanceof ApiError ? save.error.message : null} />
      {canEdit ? (
        <div className="flex items-center gap-3">
          <Button onClick={submit} disabled={save.isPending}>
            {save.isPending ? 'Saving…' : 'Save contacts'}
          </Button>
          <output aria-live="polite" className="text-sm text-muted-foreground">
            {save.isSuccess ? 'Contacts saved.' : ''}
          </output>
        </div>
      ) : null}
    </div>
  );
}
