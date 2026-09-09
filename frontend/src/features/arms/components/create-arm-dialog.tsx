import { zodResolver } from '@hookform/resolvers/zod';
import { useEffect } from 'react';
import { Controller, useForm, useWatch } from 'react-hook-form';
import { Button } from '@/components/ui/button';
import { Dialog, DialogContent, DialogFooter, DialogHeader, DialogTitle } from '@/components/ui/dialog';
import { Field, FieldError, FieldLabel } from '@/components/ui/field';
import { Input } from '@/components/ui/input';
import { useLevels } from '@/features/classes/api';
import { useSessions } from '@/features/sessions/api';
import { ApiError } from '@/lib/http';
import { hasFieldError } from '@/shared/forms/field-message';
import { useCreateArm, useNextArmLabel } from '../api';
import { createArmSchema, type CreateArmFormValues } from '../arm-schema';
import { ArmLevelSelect, SessionSelect } from './arm-pickers';

const FIELDS = ['sessionId', 'levelId', 'label', 'capacity'] as const;

/**
 * `POST /api/v1/arms` (spec 6.4.3, 6.4.4) — "opening a new room mid-term
 * under an active level", permitted with no privilege beyond `arm.create`.
 * No form-teacher field here: assigning one is a separately-gated action
 * (`arm.formteacher.assign`, `PATCH /arms/{id}`) done from the edit dialog
 * once the arm exists (out-of-scope note in the task card's judgement call).
 */
export function CreateArmDialog({ onClose }: { onClose: () => void }) {
  const createArm = useCreateArm();
  const sessions = useSessions();
  const levels = useLevels();
  // The command requires "an upcoming or active session" — `useSessions` has
  // no combined server-side filter for that, so this excludes `Closed`
  // client-side rather than adding a second round trip.
  const sessionItems = (sessions.data?.pages.flatMap((page) => page.items) ?? []).filter(
    (s) => s.state !== 'Closed',
  );
  const levelItems = levels.data?.pages.flatMap((page) => page.items) ?? [];

  const {
    register,
    control,
    handleSubmit,
    setValue,
    formState: { errors },
  } = useForm<CreateArmFormValues>({
    resolver: zodResolver(createArmSchema),
    defaultValues: { sessionId: '', levelId: '', label: '', capacity: '' },
  });
  const sessionId = useWatch({ control, name: 'sessionId' });
  const levelId = useWatch({ control, name: 'levelId' });
  const nextLabel = useNextArmLabel(levelId, sessionId);

  // Pre-fills the label once both pickers resolve (spec 6.4.3) — editable
  // immediately after, never re-applied once the caller has typed their own.
  useEffect(() => {
    if (nextLabel.data) setValue('label', nextLabel.data.label);
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [nextLabel.data]);

  const onSubmit = handleSubmit((values) => {
    createArm.mutate(
      {
        classLevelId: values.levelId,
        sessionId: values.sessionId,
        label: values.label,
        capacity: values.capacity === '' ? null : Number(values.capacity),
        formTeacherAdminId: null,
      },
      { onSuccess: onClose },
    );
  });

  const error = createArm.error;
  const formError =
    error instanceof ApiError && (error.kind !== 'validation' || !hasFieldError(error.fieldErrors, FIELDS))
      ? error.message
      : null;

  return (
    <Dialog open onOpenChange={(open) => !open && onClose()}>
      <DialogContent>
        <DialogHeader>
          <DialogTitle>New arm</DialogTitle>
        </DialogHeader>

        <form onSubmit={onSubmit} className="flex flex-col gap-4" noValidate>
          {formError ? (
            <p role="alert" className="rounded-md bg-destructive/10 px-3 py-2 text-sm text-destructive">
              {formError}
            </p>
          ) : null}

          <Field invalid={!!errors.sessionId}>
            <FieldLabel>Session</FieldLabel>
            <Controller
              control={control}
              name="sessionId"
              render={({ field }) => (
                <SessionSelect value={field.value} onChange={field.onChange} sessions={sessionItems} />
              )}
            />
            <FieldError match={true}>{errors.sessionId?.message}</FieldError>
          </Field>

          <Field invalid={!!errors.levelId}>
            <FieldLabel>Level</FieldLabel>
            <Controller
              control={control}
              name="levelId"
              render={({ field }) => (
                <ArmLevelSelect value={field.value} onChange={field.onChange} levels={levelItems} />
              )}
            />
            <FieldError match={true}>{errors.levelId?.message}</FieldError>
          </Field>

          <Field invalid={!!errors.label}>
            <FieldLabel>Label</FieldLabel>
            <Input placeholder="C" {...register('label')} />
            <FieldError match={true}>{errors.label?.message}</FieldError>
          </Field>

          <Field invalid={!!errors.capacity}>
            <FieldLabel>Capacity</FieldLabel>
            <Input type="number" min={1} max={100} placeholder="Default" {...register('capacity')} />
            <FieldError match={true}>{errors.capacity?.message}</FieldError>
          </Field>

          <DialogFooter>
            <Button type="button" variant="ghost" onClick={onClose}>
              Cancel
            </Button>
            <Button type="submit" disabled={createArm.isPending}>
              {createArm.isPending ? 'Creating…' : 'Create arm'}
            </Button>
          </DialogFooter>
        </form>
      </DialogContent>
    </Dialog>
  );
}
