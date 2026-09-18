import { zodResolver } from '@hookform/resolvers/zod';
import { useForm } from 'react-hook-form';
import { Button } from '@/components/ui/button';
import { Field, FieldError, FieldLabel } from '@/components/ui/field';
import { Input } from '@/components/ui/input';
import { ApiError } from '@/lib/http';
import { fieldMessage } from '@/shared/forms/field-message';
import { useUpdateSchoolIdentity } from '../api';
import { schoolIdentitySchema, type SchoolIdentityFormValues } from '../identity-schema';
import type { SettingsIdentityGroupDto } from '../types';

const EDITABLE_FIELDS = [
  'schoolName',
  'shortName',
  'address',
  'phone',
  'email',
  'motto',
  'headTeacherName',
] as const;

export function SchoolIdentityForm({
  identity,
  onSaved,
}: {
  identity: SettingsIdentityGroupDto;
  onSaved: () => void;
}) {
  const updateIdentity = useUpdateSchoolIdentity();
  const {
    register,
    handleSubmit,
    setError,
    formState: { errors, isSubmitting },
  } = useForm<SchoolIdentityFormValues>({
    resolver: zodResolver(schoolIdentitySchema),
    defaultValues: {
      schoolName: identity.schoolName,
      shortName: identity.shortName,
      address: identity.address,
      phone: identity.phone,
      email: identity.email,
      motto: identity.motto ?? '',
      headTeacherName: identity.headTeacherName,
    },
  });

  const onSubmit = handleSubmit((values) => {
    updateIdentity.mutate(
      {
        schoolName: values.schoolName,
        shortName: values.shortName,
        address: values.address,
        phone: values.phone,
        email: values.email,
        motto: values.motto.trim() === '' ? null : values.motto,
        headTeacherName: values.headTeacherName,
        expectedVersion: identity.versionNumber,
      },
      {
        onSuccess: onSaved,
        onError: (error) => {
          if (error instanceof ApiError && error.kind === 'validation') {
            for (const field of EDITABLE_FIELDS) {
              const message = fieldMessage(error.fieldErrors, field);
              if (message) setError(field, { message });
            }
          }
        },
      },
    );
  });

  // A non-field failure: 409 stale-version conflict, 403, network/server, etc.
  const formError =
    updateIdentity.error instanceof ApiError && updateIdentity.error.kind !== 'validation'
      ? updateIdentity.error.message
      : null;

  return (
    <form onSubmit={onSubmit} className="flex flex-col gap-5" noValidate>
      {formError ? (
        <p role="alert" className="rounded-md bg-destructive/10 px-3 py-2 text-sm text-destructive">
          {formError}
        </p>
      ) : null}

      <Field invalid={!!errors.schoolName}>
        <FieldLabel>School name</FieldLabel>
        <Input {...register('schoolName')} />
        <FieldError match={true}>{errors.schoolName?.message}</FieldError>
      </Field>

      <Field invalid={!!errors.shortName}>
        <FieldLabel>Short name</FieldLabel>
        <Input {...register('shortName')} />
        <FieldError match={true}>{errors.shortName?.message}</FieldError>
      </Field>

      <Field invalid={!!errors.address}>
        <FieldLabel>Address</FieldLabel>
        <Input {...register('address')} />
        <FieldError match={true}>{errors.address?.message}</FieldError>
      </Field>

      <Field invalid={!!errors.phone}>
        <FieldLabel>Phone</FieldLabel>
        <Input {...register('phone')} />
        <FieldError match={true}>{errors.phone?.message}</FieldError>
      </Field>

      <Field invalid={!!errors.email}>
        <FieldLabel>Email</FieldLabel>
        <Input type="email" {...register('email')} />
        <FieldError match={true}>{errors.email?.message}</FieldError>
      </Field>

      <Field invalid={!!errors.motto}>
        <FieldLabel>Motto</FieldLabel>
        <Input {...register('motto')} />
        <FieldError match={true}>{errors.motto?.message}</FieldError>
      </Field>

      <Field invalid={!!errors.headTeacherName}>
        <FieldLabel>Head teacher name</FieldLabel>
        <Input {...register('headTeacherName')} />
        <FieldError match={true}>{errors.headTeacherName?.message}</FieldError>
      </Field>

      <Button type="submit" disabled={isSubmitting || updateIdentity.isPending}>
        {updateIdentity.isPending ? 'Saving…' : 'Save changes'}
      </Button>
    </form>
  );
}
