import { zodResolver } from '@hookform/resolvers/zod';
import { useForm } from 'react-hook-form';
import { useNavigate } from 'react-router';
import { paths } from '@/app/router/paths';
import { Button } from '@/components/ui/button';
import { Field, FieldError, FieldLabel } from '@/components/ui/field';
import { Input } from '@/components/ui/input';
import { ApiError } from '@/lib/http';
import { useSignIn } from '../api';
import { signInSchema, type SignInFormValues } from '../sign-in-schema';

/**
 * Maps a 422's server-supplied field errors onto the form, tolerant of casing
 * (ASP.NET model-validation keys are PascalCase; the rest of the wire is
 * camelCase) — never assumed, always looked up.
 */
function fieldMessage(fieldErrors: Record<string, string[]> | undefined, name: string): string | undefined {
  if (!fieldErrors) return undefined;
  const key = Object.keys(fieldErrors).find((candidate) => candidate.toLowerCase() === name);
  return key ? fieldErrors[key]?.[0] : undefined;
}

export function SignInForm() {
  const navigate = useNavigate();
  const signIn = useSignIn();
  const {
    register,
    handleSubmit,
    setError,
    formState: { errors, isSubmitting },
  } = useForm<SignInFormValues>({ resolver: zodResolver(signInSchema) });

  const onSubmit = handleSubmit((values) => {
    signIn.mutate(values, {
      onSuccess: () => {
        void navigate(paths.root, { replace: true });
      },
      onError: (error) => {
        if (error instanceof ApiError && error.kind === 'validation') {
          const emailMessage = fieldMessage(error.fieldErrors, 'email');
          const passwordMessage = fieldMessage(error.fieldErrors, 'password');
          if (emailMessage) setError('email', { message: emailMessage });
          if (passwordMessage) setError('password', { message: passwordMessage });
        }
      },
    });
  });

  // Deliberately generic (delta §2, spec 6.1.11): wrong password, unknown email,
  // and a locked account given the wrong password all render this same message —
  // rendering anything more specific would tell an attacker which email exists.
  const formError =
    signIn.error instanceof ApiError && signIn.error.kind !== 'validation' ? signIn.error.message : null;

  return (
    <form onSubmit={onSubmit} className="flex flex-col gap-5" noValidate>
      {formError ? (
        <p role="alert" className="rounded-md bg-destructive/10 px-3 py-2 text-sm text-destructive">
          {formError}
        </p>
      ) : null}

      <Field invalid={!!errors.email}>
        <FieldLabel>Email address</FieldLabel>
        <Input type="email" autoComplete="username" {...register('email')} />
        <FieldError match={true}>{errors.email?.message}</FieldError>
      </Field>

      <Field invalid={!!errors.password}>
        <FieldLabel>Password</FieldLabel>
        <Input type="password" autoComplete="current-password" {...register('password')} />
        <FieldError match={true}>{errors.password?.message}</FieldError>
      </Field>

      <Button type="submit" disabled={isSubmitting || signIn.isPending}>
        {signIn.isPending ? 'Signing in…' : 'Sign in'}
      </Button>
    </form>
  );
}
