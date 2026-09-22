import { useFormContext } from 'react-hook-form';
import { Field, FieldError, FieldLabel } from '@/components/ui/field';
import { Input } from '@/components/ui/input';
import type { BiographicalFormValues } from '../pupil-schema';

/**
 * Section B of the admission form (spec 6.5.3), shared by the create and edit dialogs. Reads the form through
 * `useFormContext` so both forms (the create form extends this shape) can host it.
 */
export function BiographicalFields() {
  const {
    register,
    formState: { errors },
  } = useFormContext<BiographicalFormValues>();

  const text = (name: keyof BiographicalFormValues, label: string, type = 'text') => (
    <Field invalid={!!errors[name]}>
      <FieldLabel>{label}</FieldLabel>
      <Input type={type} {...register(name)} />
      <FieldError match={true}>{errors[name]?.message}</FieldError>
    </Field>
  );

  return (
    <fieldset className="flex flex-col gap-3">
      <legend className="mb-1 text-sm font-semibold text-foreground">Pupil</legend>
      <div className="grid gap-3 sm:grid-cols-3">
        {text('surname', 'Surname')}
        {text('firstName', 'First name')}
        {text('middleName', 'Middle name (optional)')}
      </div>

      <fieldset className="flex flex-col gap-1">
        <legend className="text-sm font-medium text-foreground">Sex</legend>
        <div className="flex gap-4">
          {(['Male', 'Female'] as const).map((sex) => (
            <label key={sex} className="flex items-center gap-2 text-sm text-foreground">
              <input type="radio" value={sex} {...register('sex')} />
              {sex}
            </label>
          ))}
        </div>
        {errors.sex ? (
          <p role="alert" className="text-sm text-destructive">
            {errors.sex.message}
          </p>
        ) : null}
      </fieldset>

      <div className="grid gap-3 sm:grid-cols-2">
        {text('dateOfBirth', 'Date of birth', 'date')}
        {text('nationality', 'Nationality (defaults to Nigerian)')}
        {text('stateOfOrigin', 'State of origin')}
        {text('lga', 'LGA')}
      </div>
      {text('homeAddress', 'Home address')}
      <div className="grid gap-3 sm:grid-cols-2">
        {text('previousSchool', 'Previous school (optional)')}
        {text('previousClass', 'Previous class (optional)')}
      </div>
      {text('otherInformation', 'Other information (optional)')}
    </fieldset>
  );
}
