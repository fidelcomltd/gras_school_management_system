import { zodResolver } from '@hookform/resolvers/zod';
import { useForm } from 'react-hook-form';
import { Button } from '@/components/ui/button';
import { Dialog, DialogContent, DialogFooter, DialogHeader, DialogTitle } from '@/components/ui/dialog';
import { Field, FieldError, FieldLabel } from '@/components/ui/field';
import { Input } from '@/components/ui/input';
import { ApiError } from '@/lib/http';
import { hasFieldError } from '@/shared/forms/field-message';
import { useUpdateAdmin } from '../api';
import { editAdminSchema, type EditAdminFormValues } from '../admin-schema';
import type { AdminAccountDetailDto } from '../types';

const FIELDS = ['staffName', 'email', 'phone', 'isSuperAdmin'] as const;

/**
 * `PATCH /api/v1/admins/{id}` (spec 6.1.2, 6.1.9). Every field crosses the
 * wire on every submit — no partial-update semantics here (unlike roles).
 * `isSuperAdmin` is the one field with a "leave unchanged" (`null`) value,
 * shown only to a caller who already holds it themselves (spec 6.1.7 rule 4
 * — a caller cannot grant a flag they do not have). This dialog only covers
 * the `admin.update` path, not the narrower self-edit carve-out (staffName/
 * phone only) — out of scope for this card, see the task's Log.
 */
export function EditAdminDialog({
  admin,
  canGrantSuperAdmin,
  onClose,
}: {
  admin: AdminAccountDetailDto;
  canGrantSuperAdmin: boolean;
  onClose: () => void;
}) {
  const updateAdmin = useUpdateAdmin();
  const {
    register,
    handleSubmit,
    formState: { errors },
  } = useForm<EditAdminFormValues>({
    resolver: zodResolver(editAdminSchema),
    defaultValues: {
      staffName: admin.staffName,
      email: admin.email,
      phone: admin.phone ?? '',
      isSuperAdmin: '',
    },
  });

  const onSubmit = handleSubmit((values) => {
    updateAdmin.mutate(
      {
        id: admin.id,
        staffName: values.staffName,
        email: values.email,
        phone: values.phone,
        isSuperAdmin: values.isSuperAdmin === '' ? null : values.isSuperAdmin === 'true',
      },
      { onSuccess: onClose },
    );
  });

  const error = updateAdmin.error;
  const formError =
    error instanceof ApiError && (error.kind !== 'validation' || !hasFieldError(error.fieldErrors, FIELDS))
      ? error.message
      : null;

  return (
    <Dialog open onOpenChange={(open) => !open && onClose()}>
      <DialogContent>
        <DialogHeader>
          <DialogTitle>Edit {admin.staffName}</DialogTitle>
        </DialogHeader>

        <form onSubmit={onSubmit} className="flex flex-col gap-4" noValidate>
          {formError ? (
            <p role="alert" className="rounded-md bg-destructive/10 px-3 py-2 text-sm text-destructive">
              {formError}
            </p>
          ) : null}

          <Field invalid={!!errors.staffName}>
            <FieldLabel>Staff name</FieldLabel>
            <Input {...register('staffName')} />
            <FieldError match={true}>{errors.staffName?.message}</FieldError>
          </Field>

          <Field invalid={!!errors.email}>
            <FieldLabel>Email</FieldLabel>
            <Input type="email" {...register('email')} />
            <FieldError match={true}>{errors.email?.message}</FieldError>
          </Field>

          <Field invalid={!!errors.phone}>
            <FieldLabel>Phone</FieldLabel>
            <Input {...register('phone')} />
            <FieldError match={true}>{errors.phone?.message}</FieldError>
          </Field>

          {canGrantSuperAdmin ? (
            <div className="flex flex-col gap-1.5">
              <label htmlFor="admin-is-super" className="text-sm font-medium text-foreground">
                Super Admin
              </label>
              <select
                id="admin-is-super"
                className="h-10 w-full rounded-md border border-input bg-surface px-3 text-sm text-foreground"
                {...register('isSuperAdmin')}
              >
                <option value="">Leave unchanged</option>
                <option value="true">Grant Super Admin</option>
                <option value="false">Revoke Super Admin</option>
              </select>
            </div>
          ) : null}

          <DialogFooter>
            <Button type="button" variant="ghost" onClick={onClose}>
              Cancel
            </Button>
            <Button type="submit" disabled={updateAdmin.isPending}>
              {updateAdmin.isPending ? 'Saving…' : 'Save changes'}
            </Button>
          </DialogFooter>
        </form>
      </DialogContent>
    </Dialog>
  );
}
