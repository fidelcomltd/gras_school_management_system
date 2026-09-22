import { Controller, useFormContext } from 'react-hook-form';
import { Field, FieldError, FieldLabel } from '@/components/ui/field';
import { Input } from '@/components/ui/input';
import { Select, SelectContent, SelectItem, SelectTrigger } from '@/components/ui/select';
import { useLevels } from '@/features/classes/api';
import type { CreatePupilFormValues } from '../pupil-schema';

/** Section A of the admission form as captured at creation (spec 6.5.9). The session defaults to the active one. */
export function AdmissionFields() {
  const {
    control,
    register,
    formState: { errors },
  } = useFormContext<CreatePupilFormValues>();
  const levels = useLevels();
  const options = (levels.data?.pages.flatMap((page) => page.items) ?? []).map((level) => ({ value: level.id, label: level.name }));

  return (
    <fieldset className="flex flex-col gap-3">
      <legend className="mb-1 text-sm font-semibold text-foreground">Admission</legend>

      <Field invalid={!!errors.classAdmittedInto}>
        <FieldLabel>Class admitted into</FieldLabel>
        <Controller
          control={control}
          name="classAdmittedInto"
          render={({ field }) => (
            <Select items={options} value={field.value || null} onValueChange={(next) => field.onChange(next ?? '')}>
              <SelectTrigger aria-label="Class admitted into" placeholder={levels.isPending ? 'Loading classes…' : 'Choose a class'} />
              <SelectContent>
                {options.map((option) => (
                  <SelectItem key={option.value} value={option.value}>
                    {option.label}
                  </SelectItem>
                ))}
              </SelectContent>
            </Select>
          )}
        />
        <FieldError match={true}>{errors.classAdmittedInto?.message}</FieldError>
      </Field>

      <fieldset className="flex flex-col gap-1">
        <legend className="text-sm font-medium text-foreground">Admission type</legend>
        <div className="flex gap-4">
          {(['New', 'Returning'] as const).map((type) => (
            <label key={type} className="flex items-center gap-2 text-sm text-foreground">
              <input type="radio" value={type} {...register('admissionType')} />
              {type}
            </label>
          ))}
        </div>
      </fieldset>

      <Field>
        <FieldLabel>Date application received (optional)</FieldLabel>
        <Input type="date" {...register('dateApplicationReceived')} />
      </Field>

      <label className="flex items-center gap-2 text-sm text-foreground">
        <input type="checkbox" {...register('assessmentRequired')} />
        Entrance assessment required
      </label>
    </fieldset>
  );
}
