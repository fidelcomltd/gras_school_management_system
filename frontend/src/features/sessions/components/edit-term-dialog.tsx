import { zodResolver } from '@hookform/resolvers/zod';
import { useForm } from 'react-hook-form';
import { Button } from '@/components/ui/button';
import { Dialog, DialogContent, DialogFooter, DialogHeader, DialogTitle } from '@/components/ui/dialog';
import { Field, FieldLabel } from '@/components/ui/field';
import { Input } from '@/components/ui/input';
import { ApiError } from '@/lib/http';
import { hasFieldError } from '@/shared/forms/field-message';
import { useUpdateTerm } from '../api';
import { editTermSchema, type EditTermFormValues } from '../term-schema';
import type { TermDto } from '../types';

const FIELDS = ['name', 'startDate', 'endDate', 'timesSchoolOpened', 'nextResumptionDate'] as const;

/**
 * `PATCH /api/v1/terms/{id}` (spec 6.3.10). `timesSchoolOpened` is rejected
 * once the term is closed — that 409/422 surfaces verbatim below.
 */
export function EditTermDialog({
  sessionId,
  term,
  onClose,
}: {
  sessionId: string;
  term: TermDto;
  onClose: () => void;
}) {
  const updateTerm = useUpdateTerm(sessionId);
  const {
    register,
    handleSubmit,
    formState: { errors },
  } = useForm<EditTermFormValues>({
    resolver: zodResolver(editTermSchema),
    defaultValues: {
      name: term.name,
      startDate: term.startDate,
      endDate: term.endDate,
      timesSchoolOpened: term.timesSchoolOpened === null ? '' : String(term.timesSchoolOpened),
      nextResumptionDate: term.nextResumptionDate ?? '',
    },
  });

  const onSubmit = handleSubmit((values) => {
    updateTerm.mutate(
      {
        id: term.id,
        name: values.name || null,
        startDate: values.startDate || null,
        endDate: values.endDate || null,
        timesSchoolOpened: values.timesSchoolOpened === '' ? null : Number(values.timesSchoolOpened),
        nextResumptionDate: values.nextResumptionDate || null,
      },
      { onSuccess: onClose },
    );
  });

  const error = updateTerm.error;
  const formError =
    error instanceof ApiError && (error.kind !== 'validation' || !hasFieldError(error.fieldErrors, FIELDS))
      ? error.message
      : null;

  return (
    <Dialog open onOpenChange={(open) => !open && onClose()}>
      <DialogContent>
        <DialogHeader>
          <DialogTitle>Edit {term.name}</DialogTitle>
        </DialogHeader>

        <form onSubmit={onSubmit} className="flex flex-col gap-4" noValidate>
          {formError ? (
            <p role="alert" className="rounded-md bg-destructive/10 px-3 py-2 text-sm text-destructive">
              {formError}
            </p>
          ) : null}

          <Field invalid={!!errors.name}>
            <FieldLabel>Label</FieldLabel>
            <Input {...register('name')} />
          </Field>

          <Field invalid={!!errors.startDate}>
            <FieldLabel>Start date</FieldLabel>
            <Input type="date" {...register('startDate')} />
          </Field>

          <Field invalid={!!errors.endDate}>
            <FieldLabel>End date</FieldLabel>
            <Input type="date" {...register('endDate')} />
          </Field>

          <Field invalid={!!errors.timesSchoolOpened}>
            <FieldLabel>Times school opened</FieldLabel>
            <Input type="number" min={1} max={200} {...register('timesSchoolOpened')} />
          </Field>

          <Field invalid={!!errors.nextResumptionDate}>
            <FieldLabel>Next resumption date</FieldLabel>
            <Input type="date" {...register('nextResumptionDate')} />
          </Field>

          <DialogFooter>
            <Button type="button" variant="ghost" onClick={onClose}>
              Cancel
            </Button>
            <Button type="submit" disabled={updateTerm.isPending}>
              {updateTerm.isPending ? 'Saving…' : 'Save changes'}
            </Button>
          </DialogFooter>
        </form>
      </DialogContent>
    </Dialog>
  );
}
