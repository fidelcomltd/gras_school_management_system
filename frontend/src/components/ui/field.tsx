import { Field as BaseField } from '@base-ui/react/field';
import type { ComponentPropsWithRef } from 'react';
import { cn } from '@/lib/utils/cn';
import type { WithClassName } from './props';

/**
 * Label / description / error grouping for a form control.
 *
 * Wrapping an `Input` or `Select` in a `Field` is how a control gets its
 * `<label for>`, `aria-describedby`, and `data-invalid` state without any of
 * it being wired by hand. Prefer this over a bare label element.
 *
 *   <Field>
 *     <FieldLabel>Admission number</FieldLabel>
 *     <Input name="admissionNumber" required />
 *     <FieldDescription>As printed on the student's ID card.</FieldDescription>
 *     <FieldError />
 *   </Field>
 */

export function Field({
  className,
  ...props
}: WithClassName<ComponentPropsWithRef<typeof BaseField.Root>>) {
  return <BaseField.Root className={cn('flex flex-col gap-1.5', className)} {...props} />;
}

export function FieldLabel({
  className,
  ...props
}: WithClassName<ComponentPropsWithRef<typeof BaseField.Label>>) {
  return (
    <BaseField.Label
      className={cn('text-sm font-medium text-foreground', className)}
      {...props}
    />
  );
}

export function FieldDescription({
  className,
  ...props
}: WithClassName<ComponentPropsWithRef<typeof BaseField.Description>>) {
  return (
    <BaseField.Description className={cn('text-xs text-muted-foreground', className)} {...props} />
  );
}

/**
 * Renders only when the control is invalid. `aria-live` is deliberate: a
 * validation message that appears silently is invisible to a screen reader.
 */
export function FieldError({
  className,
  ...props
}: WithClassName<ComponentPropsWithRef<typeof BaseField.Error>>) {
  return (
    <BaseField.Error
      aria-live="polite"
      className={cn('text-xs font-medium text-destructive', className)}
      {...props}
    />
  );
}
