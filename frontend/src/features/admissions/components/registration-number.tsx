import { Button } from '@/components/ui/button';
import { PrintAdmissionSlip } from '@/features/pupils/components/print-admission-slip';

/** Spec 6.5.11: the issued number in large type with a copy button, because the office is asked for it immediately. */
/** With `pupilId`, the slip can be printed at once: spec 6.5.11 puts "Print admission slip" on the confirmation. */
export function RegistrationNumber({ number, label, pupilId }: { number: string; label: string; pupilId?: string }) {
  return (
    <div className="flex flex-col items-start gap-2">
      <output className="flex flex-col gap-1 text-sm text-foreground">
        {label}
        <span className="font-display text-4xl font-semibold tracking-wide">{number}</span>
      </output>
      <div className="flex flex-wrap items-start gap-2">
        <Button type="button" variant="outline" size="sm" onClick={() => void navigator.clipboard?.writeText(number)}>
          Copy number
        </Button>
        {pupilId ? <PrintAdmissionSlip pupilId={pupilId} /> : null}
      </div>
    </div>
  );
}
