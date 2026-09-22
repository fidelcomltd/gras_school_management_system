import { zodResolver } from '@hookform/resolvers/zod';
import { useForm, useWatch } from 'react-hook-form';
import { z } from 'zod';
import { FormError } from '@/components/feedback/query-states';
import { Button } from '@/components/ui/button';
import { Dialog, DialogContent, DialogDescription, DialogFooter, DialogHeader, DialogTitle } from '@/components/ui/dialog';
import { Field, FieldError, FieldLabel } from '@/components/ui/field';
import { Input } from '@/components/ui/input';
import { ApiError } from '@/lib/http';
import { useGeneratePinBatch } from '../api';
import type { PinBatchDto } from '../types';

const whole = (min: number, max: number, label: string) =>
  z.coerce.number({ message: `${label} must be a number.` }).int(`${label} must be a whole number.`).min(min, `${label}: at least ${min}.`).max(max, `${label}: at most ${max}.`);

const schema = z
  .object({
    pinCount: whole(1, 2000, 'Number of pins'),
    maxUses: whole(1, 100, 'Uses per pin'),
    confirmMaxUses: z.string(),
    pinLength: whole(10, 16, 'Pin length'),
    name: z.string().trim().max(80, 'At most 80 characters.'),
    purposeNote: z.string().trim().max(200, 'At most 200 characters.'),
  })
  .refine((values) => values.maxUses <= 10 || Number(values.confirmMaxUses) === values.maxUses, {
    path: ['confirmMaxUses'],
    message: 'Type the number of uses again to confirm.',
  });

type Values = z.input<typeof schema>;
type Parsed = z.output<typeof schema>;

/** `POST /api/v1/pin-batches` (spec 6.8.9). Above 10 uses the number must be typed twice (6.8.12). */
export function GenerateBatchDialog({ sessionId, onClose, onCreated }: { sessionId: string; onClose: () => void; onCreated: (batch: PinBatchDto) => void }) {
  const generate = useGeneratePinBatch();
  const {
    register,
    control,
    handleSubmit,
    formState: { errors },
  } = useForm<Values, unknown, Parsed>({
    resolver: zodResolver(schema),
    defaultValues: { pinCount: 100, maxUses: 3, confirmMaxUses: '', pinLength: 10, name: '', purposeNote: '' },
  });
  const maxUses = Number(useWatch({ control, name: 'maxUses' }));

  const onSubmit = handleSubmit((values) =>
    generate.mutate(
      {
        sessionId,
        name: values.name || null,
        purposeNote: values.purposeNote || null,
        pinCount: values.pinCount,
        pinLength: values.pinLength,
        maxUses: values.maxUses,
        confirmMaxUses: values.maxUses > 10 ? values.maxUses : null,
      },
      { onSuccess: onCreated },
    ),
  );

  const field = (name: keyof Values, label: string, type = 'text') => (
    <Field invalid={!!errors[name]}>
      <FieldLabel>{label}</FieldLabel>
      <Input type={type} {...register(name)} />
      <FieldError match={true}>{errors[name]?.message}</FieldError>
    </Field>
  );

  return (
    <Dialog open onOpenChange={(open) => !open && onClose()}>
      <DialogContent>
        <DialogHeader>
          <DialogTitle>Generate pins</DialogTitle>
          <DialogDescription>Any pin opens any pupil's published results in this session, so count them carefully.</DialogDescription>
        </DialogHeader>
        <form onSubmit={onSubmit} className="flex flex-col gap-4" noValidate>
          <FormError message={generate.error instanceof ApiError ? generate.error.message : null} />
          <div className="grid gap-3 sm:grid-cols-3">
            {field('pinCount', 'Number of pins', 'number')}
            {field('maxUses', 'Uses per pin', 'number')}
            {field('pinLength', 'Pin length', 'number')}
          </div>
          {maxUses > 10 ? field('confirmMaxUses', `Type ${maxUses} again to confirm`, 'number') : null}
          {field('name', 'Batch name (optional)')}
          {field('purposeNote', 'Note, e.g. which classes (optional)')}
          {generate.isPending ? <output className="text-sm text-muted-foreground">Generating… large batches take up to a minute.</output> : null}
          <DialogFooter>
            <Button type="button" variant="ghost" onClick={onClose}>
              Cancel
            </Button>
            <Button type="submit" disabled={generate.isPending}>
              {generate.isPending ? 'Generating…' : 'Generate'}
            </Button>
          </DialogFooter>
        </form>
      </DialogContent>
    </Dialog>
  );
}
