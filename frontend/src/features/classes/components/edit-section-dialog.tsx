import { zodResolver } from '@hookform/resolvers/zod';
import { useForm } from 'react-hook-form';
import { Button } from '@/components/ui/button';
import { Dialog, DialogContent, DialogFooter, DialogHeader, DialogTitle } from '@/components/ui/dialog';
import { Field, FieldError, FieldLabel } from '@/components/ui/field';
import { Input } from '@/components/ui/input';
import { ApiError } from '@/lib/http';
import { useUpdateSection } from '../api';
import { sectionNameSchema, type SectionNameFormValues } from '../section-schema';
import type { SectionDto } from '../types';

/** `PATCH /api/v1/sections/{id}` (spec 6.4.9). Name only. */
export function EditSectionDialog({ section, onClose }: { section: SectionDto; onClose: () => void }) {
  const updateSection = useUpdateSection();
  const {
    register,
    handleSubmit,
    formState: { errors },
  } = useForm<SectionNameFormValues>({
    resolver: zodResolver(sectionNameSchema),
    defaultValues: { name: section.name },
  });

  const onSubmit = handleSubmit((values) => {
    updateSection.mutate({ id: section.id, name: values.name }, { onSuccess: onClose });
  });

  const formError = updateSection.error instanceof ApiError ? updateSection.error.message : null;

  return (
    <Dialog open onOpenChange={(open) => !open && onClose()}>
      <DialogContent>
        <DialogHeader>
          <DialogTitle>Rename {section.name}</DialogTitle>
        </DialogHeader>

        <form onSubmit={onSubmit} className="flex flex-col gap-4" noValidate>
          {formError ? (
            <p role="alert" className="rounded-md bg-destructive/10 px-3 py-2 text-sm text-destructive">
              {formError}
            </p>
          ) : null}

          <Field invalid={!!errors.name}>
            <FieldLabel>Section name</FieldLabel>
            <Input {...register('name')} />
            <FieldError match={true}>{errors.name?.message}</FieldError>
          </Field>

          <DialogFooter>
            <Button type="button" variant="ghost" onClick={onClose}>
              Cancel
            </Button>
            <Button type="submit" disabled={updateSection.isPending}>
              {updateSection.isPending ? 'Saving…' : 'Save changes'}
            </Button>
          </DialogFooter>
        </form>
      </DialogContent>
    </Dialog>
  );
}
