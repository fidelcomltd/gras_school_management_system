import { zodResolver } from '@hookform/resolvers/zod';
import { Controller, useForm } from 'react-hook-form';
import { Button } from '@/components/ui/button';
import { Dialog, DialogContent, DialogFooter, DialogHeader, DialogTitle } from '@/components/ui/dialog';
import { Field, FieldError, FieldLabel } from '@/components/ui/field';
import { Input } from '@/components/ui/input';
import { ApiError } from '@/lib/http';
import { hasFieldError } from '@/shared/forms/field-message';
import { usePrivileges, useRole, useUpdateRole } from '../api';
import { editRoleSchema, type EditRoleFormValues } from '../role-schema';
import type { RoleDto } from '../types';
import { PrivilegePicker } from './privilege-picker';

const FIELDS = ['name', 'description', 'privileges', 'status'] as const;

/**
 * `PATCH /api/v1/roles/{id}` (TASK-0028 §2). A system role (the seeded Super
 * Admin) rejects the WHOLE request with 409 regardless of which fields it
 * touches — surfaced verbatim below, never paraphrased (AC). `GET
 * /roles/{id}` refetches on open, mirroring `EditLevelDialog`'s own reasoning:
 * an edit starts from current data, not whatever the list happened to cache.
 */
export function EditRoleDialog({ role, onClose }: { role: RoleDto; onClose: () => void }) {
  const updateRole = useUpdateRole();
  const privileges = usePrivileges();
  const detail = useRole(role.id);
  const source = detail.data ?? role;
  const {
    register,
    control,
    handleSubmit,
    formState: { errors },
  } = useForm<EditRoleFormValues>({
    resolver: zodResolver(editRoleSchema),
    values: {
      name: source.name,
      description: source.description ?? '',
      privileges: source.privileges,
      status: source.status,
    },
  });

  const onSubmit = handleSubmit((values) => {
    updateRole.mutate(
      {
        id: role.id,
        name: values.name,
        description: values.description,
        privileges: values.privileges,
        status: values.status,
      },
      { onSuccess: onClose },
    );
  });

  const error = updateRole.error;
  const formError =
    error instanceof ApiError && (error.kind !== 'validation' || !hasFieldError(error.fieldErrors, FIELDS))
      ? error.message
      : null;

  return (
    <Dialog open onOpenChange={(open) => !open && onClose()}>
      <DialogContent>
        <DialogHeader>
          <DialogTitle>Edit {role.name}</DialogTitle>
        </DialogHeader>

        <form onSubmit={onSubmit} className="flex max-h-[70vh] flex-col gap-4 overflow-y-auto" noValidate>
          {formError ? (
            <p role="alert" className="rounded-md bg-destructive/10 px-3 py-2 text-sm text-destructive">
              {formError}
            </p>
          ) : null}

          <Field invalid={!!errors.name}>
            <FieldLabel>Role name</FieldLabel>
            <Input disabled={role.isSystem} {...register('name')} />
            <FieldError match={true}>{errors.name?.message}</FieldError>
          </Field>

          <Field invalid={!!errors.description}>
            <FieldLabel>Description</FieldLabel>
            <Input disabled={role.isSystem} {...register('description')} />
            <FieldError match={true}>{errors.description?.message}</FieldError>
          </Field>

          <Field invalid={!!errors.privileges}>
            <FieldLabel>Privileges</FieldLabel>
            <Controller
              control={control}
              name="privileges"
              render={({ field }) => (
                <PrivilegePicker groups={privileges.data?.groups ?? []} value={field.value} onChange={field.onChange} />
              )}
            />
            <FieldError match={true}>{errors.privileges?.message}</FieldError>
          </Field>

          {!role.isSystem ? (
            <div className="flex flex-col gap-1.5">
              <label htmlFor="role-status" className="text-sm font-medium text-foreground">
                Status
              </label>
              <select
                id="role-status"
                className="h-10 w-full rounded-md border border-input bg-surface px-3 text-sm text-foreground"
                {...register('status')}
              >
                <option value="Active">Active</option>
                <option value="Archived">Archived</option>
              </select>
            </div>
          ) : null}

          <DialogFooter>
            <Button type="button" variant="ghost" onClick={onClose}>
              Cancel
            </Button>
            <Button type="submit" disabled={updateRole.isPending}>
              {updateRole.isPending ? 'Saving…' : 'Save changes'}
            </Button>
          </DialogFooter>
        </form>
      </DialogContent>
    </Dialog>
  );
}
