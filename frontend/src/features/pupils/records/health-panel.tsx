import { useState } from 'react';
import { FormError, LoadingState, QueryErrorState } from '@/components/feedback/query-states';
import { Button } from '@/components/ui/button';
import { ApiError } from '@/lib/http';
import { LabelledSelect } from '@/shared/pickers/labelled-select';
import { useHealth, useSaveHealth, type BloodGroup, type Genotype, type PupilHealthDto } from './api';
import { TextField, YesNo } from './fields';

const BLOOD_GROUPS: { value: BloodGroup; label: string }[] = [
  { value: 'APositive', label: 'A+' },
  { value: 'ANegative', label: 'A-' },
  { value: 'BPositive', label: 'B+' },
  { value: 'BNegative', label: 'B-' },
  { value: 'AbPositive', label: 'AB+' },
  { value: 'AbNegative', label: 'AB-' },
  { value: 'OPositive', label: 'O+' },
  { value: 'ONegative', label: 'O-' },
];
const GENOTYPES: Genotype[] = ['AA', 'AS', 'SS', 'AC', 'SC'];

/** Section F (spec 6.5.7). Safeguarding data: every time it is opened, the server records who looked. */
export function HealthPanel({ pupilId, canEdit }: { pupilId: string; canEdit: boolean }) {
  const health = useHealth(pupilId, true);
  if (health.isPending) return <LoadingState label="Loading health…" />;
  if (health.isError) return <QueryErrorState error={health.error} onRetry={() => void health.refetch()} />;
  return <HealthForm key={JSON.stringify(health.data)} pupilId={pupilId} data={health.data} canEdit={canEdit} />;
}

function HealthForm({ pupilId, data, canEdit }: { pupilId: string; data: PupilHealthDto; canEdit: boolean }) {
  const save = useSaveHealth(pupilId);
  const [form, setForm] = useState({
    hasAllergy: data.hasAllergy ?? null,
    allergyDetails: data.allergyDetails ?? '',
    hasMedicalCondition: data.hasMedicalCondition ?? null,
    medicalConditionDetails: data.medicalConditionDetails ?? '',
    takesRegularMedication: data.takesRegularMedication ?? null,
    medicationDetails: data.medicationDetails ?? '',
    specialInstructions: data.specialInstructions ?? '',
    preferredHospital: data.preferredHospital ?? '',
    hospitalPhone: (data.hospitalPhone ?? '').replace(/^\+234/, '0'),
    bloodGroup: (data.bloodGroup ?? '') as BloodGroup | '',
    genotype: (data.genotype ?? '') as Genotype | '',
  });
  const set = <K extends keyof typeof form>(key: K, value: (typeof form)[K]) => setForm((current) => ({ ...current, [key]: value }));

  return (
    <fieldset disabled={!canEdit} className="flex flex-col gap-4">
      <p className="text-xs text-muted-foreground">Safeguarding information. Never on a result sheet, the portal, or a general export; every view is logged.</p>
      <YesNo legend="Does the child have any allergy?" value={form.hasAllergy} onChange={(value) => set('hasAllergy', value)} />
      {form.hasAllergy ? <TextField label="Allergy details" multiline value={form.allergyDetails} onChange={(value) => set('allergyDetails', value)} /> : null}
      <YesNo legend="Does the child have any medical condition?" value={form.hasMedicalCondition} onChange={(value) => set('hasMedicalCondition', value)} />
      {form.hasMedicalCondition ? (
        <TextField label="Medical condition details" multiline value={form.medicalConditionDetails} onChange={(value) => set('medicalConditionDetails', value)} />
      ) : null}
      <YesNo legend="Does the child take regular medication?" value={form.takesRegularMedication} onChange={(value) => set('takesRegularMedication', value)} />
      {form.takesRegularMedication ? (
        <TextField label="Medication details" multiline value={form.medicationDetails} onChange={(value) => set('medicationDetails', value)} />
      ) : null}
      <TextField label="Special health, dietary or safety instructions" multiline value={form.specialInstructions} onChange={(value) => set('specialInstructions', value)} />
      <p className="text-xs text-muted-foreground">A blank hospital here is a blank on the day it is needed.</p>
      <div className="grid gap-3 sm:grid-cols-2">
        <TextField label="Preferred hospital" value={form.preferredHospital} onChange={(value) => set('preferredHospital', value)} />
        <TextField label="Hospital phone" inputMode="tel" value={form.hospitalPhone} onChange={(value) => set('hospitalPhone', value)} />
      </div>
      <div className="flex flex-wrap gap-3">
        <LabelledSelect label="Blood group" placeholder="Blood group" value={form.bloodGroup} options={BLOOD_GROUPS} onChange={(value) => set('bloodGroup', value as BloodGroup)} className="w-40" />
        <LabelledSelect
          label="Genotype"
          placeholder="Genotype"
          value={form.genotype}
          options={GENOTYPES.map((value) => ({ value, label: value }))}
          onChange={(value) => set('genotype', value as Genotype)}
          className="w-40"
        />
      </div>
      <FormError message={save.error instanceof ApiError ? save.error.message : null} />
      {canEdit ? (
        <Button
          className="self-start"
          disabled={save.isPending}
          onClick={() =>
            save.mutate({
              hasAllergy: form.hasAllergy,
              allergyDetails: form.allergyDetails || null,
              hasMedicalCondition: form.hasMedicalCondition,
              medicalConditionDetails: form.medicalConditionDetails || null,
              takesRegularMedication: form.takesRegularMedication,
              medicationDetails: form.medicationDetails || null,
              specialInstructions: form.specialInstructions || null,
              preferredHospital: form.preferredHospital || null,
              hospitalPhone: form.hospitalPhone || null,
              bloodGroup: form.bloodGroup || null,
              genotype: form.genotype || null,
            })
          }
        >
          {save.isPending ? 'Saving…' : 'Save health'}
        </Button>
      ) : null}
    </fieldset>
  );
}
