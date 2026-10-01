import { useState } from 'react';
import { FormError, LoadingState, QueryErrorState } from '@/components/feedback/query-states';
import { Button } from '@/components/ui/button';
import { useContacts, useSaveContacts, type ContactRole, type PupilContactDto, type PupilContactInput } from './api';
import { TextField } from './fields';
import { PhoneDuplicateWarning } from './phone-duplicates';
import { errorText, localPhone } from './format';

const SLOTS: { role: ContactRole; title: string; adult: boolean; parent: boolean }[] = [
  { role: 'Father', title: 'Father', adult: true, parent: true },
  { role: 'Mother', title: 'Mother', adult: true, parent: true },
  { role: 'Guardian', title: 'Guardian (if applicable)', adult: true, parent: false },
  { role: 'EmergencyPrimary', title: 'Primary emergency contact', adult: false, parent: false },
  { role: 'EmergencyAlternate', title: 'Alternative emergency contact', adult: false, parent: false },
];

type EmergencyRole = Extract<ContactRole, 'EmergencyPrimary' | 'EmergencyAlternate'>;
const isEmergency = (role: ContactRole): role is EmergencyRole => role === 'EmergencyPrimary' || role === 'EmergencyAlternate';

/** Whoever is already filled in above can be copied into an emergency slot; the guardian only when there is one. */
const COPY_SOURCES = ['Father', 'Mother', 'Guardian'] as const;
type CopySource = (typeof COPY_SOURCES)[number];

type Draft = Record<ContactRole, { fullName: string; relationship: string; phone: string; whatsappNumber: string; occupation: string; email: string }>;

const blank = { fullName: '', relationship: '', phone: '', whatsappNumber: '', occupation: '', email: '' };

function toDraft(items: PupilContactDto[]): Draft {
  const draft = Object.fromEntries(SLOTS.map((slot) => [slot.role, { ...blank }])) as Draft;
  for (const item of items) {
    draft[item.role] = {
      fullName: item.fullName,
      relationship: item.relationship ?? '',
      phone: localPhone(item.phone),
      whatsappNumber: localPhone(item.whatsappNumber),
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
  return (
    <div className="flex flex-col gap-4">
      <PhoneDuplicateWarning pupilId={pupilId} />
      <ContactsForm key={JSON.stringify(contacts.data.items)} pupilId={pupilId} items={contacts.data.items} canEdit={canEdit} />
    </div>
  );
}

function ContactsForm({ pupilId, items, canEdit }: { pupilId: string; items: PupilContactDto[]; canEdit: boolean }) {
  const save = useSaveContacts(pupilId);
  const [draft, setDraft] = useState<Draft>(() => toDraft(items));
  const [primary, setPrimary] = useState<ContactRole | null>(() => items.find((item) => item.isPrimaryContact)?.role ?? null);

  const set = (role: ContactRole, field: keyof Draft[ContactRole], value: string) =>
    setDraft((current) => ({ ...current, [role]: { ...current[role], [field]: value } }));
  // A guardian's own relationship ("Aunt") says more than the slot's name does; a parent's slot name is the relationship.
  const copyInto = (target: EmergencyRole, from: CopySource) =>
    setDraft((current) => ({
      ...current,
      [target]: {
        ...blank,
        fullName: current[from].fullName,
        phone: current[from].phone,
        email: current[from].email,
        relationship: (from === 'Guardian' && current.Guardian.relationship.trim()) || from,
      },
    }));

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
              {isEmergency(slot.role) && canEdit ? (
                <div className="flex flex-wrap gap-2">
                  {COPY_SOURCES.filter((from) => draft[from].fullName.trim() !== '').map((from) => (
                    <Button key={from} type="button" variant="ghost" size="sm" onClick={() => copyInto(slot.role as EmergencyRole, from)}>
                      Copy from {from.toLowerCase()}
                    </Button>
                  ))}
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
      <FormError message={errorText(save.error)} />
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
