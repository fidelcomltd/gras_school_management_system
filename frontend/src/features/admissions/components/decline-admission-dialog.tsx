import { zodResolver } from '@hookform/resolvers/zod';
import { Field as BaseField } from '@base-ui/react/field';
import { useForm } from 'react-hook-form';
import { Button } from '@/components/ui/button';
import { Dialog, DialogContent, DialogFooter, DialogHeader, DialogTitle } from '@/components/ui/dialog';
import { Field, FieldError, FieldLabel } from '@/components/ui/field';
import { ApiError } from '@/lib/http';
import { useDeclineAdmission } from '../api';
import { declineAdmissionSchema, type DeclineAdmissionFormValues } from '../admission-schema';
import type { AdmissionQueueRow } from '../types';

/**
 * `POST /api/v1/admissions/{id}/decline` (spec 6.5.14: pending -> withdrawn).
 * `reason` is the only input the command needs, so this dialog carries none
 * of approve's blocked arm-selection problem (see `ApproveAdmissionDialog`'s
 * own doc comment) — decline is fully buildable from what the queue row
 * already has.
 */
export function DeclineAdmissionDialog({
  pupil,
  onClose,
}: {
  pupil: AdmissionQueueRow;
  onClose: () => void;
}) {
  const declineAdmission = useDeclineAdmission();
  const {
    register,
    handleSubmit,
    formState: { errors },
  } = useForm<DeclineAdmissionFormValues>({
    resolver: zodResolver(declineAdmissionSchema),
    defaultValues: { reason: '' },
  });

  const onSubmit = handleSubmit((values) => {
    declineAdmission.mutate({ id: pupil.id, reason: values.reason }, { onSuccess: onClose });
  });

  const formError = declineAdmission.error instanceof ApiError ? declineAdmission.error.message : null;

  return (
    <Dialog open onOpenChange={(open) => !open && onClose()}>
      <DialogContent>
        <DialogHeader>
          <DialogTitle>
            Decline {pupil.surname} {pupil.firstName}
          </DialogTitle>
        </DialogHeader>

        <form onSubmit={onSubmit} className="flex flex-col gap-4" noValidate>
          {formError ? (
            <p role="alert" className="rounded-md bg-destructive/10 px-3 py-2 text-sm text-destructive">
              {formError}
            </p>
          ) : null}

          <Field invalid={!!errors.reason}>
            <FieldLabel>Reason</FieldLabel>
            <BaseField.Control
              render={<textarea rows={3} />}
              className="w-full rounded-md border border-input bg-surface px-3 py-2 text-sm text-foreground transition-colors placeholder:text-muted-foreground focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-ring data-invalid:border-destructive"
              {...register('reason')}
            />
            <FieldError match={true}>{errors.reason?.message}</FieldError>
          </Field>

          <DialogFooter>
            <Button type="button" variant="ghost" onClick={onClose}>
              Cancel
            </Button>
            <Button type="submit" variant="destructive" disabled={declineAdmission.isPending}>
              {declineAdmission.isPending ? 'Declining…' : 'Decline application'}
            </Button>
          </DialogFooter>
        </form>
      </DialogContent>
    </Dialog>
  );
}
