import { zodResolver } from '@hookform/resolvers/zod';
import { useForm } from 'react-hook-form';
import { Button } from '@/components/ui/button';
import { Dialog, DialogContent, DialogFooter, DialogHeader, DialogTitle } from '@/components/ui/dialog';
import { Field, FieldError, FieldLabel } from '@/components/ui/field';
import { Input } from '@/components/ui/input';
import { ApiError } from '@/lib/http';
import { useCreateSection } from '../api';
import { sectionNameSchema, type SectionNameFormValues } from '../section-schema';

/** `POST /api/v1/sections` (spec 6.4.9). `Idempotency-Key` is REQUIRED. */
export function CreateSectionDialog({ onClose }: { onClose: () => void }) {
  const createSection = useCreateSection();
  const {
    register,
    handleSubmit,
    formState: { errors },
  } = useForm<SectionNameFormValues>({ resolver: zodResolver(sectionNameSchema), defaultValues: { name: '' } });

  const onSubmit = handleSubmit((values) => {
    createSection.mutate(values, { onSuccess: onClose });
  });

  const formError = createSection.error instanceof ApiError ? createSection.error.message : null;

  return (
    <Dialog open onOpenChange={(open) => !open && onClose()}>
      <DialogContent>
        <DialogHeader>
          <DialogTitle>New section</DialogTitle>
        </DialogHeader>

        <form onSubmit={onSubmit} className="flex flex-col gap-4" noValidate>
          {formError ? (
            <p role="alert" className="rounded-md bg-destructive/10 px-3 py-2 text-sm text-destructive">
              {formError}
            </p>
          ) : null}

          <Field invalid={!!errors.name}>
            <FieldLabel>Section name</FieldLabel>
            <Input placeholder="Secondary" {...register('name')} />
            <FieldError match={true}>{errors.name?.message}</FieldError>
          </Field>

          <DialogFooter>
            <Button type="button" variant="ghost" onClick={onClose}>
              Cancel
            </Button>
            <Button type="submit" disabled={createSection.isPending}>
              {createSection.isPending ? 'Creating…' : 'Create section'}
            </Button>
          </DialogFooter>
        </form>
      </DialogContent>
    </Dialog>
  );
}
