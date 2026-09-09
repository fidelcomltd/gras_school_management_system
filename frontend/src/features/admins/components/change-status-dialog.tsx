import { zodResolver } from '@hookform/resolvers/zod';
import { Field as BaseField } from '@base-ui/react/field';
import { Controller, useForm, useWatch } from 'react-hook-form';
import { Button } from '@/components/ui/button';
import { Dialog, DialogContent, DialogFooter, DialogHeader, DialogTitle } from '@/components/ui/dialog';
import { Field, FieldError, FieldLabel } from '@/components/ui/field';
import { Select, SelectContent, SelectItem, SelectTrigger } from '@/components/ui/select';
import { ApiError } from '@/lib/http';
import { useChangeAdminStatus } from '../api';
import { changeStatusSchema, type ChangeStatusFormValues } from '../admin-schema';
import type { AdminAccountDetailDto, AdminAccountStatus } from '../types';

/**
 * `POST /api/v1/admins/{id}/status` (spec 6.1.10). `targets` is computed by
 * `AdminDetailScreen` from the account's current status and the signed-in
 * caller's own privileges — this dialog only ever offers a transition that is
 * actually legal, never a disabled option it would reject (TASK-0041 ruling).
 * A reason is required only when moving to `Deactivated` (spec 6.1.12).
 */
export function ChangeStatusDialog({
  admin,
  targets,
  onClose,
}: {
  admin: AdminAccountDetailDto;
  targets: AdminAccountStatus[];
  onClose: () => void;
}) {
  const changeStatus = useChangeAdminStatus();
  const firstTarget = targets[0] ?? 'Active';
  const {
    register,
    control,
    handleSubmit,
    formState: { errors },
  } = useForm<ChangeStatusFormValues>({
    resolver: zodResolver(changeStatusSchema),
    defaultValues: { status: firstTarget, reason: '' },
  });
  const status = useWatch({ control, name: 'status' });

  const onSubmit = handleSubmit((values) => {
    changeStatus.mutate(
      { id: admin.id, status: values.status, reason: values.reason === '' ? null : values.reason },
      { onSuccess: onClose },
    );
  });

  const formError = changeStatus.error instanceof ApiError ? changeStatus.error.message : null;

  return (
    <Dialog open onOpenChange={(open) => !open && onClose()}>
      <DialogContent>
        <DialogHeader>
          <DialogTitle>Change status for {admin.staffName}</DialogTitle>
        </DialogHeader>

        <form onSubmit={onSubmit} className="flex flex-col gap-4" noValidate>
          {formError ? (
            <p role="alert" className="rounded-md bg-destructive/10 px-3 py-2 text-sm text-destructive">
              {formError}
            </p>
          ) : null}

          <Field invalid={!!errors.status}>
            <FieldLabel>New status</FieldLabel>
            <Controller
              control={control}
              name="status"
              render={({ field }) => (
                <Select
                  items={targets.map((target) => ({ value: target, label: target }))}
                  value={field.value}
                  onValueChange={(next) => field.onChange(next ?? firstTarget)}
                >
                  <SelectTrigger aria-label="New status" />
                  <SelectContent>
                    {targets.map((target) => (
                      <SelectItem key={target} value={target}>
                        {target}
                      </SelectItem>
                    ))}
                  </SelectContent>
                </Select>
              )}
            />
          </Field>

          {status === 'Deactivated' ? (
            <Field invalid={!!errors.reason}>
              <FieldLabel>Reason</FieldLabel>
              <BaseField.Control
                render={<textarea rows={3} />}
                className="w-full rounded-md border border-input bg-surface px-3 py-2 text-sm text-foreground transition-colors placeholder:text-muted-foreground focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-ring data-invalid:border-destructive"
                {...register('reason')}
              />
              <FieldError match={true}>{errors.reason?.message}</FieldError>
            </Field>
          ) : null}

          <DialogFooter>
            <Button type="button" variant="ghost" onClick={onClose}>
              Cancel
            </Button>
            <Button type="submit" disabled={changeStatus.isPending}>
              {changeStatus.isPending ? 'Saving…' : 'Change status'}
            </Button>
          </DialogFooter>
        </form>
      </DialogContent>
    </Dialog>
  );
}
