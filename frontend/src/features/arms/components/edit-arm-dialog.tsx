import { zodResolver } from '@hookform/resolvers/zod';
import { Controller, useForm } from 'react-hook-form';
import { Button } from '@/components/ui/button';
import { Dialog, DialogContent, DialogFooter, DialogHeader, DialogTitle } from '@/components/ui/dialog';
import { Field, FieldError, FieldLabel } from '@/components/ui/field';
import { Input } from '@/components/ui/input';
import { ApiError } from '@/lib/http';
import { hasFieldError } from '@/shared/forms/field-message';
import { useArm, useUpdateArm } from '../api';
import { editArmSchema, type EditArmFormValues } from '../arm-schema';
import type { ArmDto } from '../types';
import { FormTeacherField } from './form-teacher-field';

const FIELDS = ['label', 'capacity', 'formTeacherAdminId', 'status'] as const;

/**
 * `PATCH /api/v1/arms/{id}` (spec 6.4.3, 6.4.7). The form-teacher control is
 * absent entirely for a caller without `arm.formteacher.assign` — never
 * disabled-and-visible (TASK-0041's nav ruling, applied here to a field) —
 * and `FormTeacherField` itself further branches on whether the caller can
 * browse admin names at all (see that file's own doc comment).
 */
export function EditArmDialog({
  arm,
  canAssignFormTeacher,
  canResolveFormTeacherNames,
  currentFormTeacherName,
  onClose,
}: {
  arm: ArmDto;
  canAssignFormTeacher: boolean;
  canResolveFormTeacherNames: boolean;
  currentFormTeacherName: string | undefined;
  onClose: () => void;
}) {
  const updateArm = useUpdateArm();
  // Refetches this one arm on open, so an edit starts from current data
  // rather than whatever the list happened to cache.
  const detail = useArm(arm.id);
  const source = detail.data ?? arm;
  const {
    register,
    control,
    handleSubmit,
    formState: { errors },
  } = useForm<EditArmFormValues>({
    resolver: zodResolver(editArmSchema),
    values: {
      label: source.label,
      capacity: String(source.capacity),
      formTeacherAdminId: source.formTeacherAdminId ?? '',
      status: '',
    },
  });

  const onSubmit = handleSubmit((values) => {
    updateArm.mutate(
      {
        id: arm.id,
        label: values.label || null,
        capacity: values.capacity === '' ? null : Number(values.capacity),
        formTeacherAdminId: canAssignFormTeacher ? values.formTeacherAdminId : null,
        status: values.status === '' ? null : values.status,
      },
      { onSuccess: onClose },
    );
  });

  const error = updateArm.error;
  const formError =
    error instanceof ApiError && (error.kind !== 'validation' || !hasFieldError(error.fieldErrors, FIELDS))
      ? error.message
      : null;

  return (
    <Dialog open onOpenChange={(open) => !open && onClose()}>
      <DialogContent>
        <DialogHeader>
          <DialogTitle>Edit {arm.displayName}</DialogTitle>
        </DialogHeader>

        <form onSubmit={onSubmit} className="flex flex-col gap-4" noValidate>
          {formError ? (
            <p role="alert" className="rounded-md bg-destructive/10 px-3 py-2 text-sm text-destructive">
              {formError}
            </p>
          ) : null}

          <Field invalid={!!errors.label}>
            <FieldLabel>Label</FieldLabel>
            <Input {...register('label')} />
            <FieldError match={true}>{errors.label?.message}</FieldError>
          </Field>

          <Field invalid={!!errors.capacity}>
            <FieldLabel>Capacity</FieldLabel>
            <Input type="number" min={1} max={100} {...register('capacity')} />
            <FieldError match={true}>{errors.capacity?.message}</FieldError>
          </Field>

          {canAssignFormTeacher ? (
            <Controller
              control={control}
              name="formTeacherAdminId"
              render={({ field }) => (
                <FormTeacherField
                  value={field.value}
                  onChange={field.onChange}
                  currentName={currentFormTeacherName}
                  canResolve={canResolveFormTeacherNames}
                />
              )}
            />
          ) : null}

          <div className="flex flex-col gap-1.5">
            <label htmlFor="arm-status" className="text-sm font-medium text-foreground">
              Status
            </label>
            <select
              id="arm-status"
              className="h-10 w-full rounded-md border border-input bg-surface px-3 text-sm text-foreground"
              {...register('status')}
            >
              <option value="">Leave unchanged</option>
              <option value="Active">Active</option>
              <option value="Inactive">Inactive</option>
            </select>
          </div>

          <DialogFooter>
            <Button type="button" variant="ghost" onClick={onClose}>
              Cancel
            </Button>
            <Button type="submit" disabled={updateArm.isPending}>
              {updateArm.isPending ? 'Saving…' : 'Save changes'}
            </Button>
          </DialogFooter>
        </form>
      </DialogContent>
    </Dialog>
  );
}
