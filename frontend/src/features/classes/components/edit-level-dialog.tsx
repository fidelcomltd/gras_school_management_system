import { zodResolver } from '@hookform/resolvers/zod';
import { Controller, useForm } from 'react-hook-form';
import { Button } from '@/components/ui/button';
import { Dialog, DialogContent, DialogFooter, DialogHeader, DialogTitle } from '@/components/ui/dialog';
import { Field, FieldError, FieldLabel } from '@/components/ui/field';
import { Input } from '@/components/ui/input';
import { ApiError } from '@/lib/http';
import { hasFieldError } from '@/shared/forms/field-message';
import { useLevel, useSections, useUpdateLevel } from '../api';
import { editLevelSchema, type EditLevelFormValues } from '../level-schema';
import type { LevelDto } from '../types';
import { LevelSelect, SectionSelect } from './level-pickers';

const FIELDS = ['name', 'sectionId', 'nextLevelId', 'progressionOrder', 'status'] as const;

/**
 * `PATCH /api/v1/levels/{id}` (spec 6.4.2, 6.4.9). Reruns the eight chain
 * rules — their rejection surfaces verbatim below. Changing `status`
 * additionally needs `level.deactivate`; that field is simply absent from
 * this form for a caller who only holds `level.update` (never disabled —
 * TASK-0041's own nav ruling, applied here to a field instead of a link).
 */
export function EditLevelDialog({
  levels,
  level,
  canDeactivate,
  onClose,
}: {
  levels: LevelDto[];
  level: LevelDto;
  canDeactivate: boolean;
  onClose: () => void;
}) {
  const updateLevel = useUpdateLevel();
  const sections = useSections();
  // `GET /levels/{id}` refetches this one level on open, so an edit starts
  // from current data rather than whatever the list happened to cache —
  // falls back to the list's own row while that request is in flight.
  const detail = useLevel(level.id);
  const source = detail.data ?? level;
  const {
    register,
    control,
    handleSubmit,
    formState: { errors },
  } = useForm<EditLevelFormValues>({
    resolver: zodResolver(editLevelSchema),
    values: {
      name: source.name,
      sectionId: source.sectionId,
      nextLevelId: source.nextLevelId ?? '',
      progressionOrder: String(source.progressionOrder),
      status: '',
    },
  });

  const onSubmit = handleSubmit((values) => {
    updateLevel.mutate(
      {
        id: level.id,
        name: values.name || null,
        sectionId: values.sectionId || null,
        nextLevelId: values.nextLevelId,
        progressionOrder: values.progressionOrder === '' ? null : Number(values.progressionOrder),
        status: values.status === '' ? null : values.status,
      },
      { onSuccess: onClose },
    );
  });

  const error = updateLevel.error;
  const formError =
    error instanceof ApiError && (error.kind !== 'validation' || !hasFieldError(error.fieldErrors, FIELDS))
      ? error.message
      : null;

  const otherLevels = levels.filter((l) => l.id !== level.id);

  return (
    <Dialog open onOpenChange={(open) => !open && onClose()}>
      <DialogContent>
        <DialogHeader>
          <DialogTitle>Edit {level.name}</DialogTitle>
        </DialogHeader>

        <form onSubmit={onSubmit} className="flex max-h-[70vh] flex-col gap-4 overflow-y-auto" noValidate>
          {formError ? (
            <p role="alert" className="rounded-md bg-destructive/10 px-3 py-2 text-sm text-destructive">
              {formError}
            </p>
          ) : null}

          <Field invalid={!!errors.name}>
            <FieldLabel>Level name</FieldLabel>
            <Input {...register('name')} />
          </Field>

          <Field invalid={!!errors.sectionId}>
            <FieldLabel>Section</FieldLabel>
            <Controller
              control={control}
              name="sectionId"
              render={({ field }) => (
                <SectionSelect value={field.value} onChange={field.onChange} sections={sections.data?.sections ?? []} />
              )}
            />
          </Field>

          <Field invalid={!!errors.nextLevelId}>
            <FieldLabel>Next level</FieldLabel>
            <Controller
              control={control}
              name="nextLevelId"
              render={({ field }) => (
                <LevelSelect
                  value={field.value}
                  onChange={field.onChange}
                  levels={otherLevels}
                  label="Next level"
                  noneLabel="— none (graduating) —"
                />
              )}
            />
          </Field>

          <Field invalid={!!errors.progressionOrder}>
            <FieldLabel>Order</FieldLabel>
            <Input type="number" min={1} {...register('progressionOrder')} />
            <FieldError match={true}>{errors.progressionOrder?.message}</FieldError>
          </Field>

          {canDeactivate ? (
            <div className="flex flex-col gap-1.5">
              <label htmlFor="level-status" className="text-sm font-medium text-foreground">
                Status
              </label>
              <select
                id="level-status"
                className="h-10 w-full rounded-md border border-input bg-surface px-3 text-sm text-foreground"
                {...register('status')}
              >
                <option value="">Leave unchanged</option>
                <option value="Active">Active</option>
                <option value="Inactive">Inactive</option>
              </select>
            </div>
          ) : null}

          <DialogFooter>
            <Button type="button" variant="ghost" onClick={onClose}>
              Cancel
            </Button>
            <Button type="submit" disabled={updateLevel.isPending}>
              {updateLevel.isPending ? 'Saving…' : 'Save changes'}
            </Button>
          </DialogFooter>
        </form>
      </DialogContent>
    </Dialog>
  );
}
