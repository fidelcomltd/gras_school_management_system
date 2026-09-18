import { zodResolver } from '@hookform/resolvers/zod';
import { Field as BaseField } from '@base-ui/react/field';
import { useForm } from 'react-hook-form';
import { Button } from '@/components/ui/button';
import { Dialog, DialogContent, DialogFooter, DialogHeader, DialogTitle } from '@/components/ui/dialog';
import { Field, FieldError, FieldLabel } from '@/components/ui/field';
import { ApiError } from '@/lib/http';
import { useReopenTerm } from '../api';
import { reopenTermSchema, type ReopenTermFormValues } from '../term-schema';
import type { TermDto } from '../types';

/**
 * `POST /api/v1/terms/{id}/reopen` (spec 6.3.6): super-admin only (route
 * itself is only ever shown to one, per `TermCard`'s `canReopen`), a reason
 * of at least 10 characters, refused outright if the following term has
 * already opened — that refusal surfaces verbatim below, never generic.
 */
export function ReopenTermDialog({
  sessionId,
  term,
  onClose,
}: {
  sessionId: string;
  term: TermDto;
  onClose: () => void;
}) {
  const reopenTerm = useReopenTerm(sessionId);
  const {
    register,
    handleSubmit,
    formState: { errors },
  } = useForm<ReopenTermFormValues>({
    resolver: zodResolver(reopenTermSchema),
    defaultValues: { reason: '' },
  });

  const onSubmit = handleSubmit((values) => {
    reopenTerm.mutate({ id: term.id, reason: values.reason }, { onSuccess: onClose });
  });

  const formError = reopenTerm.error instanceof ApiError ? reopenTerm.error.message : null;

  return (
    <Dialog open onOpenChange={(open) => !open && onClose()}>
      <DialogContent>
        <DialogHeader>
          <DialogTitle>Reopen {term.name}</DialogTitle>
        </DialogHeader>

        <form onSubmit={onSubmit} className="flex flex-col gap-4" noValidate>
          {formError ? (
            <p role="alert" className="rounded-md bg-destructive/10 px-3 py-2 text-sm text-destructive">
              {formError}
            </p>
          ) : null}

          <Field invalid={!!errors.reason}>
            <FieldLabel>Reason</FieldLabel>
            <BaseField.Control
              render={<textarea rows={3} />}
              className="w-full rounded-md border border-input bg-surface px-3 py-2 text-sm text-foreground transition-colors placeholder:text-muted-foreground focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-ring data-invalid:border-destructive"
              {...register('reason')}
            />
            <FieldError match={true}>{errors.reason?.message}</FieldError>
          </Field>

          <DialogFooter>
            <Button type="button" variant="ghost" onClick={onClose}>
              Cancel
            </Button>
            <Button type="submit" disabled={reopenTerm.isPending}>
              {reopenTerm.isPending ? 'Reopening…' : 'Reopen term'}
            </Button>
          </DialogFooter>
        </form>
      </DialogContent>
    </Dialog>
  );
}
