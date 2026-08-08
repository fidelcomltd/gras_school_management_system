import { useState } from 'react';
import { Button } from '@/components/ui/button';
import {
  Dialog,
  DialogClose,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
  DialogTrigger,
} from '@/components/ui/dialog';
import { Field, FieldDescription, FieldLabel } from '@/components/ui/field';
import { Input } from '@/components/ui/input';
import { Select, SelectContent, SelectItem, SelectTrigger } from '@/components/ui/select';

const BUTTON_VARIANTS = ['primary', 'secondary', 'outline', 'ghost', 'accent', 'destructive'] as const;

const TERMS = [
  { value: 'michaelmas', label: 'Michaelmas' },
  { value: 'hilary', label: 'Hilary' },
  { value: 'trinity', label: 'Trinity' },
] as const;

/**
 * Live proof the primitives render and behave. This is scaffold demonstration,
 * not a feature — delete it once real screens exist.
 */
export function PrimitiveGallery() {
  const [term, setTerm] = useState<string | null>(null);

  return (
    <section aria-labelledby="primitives-heading" className="flex flex-col gap-6">
      <div>
        <h2 id="primitives-heading" className="font-display text-xl font-semibold text-foreground">
          Primitives
        </h2>
        <p className="mt-1 text-sm text-muted-foreground">
          Base UI parts, styled locally. No Radix anywhere in the tree.
        </p>
      </div>

      <div className="flex flex-wrap gap-2">
        {BUTTON_VARIANTS.map((variant) => (
          <Button key={variant} variant={variant}>
            {variant}
          </Button>
        ))}
        <Button disabled>disabled</Button>
      </div>

      <div className="grid gap-4 sm:grid-cols-2">
        <Field>
          <FieldLabel>Admission number</FieldLabel>
          <Input name="admissionNumber" placeholder="GRA/2026/0001" />
          <FieldDescription>Labelled via Field — no manual aria wiring.</FieldDescription>
        </Field>

        <Field>
          <FieldLabel>Term</FieldLabel>
          <Select items={TERMS} value={term} onValueChange={setTerm}>
            <SelectTrigger placeholder="Choose a term" />
            <SelectContent>
              {TERMS.map((option) => (
                <SelectItem key={option.value} value={option.value}>
                  {option.label}
                </SelectItem>
              ))}
            </SelectContent>
          </Select>
          <FieldDescription>Keyboard navigable, with typeahead.</FieldDescription>
        </Field>
      </div>

      <div>
        <Dialog>
          <DialogTrigger render={<Button variant="outline">Open dialog</Button>} />
          <DialogContent>
            <DialogHeader>
              <DialogTitle>Dialog primitive</DialogTitle>
              <DialogDescription>
                Focus is trapped, the background is inert, and Escape dismisses. All of it comes
                from Base UI.
              </DialogDescription>
            </DialogHeader>
            <DialogFooter>
              <DialogClose render={<Button variant="ghost">Cancel</Button>} />
              <Button>Confirm</Button>
            </DialogFooter>
          </DialogContent>
        </Dialog>
      </div>
    </section>
  );
}
