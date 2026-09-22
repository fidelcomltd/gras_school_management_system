import { zodResolver } from '@hookform/resolvers/zod';
import { useForm } from 'react-hook-form';
import { FormError } from '@/components/feedback/query-states';
import { Button } from '@/components/ui/button';
import { Dialog, DialogContent, DialogDescription, DialogFooter, DialogHeader, DialogTitle } from '@/components/ui/dialog';
import { Field, FieldError, FieldLabel } from '@/components/ui/field';
import { Input } from '@/components/ui/input';
import { ApiError } from '@/lib/http';
import { useCorrectRegistrationNumber } from '../api';
import { correctNumberSchema, type CorrectNumberFormValues } from '../pupil-schema';
import type { PupilDto } from '../types';

/** `POST /api/v1/pupils/{id}/registration-number` (spec 6.5.10): the only way a number changes, with a reason on record. */
export function CorrectNumberDialog({ pupil, onClose }: { pupil: PupilDto; onClose: () => void }) {
  const correct = useCorrectRegistrationNumber(pupil.id);
  const {
    register,
    handleSubmit,
    formState: { errors },
  } = useForm<CorrectNumberFormValues>({
    resolver: zodResolver(correctNumberSchema),
    defaultValues: { registrationNumber: pupil.registrationNumber ?? '', reason: '' },
  });

  const onSubmit = handleSubmit((values) =>
    correct.mutate(
      { id: pupil.id, registrationNumber: values.registrationNumber.trim(), reason: values.reason.trim() },
      { onSuccess: onClose },
    ),
  );

  return (
    <Dialog open onOpenChange={(open) => !open && onClose()}>
      <DialogContent>
        <DialogHeader>
          <DialogTitle>Correct registration number</DialogTitle>
          <DialogDescription>
            The old number is kept in the pupil's history and the change is recorded in the audit log.
          </DialogDescription>
        </DialogHeader>

        <form onSubmit={onSubmit} className="flex flex-col gap-4" noValidate>
          <FormError message={correct.error instanceof ApiError ? correct.error.message : null} />
          <Field invalid={!!errors.registrationNumber}>
            <FieldLabel>New registration number</FieldLabel>
            <Input {...register('registrationNumber')} />
            <FieldError match={true}>{errors.registrationNumber?.message}</FieldError>
          </Field>
          <Field invalid={!!errors.reason}>
            <FieldLabel>Reason</FieldLabel>
            <Input {...register('reason')} />
            <FieldError match={true}>{errors.reason?.message}</FieldError>
          </Field>
          <DialogFooter>
            <Button type="button" variant="ghost" onClick={onClose}>
              Cancel
            </Button>
            <Button type="submit" disabled={correct.isPending}>
              {correct.isPending ? 'Saving…' : 'Correct number'}
            </Button>
          </DialogFooter>
        </form>
      </DialogContent>
    </Dialog>
  );
}
