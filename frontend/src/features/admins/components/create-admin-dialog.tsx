import { useState } from 'react';
import { zodResolver } from '@hookform/resolvers/zod';
import { useForm } from 'react-hook-form';
import { Button } from '@/components/ui/button';
import { Dialog, DialogContent, DialogFooter, DialogHeader, DialogTitle } from '@/components/ui/dialog';
import { Field, FieldError, FieldLabel } from '@/components/ui/field';
import { Input } from '@/components/ui/input';
import { ApiError } from '@/lib/http';
import { hasFieldError } from '@/shared/forms/field-message';
import { useCreateAdmin } from '../api';
import { createAdminSchema, type CreateAdminFormValues } from '../admin-schema';
import { TemporaryPasswordReveal } from './temporary-password-reveal';

const FIELDS = ['staffName', 'email', 'phone'] as const;

/**
 * `POST /api/v1/admins` (spec 6.1.9 step 1, 6.1.14). Role assignment (step
 * two) is TASK-0028's own scope, not this dialog's — an account with zero
 * assignments can exist and sign in.
 *
 * The generated temporary password is credential material (TASK-0043):
 * captured into this component's own `reveal` state only, and the mutation's
 * `reset()` is called in the same tick so nothing survives in TanStack
 * Query's mutation cache once it has been shown here.
 */
export function CreateAdminDialog({ onClose }: { onClose: () => void }) {
  const createAdmin = useCreateAdmin();
  const [reveal, setReveal] = useState<{ staffName: string; temporaryPassword: string } | null>(null);
  const {
    register,
    handleSubmit,
    formState: { errors },
  } = useForm<CreateAdminFormValues>({
    resolver: zodResolver(createAdminSchema),
    defaultValues: { staffName: '', email: '', phone: '' },
  });

  const onSubmit = handleSubmit((values) => {
    createAdmin.mutate(values, {
      onSuccess: (account) => {
        setReveal({ staffName: account.staffName, temporaryPassword: account.temporaryPassword ?? '' });
        createAdmin.reset();
      },
    });
  });

  const error = createAdmin.error;
  const formError =
    error instanceof ApiError && (error.kind !== 'validation' || !hasFieldError(error.fieldErrors, FIELDS))
      ? error.message
      : null;

  return (
    <Dialog open onOpenChange={(open) => !open && onClose()}>
      <DialogContent>
        <DialogHeader>
          <DialogTitle>{reveal ? `Temporary password for ${reveal.staffName}` : 'New admin account'}</DialogTitle>
        </DialogHeader>

        {reveal ? (
          <TemporaryPasswordReveal password={reveal.temporaryPassword} onDone={onClose} />
        ) : (
          <form onSubmit={onSubmit} className="flex flex-col gap-4" noValidate>
            {formError ? (
              <p role="alert" className="rounded-md bg-destructive/10 px-3 py-2 text-sm text-destructive">
                {formError}
              </p>
            ) : null}

            <Field invalid={!!errors.staffName}>
              <FieldLabel>Staff name</FieldLabel>
              <Input placeholder="Ngozi Adeyemi" {...register('staffName')} />
              <FieldError match={true}>{errors.staffName?.message}</FieldError>
            </Field>

            <Field invalid={!!errors.email}>
              <FieldLabel>Email</FieldLabel>
              <Input type="email" placeholder="ngozi.adeyemi@example.com" {...register('email')} />
              <FieldError match={true}>{errors.email?.message}</FieldError>
            </Field>

            <Field invalid={!!errors.phone}>
              <FieldLabel>Phone</FieldLabel>
              <Input placeholder="08012345678" {...register('phone')} />
              <FieldError match={true}>{errors.phone?.message}</FieldError>
            </Field>

            <DialogFooter>
              <Button type="button" variant="ghost" onClick={onClose}>
                Cancel
              </Button>
              <Button type="submit" disabled={createAdmin.isPending}>
                {createAdmin.isPending ? 'Creating…' : 'Create admin'}
              </Button>
            </DialogFooter>
          </form>
        )}
      </DialogContent>
    </Dialog>
  );
}
