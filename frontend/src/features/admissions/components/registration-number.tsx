import { Button } from '@/components/ui/button';

/** Spec 6.5.11: the issued number in large type with a copy button, because the office is asked for it immediately. */
export function RegistrationNumber({ number, label }: { number: string; label: string }) {
  return (
    <div className="flex flex-col items-start gap-2">
      <output className="flex flex-col gap-1 text-sm text-foreground">
        {label}
        <span className="font-display text-4xl font-semibold tracking-wide">{number}</span>
      </output>
      <Button type="button" variant="outline" size="sm" onClick={() => void navigator.clipboard?.writeText(number)}>
        Copy number
      </Button>
    </div>
  );
}
