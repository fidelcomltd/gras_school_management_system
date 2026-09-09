import { zodResolver } from '@hookform/resolvers/zod';
import { Controller, useForm } from 'react-hook-form';
import { Button } from '@/components/ui/button';
import { Dialog, DialogContent, DialogFooter, DialogHeader, DialogTitle } from '@/components/ui/dialog';
import { Field, FieldError, FieldLabel } from '@/components/ui/field';
import { Input } from '@/components/ui/input';
import { ApiError } from '@/lib/http';
import { hasFieldError } from '@/shared/forms/field-message';
import { useCreateRole, usePrivileges } from '../api';
import { createRoleSchema, type CreateRoleFormValues } from '../role-schema';
import { PrivilegePicker } from './privilege-picker';

const FIELDS = ['name', 'description', 'privileges'] as const;

/**
 * `POST /api/v1/roles` (TASK-0028 §2). Rejects the reserved name
 * `Super Admin` and any unknown privilege code, naming the offender —
 * surfaced verbatim below. Every requested privilege must already be held by
 * the caller (spec 6.1.7 rule 2), enforced server-side.
 */
export function CreateRoleDialog({ onClose }: { onClose: () => void }) {
  const createRole = useCreateRole();
  const privileges = usePrivileges();
  const {
    register,
    control,
    handleSubmit,
    formState: { errors },
  } = useForm<CreateRoleFormValues>({
    resolver: zodResolver(createRoleSchema),
    defaultValues: { name: '', description: '', privileges: [] },
  });

  const onSubmit = handleSubmit((values) => {
    createRole.mutate(
      {
        name: values.name,
        description: values.description === '' ? null : values.description,
        privileges: values.privileges,
      },
      { onSuccess: onClose },
    );
  });

  const error = createRole.error;
  const formError =
    error instanceof ApiError && (error.kind !== 'validation' || !hasFieldError(error.fieldErrors, FIELDS))
      ? error.message
      : null;

  return (
    <Dialog open onOpenChange={(open) => !open && onClose()}>
      <DialogContent>
        <DialogHeader>
          <DialogTitle>New role</DialogTitle>
        </DialogHeader>

        <form onSubmit={onSubmit} className="flex max-h-[70vh] flex-col gap-4 overflow-y-auto" noValidate>
          {formError ? (
            <p role="alert" className="rounded-md bg-destructive/10 px-3 py-2 text-sm text-destructive">
              {formError}
            </p>
          ) : null}

          <Field invalid={!!errors.name}>
            <FieldLabel>Role name</FieldLabel>
            <Input placeholder="Class Teacher" {...register('name')} />
            <FieldError match={true}>{errors.name?.message}</FieldError>
          </Field>

          <Field invalid={!!errors.description}>
            <FieldLabel>Description</FieldLabel>
            <Input placeholder="Enters marks and views pupil records." {...register('description')} />
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

          <DialogFooter>
            <Button type="button" variant="ghost" onClick={onClose}>
              Cancel
            </Button>
            <Button type="submit" disabled={createRole.isPending}>
              {createRole.isPending ? 'Creating…' : 'Create role'}
            </Button>
          </DialogFooter>
        </form>
      </DialogContent>
    </Dialog>
  );
}
