import { zodResolver } from '@hookform/resolvers/zod';
import { useForm } from 'react-hook-form';
import { Button } from '@/components/ui/button';
import { Dialog, DialogContent, DialogFooter, DialogHeader, DialogTitle } from '@/components/ui/dialog';
import { Field, FieldError, FieldLabel } from '@/components/ui/field';
import { Input } from '@/components/ui/input';
import { ApiError } from '@/lib/http';
import { hasFieldError } from '@/shared/forms/field-message';
import { useUpdateSession } from '../api';
import { editSessionSchema, type EditSessionFormValues } from '../session-schema';
import type { SessionDetailDto } from '../types';

const FIELDS = ['name', 'startDate', 'endDate'] as const;

/**
 * `PATCH /api/v1/sessions/{id}` (spec 6.3.10): name and dates, while
 * `upcoming` or `active` — a `closed` session's 409 surfaces verbatim below,
 * never paraphrased. Every field is independently optional server-side; a
 * field left blank here is sent as `null`, meaning "leave unchanged".
 */
export function EditSessionDialog({
  session,
  onClose,
}: {
  session: SessionDetailDto;
  onClose: () => void;
}) {
  const updateSession = useUpdateSession(session.id);
  const {
    register,
    handleSubmit,
    formState: { errors },
  } = useForm<EditSessionFormValues>({
    resolver: zodResolver(editSessionSchema),
    defaultValues: { name: session.name, startDate: session.startDate, endDate: session.endDate },
  });

  const onSubmit = handleSubmit((values) => {
    updateSession.mutate(
      {
        id: session.id,
        name: values.name || null,
        startDate: values.startDate || null,
        endDate: values.endDate || null,
      },
      { onSuccess: onClose },
    );
  });

  const error = updateSession.error;
  const formError =
    error instanceof ApiError && (error.kind !== 'validation' || !hasFieldError(error.fieldErrors, FIELDS))
      ? error.message
      : null;

  return (
    <Dialog open onOpenChange={(open) => !open && onClose()}>
      <DialogContent>
        <DialogHeader>
          <DialogTitle>Edit session</DialogTitle>
        </DialogHeader>

        <form onSubmit={onSubmit} className="flex flex-col gap-4" noValidate>
          {formError ? (
            <p role="alert" className="rounded-md bg-destructive/10 px-3 py-2 text-sm text-destructive">
              {formError}
            </p>
          ) : null}

          <Field invalid={!!errors.name}>
            <FieldLabel>Session name</FieldLabel>
            <Input {...register('name')} />
            <FieldError match={true}>{errors.name?.message}</FieldError>
          </Field>

          <Field invalid={!!errors.startDate}>
            <FieldLabel>Start date</FieldLabel>
            <Input type="date" {...register('startDate')} />
          </Field>

          <Field invalid={!!errors.endDate}>
            <FieldLabel>End date</FieldLabel>
            <Input type="date" {...register('endDate')} />
          </Field>

          <DialogFooter>
            <Button type="button" variant="ghost" onClick={onClose}>
              Cancel
            </Button>
            <Button type="submit" disabled={updateSession.isPending}>
              {updateSession.isPending ? 'Saving…' : 'Save changes'}
            </Button>
          </DialogFooter>
        </form>
      </DialogContent>
    </Dialog>
  );
}
