import type { UseFormRegister } from 'react-hook-form';
import { Field, FieldLabel } from '@/components/ui/field';
import type { CreateSessionFormValues } from '../session-schema';
import { DateInput } from '@/components/ui/date-input';

/**
 * One term's three date fields, reused three times by `CreateSessionDialog`
 * (`term1`/`term2`/`term3`) so that form isn't 3x the markup for what is
 * otherwise identical shape (spec 6.3.5: "The form takes the session name,
 * then three rows of start date, end date and next resumption date").
 */
export function TermDateFields({
  ordinalLabel,
  prefix,
  register,
}: {
  ordinalLabel: string;
  prefix: 'term1' | 'term2' | 'term3';
  register: UseFormRegister<CreateSessionFormValues>;
}) {
  return (
    <fieldset className="flex flex-col gap-3 rounded-md border border-border p-3">
      <legend className="px-1 text-sm font-medium text-foreground">{ordinalLabel}</legend>

      <Field>
        <FieldLabel>{ordinalLabel} start date</FieldLabel>
        <DateInput {...register(`${prefix}.startDate`)} />
      </Field>

      <Field>
        <FieldLabel>{ordinalLabel} end date</FieldLabel>
        <DateInput {...register(`${prefix}.endDate`)} />
      </Field>

      <Field>
        <FieldLabel>{ordinalLabel}: next term begins (printed on its results)</FieldLabel>
        <DateInput {...register(`${prefix}.nextResumptionDate`)} />
      </Field>
    </fieldset>
  );
}
