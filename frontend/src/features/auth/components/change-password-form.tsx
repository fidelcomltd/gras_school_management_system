import { zodResolver } from '@hookform/resolvers/zod';
import { useForm } from 'react-hook-form';
import { Button } from '@/components/ui/button';
import { Field, FieldDescription, FieldError, FieldLabel } from '@/components/ui/field';
import { Input } from '@/components/ui/input';
import { ApiError } from '@/lib/http';
import { fieldMessage, hasFieldError } from '@/shared/forms/field-message';
import { useChangePassword } from '../api';
import { changePasswordSchema, PASSWORD_MIN_LENGTH, type ChangePasswordFormValues } from '../change-password-schema';

const FIELDS = ['currentPassword', 'newPassword'] as const;

/**
 * `POST /api/v1/auth/password`. Other sessions of the account are signed out; this one stays signed in.
 * `showSuccess` confirms the change in place; the forced screen turns it off because the app opens instead.
 */
export function ChangePasswordForm({ showSuccess = true }: { showSuccess?: boolean }) {
  const changePassword = useChangePassword();
  const {
    register,
    handleSubmit,
    setError,
    reset,
    formState: { errors },
  } = useForm<ChangePasswordFormValues>({
    resolver: zodResolver(changePasswordSchema),
    defaultValues: { currentPassword: '', newPassword: '', confirmPassword: '' },
  });

  const onSubmit = handleSubmit((values) =>
    changePassword.mutate(
      { currentPassword: values.currentPassword, newPassword: values.newPassword },
      {
        onSuccess: () => reset(),
        onError: (error) => {
          if (error instanceof ApiError && error.errorCode === 'auth.current_password_incorrect') {
            setError('currentPassword', { message: error.message });
          } else if (error instanceof ApiError && error.kind === 'validation') {
            const current = fieldMessage(error.fieldErrors, 'currentPassword');
            const next = fieldMessage(error.fieldErrors, 'newPassword');
            if (current) setError('currentPassword', { message: current });
            if (next) setError('newPassword', { message: next });
          }
        },
      },
    ),
  );

  const error = changePassword.error;
  // Shown on the fields where the server named one; otherwise, including a 422 naming neither, as a banner.
  const onField =
    error instanceof ApiError &&
    (error.errorCode === 'auth.current_password_incorrect' ||
      (error.kind === 'validation' && hasFieldError(error.fieldErrors, FIELDS)));
  const formError = error instanceof ApiError && !onField ? error.message : null;

  return (
    <form onSubmit={onSubmit} className="flex flex-col gap-5" noValidate>
      {formError ? (
        <p role="alert" className="rounded-md bg-destructive/10 px-3 py-2 text-sm text-destructive">
          {formError}
        </p>
      ) : null}
      {changePassword.isSuccess && showSuccess ? (
        <output className="rounded-md bg-primary/10 px-3 py-2 text-sm text-foreground">
          Password changed. Any other devices signed in to your account have been signed out.
        </output>
      ) : null}

      <Field invalid={!!errors.currentPassword}>
        <FieldLabel>Current password</FieldLabel>
        <Input type="password" autoComplete="current-password" {...register('currentPassword')} />
        <FieldError match={true}>{errors.currentPassword?.message}</FieldError>
      </Field>

      <Field invalid={!!errors.newPassword}>
        <FieldLabel>New password</FieldLabel>
        <Input type="password" autoComplete="new-password" {...register('newPassword')} />
        <FieldDescription>
          At least {PASSWORD_MIN_LENGTH} characters, with a letter and a digit. It cannot be one of your last five passwords.
        </FieldDescription>
        <FieldError match={true}>{errors.newPassword?.message}</FieldError>
      </Field>

      <Field invalid={!!errors.confirmPassword}>
        <FieldLabel>Confirm new password</FieldLabel>
        <Input type="password" autoComplete="new-password" {...register('confirmPassword')} />
        <FieldError match={true}>{errors.confirmPassword?.message}</FieldError>
      </Field>

      <Button type="submit" className="self-start" disabled={changePassword.isPending}>
        {changePassword.isPending ? 'Changing…' : 'Change password'}
      </Button>
    </form>
  );
}
