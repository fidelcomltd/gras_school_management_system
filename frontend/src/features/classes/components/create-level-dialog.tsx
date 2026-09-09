import { zodResolver } from '@hookform/resolvers/zod';
import { Controller, useForm, useWatch } from 'react-hook-form';
import { Button } from '@/components/ui/button';
import { Dialog, DialogContent, DialogFooter, DialogHeader, DialogTitle } from '@/components/ui/dialog';
import { Field, FieldError, FieldLabel } from '@/components/ui/field';
import { Input } from '@/components/ui/input';
import { ApiError } from '@/lib/http';
import { hasFieldError } from '@/shared/forms/field-message';
import { useCreateLevel, useSections } from '../api';
import { createLevelSchema, type CreateLevelFormValues } from '../level-schema';
import type { LevelDto } from '../types';
import { LevelSelect, SectionSelect } from './level-pickers';

const FIELDS = ['name', 'sectionId', 'insertAfterLevelId', 'progressionOrder', 'nextLevelId'] as const;

/**
 * `POST /api/v1/levels` (spec 6.4.2, 6.4.9). The worked case — give
 * `insertAfterLevelId` and the server rewires the chain — is the default
 * placement here; "manual order" is the alternative the contract also
 * allows, for "the administrator who prefers typing".
 */
export function CreateLevelDialog({ levels, onClose }: { levels: LevelDto[]; onClose: () => void }) {
  const createLevel = useCreateLevel();
  const sections = useSections();
  const {
    register,
    control,
    handleSubmit,
    formState: { errors },
  } = useForm<CreateLevelFormValues>({
    resolver: zodResolver(createLevelSchema),
    defaultValues: {
      name: '',
      sectionId: '',
      placement: 'insertAfter',
      insertAfterLevelId: '',
      progressionOrder: '',
      nextLevelId: '',
    },
  });
  const placement = useWatch({ control, name: 'placement' });

  const onSubmit = handleSubmit((values) => {
    createLevel.mutate(
      {
        name: values.name,
        sectionId: values.sectionId,
        insertAfterLevelId: values.placement === 'insertAfter' ? values.insertAfterLevelId : null,
        progressionOrder: values.placement === 'manualOrder' ? Number(values.progressionOrder) : null,
        nextLevelId: values.placement === 'manualOrder' && values.nextLevelId !== '' ? values.nextLevelId : null,
      },
      { onSuccess: onClose },
    );
  });

  const error = createLevel.error;
  const formError =
    error instanceof ApiError && (error.kind !== 'validation' || !hasFieldError(error.fieldErrors, FIELDS))
      ? error.message
      : null;

  return (
    <Dialog open onOpenChange={(open) => !open && onClose()}>
      <DialogContent>
        <DialogHeader>
          <DialogTitle>New level</DialogTitle>
        </DialogHeader>

        <form onSubmit={onSubmit} className="flex flex-col gap-4" noValidate>
          {formError ? (
            <p role="alert" className="rounded-md bg-destructive/10 px-3 py-2 text-sm text-destructive">
              {formError}
            </p>
          ) : null}

          <Field invalid={!!errors.name}>
            <FieldLabel>Level name</FieldLabel>
            <Input placeholder="Primary 1" {...register('name')} />
            <FieldError match={true}>{errors.name?.message}</FieldError>
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
            <FieldError match={true}>{errors.sectionId?.message}</FieldError>
          </Field>

          <fieldset className="flex flex-col gap-2 rounded-md border border-border p-3">
            <legend className="px-1 text-sm font-medium text-foreground">Where in the chain?</legend>
            <label className="flex items-center gap-2 text-sm">
              <input type="radio" value="insertAfter" {...register('placement')} />
              Insert after an existing level
            </label>
            {placement === 'insertAfter' ? (
              <Field invalid={!!errors.insertAfterLevelId}>
                <FieldLabel>Insert after</FieldLabel>
                <Controller
                  control={control}
                  name="insertAfterLevelId"
                  render={({ field }) => (
                    <LevelSelect value={field.value} onChange={field.onChange} levels={levels} label="Insert after" />
                  )}
                />
                <FieldError match={true}>{errors.insertAfterLevelId?.message}</FieldError>
              </Field>
            ) : null}

            <label className="flex items-center gap-2 text-sm">
              <input type="radio" value="manualOrder" {...register('placement')} />
              Set the order myself
            </label>
            {placement === 'manualOrder' ? (
              <>
                <Field invalid={!!errors.progressionOrder}>
                  <FieldLabel>Order</FieldLabel>
                  <Input type="number" min={1} {...register('progressionOrder')} />
                  <FieldError match={true}>{errors.progressionOrder?.message}</FieldError>
                </Field>
                <Field>
                  <FieldLabel>Next level</FieldLabel>
                  <Controller
                    control={control}
                    name="nextLevelId"
                    render={({ field }) => (
                      <LevelSelect
                        value={field.value}
                        onChange={field.onChange}
                        levels={levels}
                        label="Next level"
                        noneLabel="— none (graduating) —"
                      />
                    )}
                  />
                </Field>
              </>
            ) : null}
          </fieldset>

          <DialogFooter>
            <Button type="button" variant="ghost" onClick={onClose}>
              Cancel
            </Button>
            <Button type="submit" disabled={createLevel.isPending}>
              {createLevel.isPending ? 'Creating…' : 'Create level'}
            </Button>
          </DialogFooter>
        </form>
      </DialogContent>
    </Dialog>
  );
}
