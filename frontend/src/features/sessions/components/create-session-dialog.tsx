import { zodResolver } from '@hookform/resolvers/zod';
import { useForm } from 'react-hook-form';
import { Button } from '@/components/ui/button';
import { Dialog, DialogContent, DialogFooter, DialogHeader, DialogTitle } from '@/components/ui/dialog';
import { Field, FieldError, FieldLabel } from '@/components/ui/field';
import { Input } from '@/components/ui/input';
import { ApiError } from '@/lib/http';
import { hasFieldError } from '@/shared/forms/field-message';
import { useCreateSession } from '../api';
import { createSessionSchema, type CreateSessionFormValues } from '../session-schema';
import { TermDateFields } from './term-date-fields';

const TOP_LEVEL_FIELDS = ['name', 'startDate', 'endDate'] as const;

/**
 * `POST /api/v1/sessions` (spec 6.3.5): creates the session and its three
 * terms in one request. `Idempotency-Key` is generated fresh inside
 * `useCreateSession`'s `mutationFn` — once per `mutate()` call, i.e. once per
 * submit, never reused across distinct submits (AC).
 */
export function CreateSessionDialog({ onClose }: { onClose: () => void }) {
  const createSession = useCreateSession();
  const {
    register,
    handleSubmit,
    formState: { errors },
  } = useForm<CreateSessionFormValues>({
    resolver: zodResolver(createSessionSchema),
    defaultValues: {
      name: '',
      startDate: '',
      endDate: '',
      term1: { startDate: '', endDate: '', nextResumptionDate: '' },
      term2: { startDate: '', endDate: '', nextResumptionDate: '' },
      term3: { startDate: '', endDate: '', nextResumptionDate: '' },
    },
  });

  const onSubmit = handleSubmit((values) => {
    createSession.mutate(
      {
        name: values.name,
        startDate: values.startDate,
        endDate: values.endDate,
        term1: { ...values.term1, nextResumptionDate: values.term1.nextResumptionDate || null },
        term2: { ...values.term2, nextResumptionDate: values.term2.nextResumptionDate || null },
        term3: { ...values.term3, nextResumptionDate: values.term3.nextResumptionDate || null },
      },
      { onSuccess: onClose },
    );
  });

  // Verbatim server rejection (the eight-rule-shaped name/date validation,
  // spec 6.3.5) unless it mapped onto a field this form already shows inline.
  const error = createSession.error;
  const formError =
    error instanceof ApiError && (error.kind !== 'validation' || !hasFieldError(error.fieldErrors, TOP_LEVEL_FIELDS))
      ? error.message
      : null;

  return (
    <Dialog open onOpenChange={(open) => !open && onClose()}>
      <DialogContent>
        <DialogHeader>
          <DialogTitle>New session</DialogTitle>
        </DialogHeader>

        <form onSubmit={onSubmit} className="flex max-h-[70vh] flex-col gap-4 overflow-y-auto" noValidate>
          {formError ? (
            <p role="alert" className="rounded-md bg-destructive/10 px-3 py-2 text-sm text-destructive">
              {formError}
            </p>
          ) : null}

          <Field invalid={!!errors.name}>
            <FieldLabel>Session name</FieldLabel>
            <Input placeholder="2026/2027" {...register('name')} />
            <FieldError match={true}>{errors.name?.message}</FieldError>
          </Field>

          <Field invalid={!!errors.startDate}>
            <FieldLabel>Session start date</FieldLabel>
            <Input type="date" {...register('startDate')} />
            <FieldError match={true}>{errors.startDate?.message}</FieldError>
          </Field>

          <Field invalid={!!errors.endDate}>
            <FieldLabel>Session end date</FieldLabel>
            <Input type="date" {...register('endDate')} />
            <FieldError match={true}>{errors.endDate?.message}</FieldError>
          </Field>

          <TermDateFields ordinalLabel="First term" prefix="term1" register={register} />
          <TermDateFields ordinalLabel="Second term" prefix="term2" register={register} />
          <TermDateFields ordinalLabel="Third term" prefix="term3" register={register} />

          <DialogFooter>
            <Button type="button" variant="ghost" onClick={onClose}>
              Cancel
            </Button>
            <Button type="submit" disabled={createSession.isPending}>
              {createSession.isPending ? 'Creating…' : 'Create session'}
            </Button>
          </DialogFooter>
        </form>
      </DialogContent>
    </Dialog>
  );
}
