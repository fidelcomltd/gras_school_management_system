import { zodResolver } from '@hookform/resolvers/zod';
import { useForm } from 'react-hook-form';
import { z } from 'zod';
import { FormError } from '@/components/feedback/query-states';
import { Button } from '@/components/ui/button';
import { Dialog, DialogContent, DialogFooter, DialogHeader, DialogTitle } from '@/components/ui/dialog';
import { Field, FieldError, FieldLabel } from '@/components/ui/field';
import { Input } from '@/components/ui/input';
import { ApiError } from '@/lib/http';
import { useCreateSubject, useUpdateSubject } from '../api';
import type { SubjectDto } from '../types';

const schema = z.object({
  name: z.string().trim().min(1, 'Name is required.').max(80, 'At most 80 characters.'),
  code: z
    .string()
    .trim()
    .max(12, 'At most 12 characters.')
    .regex(/^[A-Za-z0-9]*$/, 'Letters and digits only.'),
  description: z.string().trim().max(300, 'At most 300 characters.'),
  active: z.boolean(),
});

type Values = z.infer<typeof schema>;

/** Create (no `subject`) or edit a subject (spec 6.6.3). Deactivating needs `subject.deactivate`. */
export function SubjectDialog({
  subject,
  canDeactivate,
  onClose,
}: {
  subject?: SubjectDto | undefined;
  canDeactivate: boolean;
  onClose: () => void;
}) {
  const create = useCreateSubject();
  const update = useUpdateSubject();
  const mutation = subject ? update : create;
  const {
    register,
    handleSubmit,
    formState: { errors },
  } = useForm<Values>({
    resolver: zodResolver(schema),
    defaultValues: {
      name: subject?.name ?? '',
      code: subject?.code ?? '',
      description: subject?.description ?? '',
      active: subject ? subject.status === 'Active' : true,
    },
  });

  const onSubmit = handleSubmit((values) => {
    const code = values.code.toUpperCase() || null;
    const description = values.description || null;
    if (subject) {
      const status = values.active ? 'Active' : 'Inactive';
      update.mutate(
        { id: subject.id, name: values.name, code: code ?? '', description: description ?? '', status: status === subject.status ? null : status },
        { onSuccess: onClose },
      );
    } else {
      create.mutate({ name: values.name, code, description }, { onSuccess: onClose });
    }
  });

  return (
    <Dialog open onOpenChange={(open) => !open && onClose()}>
      <DialogContent>
        <DialogHeader>
          <DialogTitle>{subject ? 'Edit subject' : 'New subject'}</DialogTitle>
        </DialogHeader>
        <form onSubmit={onSubmit} className="flex flex-col gap-4" noValidate>
          <FormError message={mutation.error instanceof ApiError ? mutation.error.message : null} />
          <Field invalid={!!errors.name}>
            <FieldLabel>Name</FieldLabel>
            <Input {...register('name')} />
            <FieldError match={true}>{errors.name?.message}</FieldError>
          </Field>
          <Field invalid={!!errors.code}>
            <FieldLabel>Short code (optional)</FieldLabel>
            <Input {...register('code')} />
            <FieldError match={true}>{errors.code?.message}</FieldError>
          </Field>
          <Field invalid={!!errors.description}>
            <FieldLabel>Description (optional)</FieldLabel>
            <Input {...register('description')} />
            <FieldError match={true}>{errors.description?.message}</FieldError>
          </Field>
          {subject && canDeactivate ? (
            <label className="flex items-center gap-2 text-sm text-foreground">
              <input type="checkbox" {...register('active')} />
              Active (an inactive subject cannot be mapped to new terms)
            </label>
          ) : null}
          <DialogFooter>
            <Button type="button" variant="ghost" onClick={onClose}>
              Cancel
            </Button>
            <Button type="submit" disabled={mutation.isPending}>
              {mutation.isPending ? 'Saving…' : subject ? 'Save changes' : 'Create subject'}
            </Button>
          </DialogFooter>
        </form>
      </DialogContent>
    </Dialog>
  );
}
