import { zodResolver } from '@hookform/resolvers/zod';
import { Field as BaseField } from '@base-ui/react/field';
import { useState } from 'react';
import { Controller, useForm, useWatch } from 'react-hook-form';
import { Button } from '@/components/ui/button';
import { DialogFooter } from '@/components/ui/dialog';
import { Field, FieldDescription, FieldError, FieldLabel } from '@/components/ui/field';
import { Input } from '@/components/ui/input';
import { useArms } from '@/features/arms/api';
import { ApiError } from '@/lib/http';
import { useApproveAdmission } from '../api';
import { approveAdmissionSchema, type ApproveAdmissionFormValues } from '../admission-schema';
import type { AdmissionQueueRow, AdmissionRecordDto } from '../types';
import { ArmSelect } from './arm-select';

/**
 * `Idempotency-Key` is generated ONCE per mount, held in `useState`'s
 * initializer — this component only exists while its dialog is open
 * (`ApproveAdmissionDialog`, mounted/unmounted by `AdmissionQueueList`), so a
 * double-click or a retry after a dropped response reuses the same key, and
 * re-opening the dialog for a new decision remounts this component and
 * draws a fresh one. Never keyed off the pupil id (the card's own note).
 */
export function ApproveAdmissionForm({
  pupil,
  record,
  onClose,
}: {
  pupil: AdmissionQueueRow;
  record: AdmissionRecordDto;
  onClose: () => void;
}) {
  const [idempotencyKey] = useState(() => crypto.randomUUID());
  const approveAdmission = useApproveAdmission();
  // Never sessionId/levelId omitted or guessed — sourced from THIS record's
  // own ids, exactly as the card's AC requires.
  const arms = useArms({ sessionId: record.sessionId, levelId: record.classAdmittedInto });
  const armItems = arms.data?.pages.flatMap((page) => page.items) ?? [];

  const {
    control,
    register,
    handleSubmit,
    formState: { errors },
  } = useForm<ApproveAdmissionFormValues>({
    resolver: zodResolver(approveAdmissionSchema),
    defaultValues: {
      armId: '',
      assessmentResultRemarks: '',
      headOfSchoolConfirmed: false,
      headOfSchoolName: '',
    },
  });
  const headOfSchoolConfirmed = useWatch({ control, name: 'headOfSchoolConfirmed' });

  const onSubmit = handleSubmit((values) => {
    approveAdmission.mutate({
      id: pupil.id,
      idempotencyKey,
      armId: values.armId,
      assessmentResultRemarks:
        values.assessmentResultRemarks.trim() === '' ? null : values.assessmentResultRemarks,
      headOfSchoolConfirmed: values.headOfSchoolConfirmed,
      headOfSchoolName: values.headOfSchoolName.trim() === '' ? null : values.headOfSchoolName,
    });
  });

  if (approveAdmission.isSuccess) {
    return (
      <div className="flex flex-col gap-4">
        <output className="text-sm text-foreground">
          Approved. Registration number:{' '}
          <span className="font-semibold">{approveAdmission.data.registrationNumber ?? '—'}</span>
        </output>
        <DialogFooter>
          <Button type="button" onClick={onClose}>
            Done
          </Button>
        </DialogFooter>
      </div>
    );
  }

  const error = approveAdmission.error;
  const formError = error instanceof ApiError ? error.message : null;

  return (
    <form onSubmit={onSubmit} className="flex flex-col gap-4" noValidate>
      {formError ? (
        <p role="alert" className="rounded-md bg-destructive/10 px-3 py-2 text-sm text-destructive">
          {formError}
        </p>
      ) : null}

      <Field invalid={!!errors.armId}>
        <FieldLabel>Arm</FieldLabel>
        <Controller
          control={control}
          name="armId"
          render={({ field }) => <ArmSelect value={field.value} onChange={field.onChange} arms={armItems} />}
        />
        <FieldError match={true}>{errors.armId?.message}</FieldError>
      </Field>

      <Field>
        <FieldLabel>Assessment result</FieldLabel>
        <BaseField.Control
          render={<textarea rows={2} />}
          className="w-full rounded-md border border-input bg-surface px-3 py-2 text-sm text-foreground transition-colors placeholder:text-muted-foreground focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-ring"
          {...register('assessmentResultRemarks')}
        />
      </Field>

      <Field>
        <FieldLabel>Head of school name</FieldLabel>
        <Input {...register('headOfSchoolName')} />
        <FieldDescription>
          Leave blank to use the head of school name already on file in settings.
        </FieldDescription>
      </Field>

      <label className="flex items-center gap-2 text-sm text-foreground">
        <input type="checkbox" {...register('headOfSchoolConfirmed')} />
        The head of school confirms this admission.
      </label>
      {errors.headOfSchoolConfirmed ? (
        <p role="alert" className="text-xs font-medium text-destructive">
          {errors.headOfSchoolConfirmed.message}
        </p>
      ) : null}

      <DialogFooter>
        <Button type="button" variant="ghost" onClick={onClose}>
          Cancel
        </Button>
        <Button type="submit" disabled={!headOfSchoolConfirmed || approveAdmission.isPending}>
          {approveAdmission.isPending ? 'Approving…' : 'Approve'}
        </Button>
      </DialogFooter>
    </form>
  );
}
