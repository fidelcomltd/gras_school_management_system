import { zodResolver } from '@hookform/resolvers/zod';
import { useEffect, useState } from 'react';
import { Controller, useFieldArray, useForm } from 'react-hook-form';
import { Button } from '@/components/ui/button';
import { Dialog, DialogContent, DialogFooter, DialogHeader, DialogTitle } from '@/components/ui/dialog';
import { Field, FieldLabel } from '@/components/ui/field';
import { Input } from '@/components/ui/input';
import { useLevels } from '@/features/classes/api';
import { useSessions } from '@/features/sessions/api';
import { ApiError } from '@/lib/http';
import { useBulkCreateArms } from '../api';
import { bulkCreateArmsSchema, type BulkCreateArmsFormValues } from '../arm-schema';
import type { ArmDto, BulkCreateArmsCommand } from '../types';
import { SessionSelect } from './arm-pickers';

function buildPayload(values: BulkCreateArmsFormValues, dryRun: boolean): BulkCreateArmsCommand {
  return {
    sessionId: values.sessionId,
    levels: values.rows
      .filter((r) => r.armCount !== '' && Number(r.armCount) > 0)
      .map((r) => ({
        levelId: r.levelId,
        armCount: Number(r.armCount),
        capacity: r.capacity === '' ? null : Number(r.capacity),
      })),
    dryRun,
  };
}

/**
 * `POST /api/v1/arms/bulk` (spec 6.4.3) — "an administrator opening a new
 * session creates every level's rooms in one action instead of one form per
 * level". ALWAYS dry-runs first (TASK-0045 AC): the 'form' step's only
 * submit button is "Preview"; "Create arms" only exists once a preview
 * response is already in state (the 'preview' step), and any edit to an
 * input after that clears `preview` and returns to the form — a stale
 * preview can never be the thing that gets committed.
 */
export function BulkCreateArmsDialog({ onClose }: { onClose: () => void }) {
  const bulkCreate = useBulkCreateArms();
  const sessions = useSessions();
  const levels = useLevels();
  const sessionItems = (sessions.data?.pages.flatMap((p) => p.items) ?? []).filter((s) => s.state !== 'Closed');
  const levelItems = levels.data?.pages.flatMap((p) => p.items) ?? [];

  const [preview, setPreview] = useState<ArmDto[] | null>(null);

  const { control, register, handleSubmit, watch } = useForm<BulkCreateArmsFormValues>({
    resolver: zodResolver(bulkCreateArmsSchema),
    defaultValues: { sessionId: '', rows: [] },
  });
  const { fields, replace } = useFieldArray({ control, name: 'rows' });

  // One row per active level, seeded once the levels load.
  useEffect(() => {
    if (levelItems.length > 0 && fields.length === 0) {
      replace(levelItems.map((l) => ({ levelId: l.id, levelName: l.name, armCount: '', capacity: '' })));
    }
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [levelItems.length]);

  // Any change after a preview invalidates it (see the file's own doc comment).
  useEffect(() => {
    const subscription = watch(() => setPreview(null));
    return () => subscription.unsubscribe();
  }, [watch]);

  const onPreview = handleSubmit((values) => {
    bulkCreate.mutate(buildPayload(values, true), { onSuccess: (result) => setPreview(result.created) });
  });

  const onCommit = handleSubmit((values) => {
    bulkCreate.mutate(buildPayload(values, false), { onSuccess: onClose });
  });

  const formError = bulkCreate.error instanceof ApiError ? bulkCreate.error.message : null;

  return (
    <Dialog open onOpenChange={(open) => !open && onClose()}>
      <DialogContent>
        <DialogHeader>
          <DialogTitle>Create arms for session</DialogTitle>
        </DialogHeader>

        <div className="flex max-h-[70vh] flex-col gap-4 overflow-y-auto">
          {formError ? (
            <p role="alert" className="rounded-md bg-destructive/10 px-3 py-2 text-sm text-destructive">
              {formError}
            </p>
          ) : null}

          <Field>
            <FieldLabel>Session</FieldLabel>
            <Controller
              control={control}
              name="sessionId"
              render={({ field }) => (
                <SessionSelect value={field.value} onChange={field.onChange} sessions={sessionItems} />
              )}
            />
          </Field>

          {preview ? (
            <ul aria-label="Bulk create preview" className="flex flex-col gap-2">
              {preview.map((arm) => (
                <li
                  key={arm.id}
                  className="flex items-center justify-between rounded-md border border-border bg-surface px-3 py-2 text-sm"
                >
                  <span className="font-medium text-foreground">{arm.displayName}</span>
                  <span className="text-muted-foreground">Capacity {arm.capacity}</span>
                </li>
              ))}
            </ul>
          ) : (
            <table className="w-full text-sm">
              <thead>
                <tr className="text-left text-xs text-muted-foreground">
                  <th className="pb-2">Level</th>
                  <th className="pb-2">Arms to create</th>
                  <th className="pb-2">Capacity each</th>
                </tr>
              </thead>
              <tbody>
                {fields.map((row, index) => (
                  <tr key={row.id}>
                    <td className="py-1 pr-2 text-foreground">{row.levelName}</td>
                    <td className="py-1 pr-2">
                      <Input
                        type="number"
                        min={0}
                        max={26}
                        aria-label={`Arms to create for ${row.levelName}`}
                        {...register(`rows.${index}.armCount`)}
                      />
                    </td>
                    <td className="py-1">
                      <Input
                        type="number"
                        min={1}
                        max={100}
                        placeholder="Default"
                        aria-label={`Capacity each for ${row.levelName}`}
                        {...register(`rows.${index}.capacity`)}
                      />
                    </td>
                  </tr>
                ))}
              </tbody>
            </table>
          )}

          <DialogFooter>
            {preview ? (
              <>
                <Button type="button" variant="ghost" onClick={() => setPreview(null)}>
                  Back
                </Button>
                <Button type="button" onClick={() => void onCommit()} disabled={bulkCreate.isPending}>
                  {bulkCreate.isPending ? 'Creating…' : `Create ${preview.length} arms`}
                </Button>
              </>
            ) : (
              <>
                <Button type="button" variant="ghost" onClick={onClose}>
                  Cancel
                </Button>
                <Button type="button" onClick={() => void onPreview()} disabled={bulkCreate.isPending}>
                  {bulkCreate.isPending ? 'Previewing…' : 'Preview'}
                </Button>
              </>
            )}
          </DialogFooter>
        </div>
      </DialogContent>
    </Dialog>
  );
}
