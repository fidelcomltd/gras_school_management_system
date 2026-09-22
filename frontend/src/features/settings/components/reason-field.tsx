import { useId } from 'react';
import { Input } from '@/components/ui/input';

/** Spec 6.2.9: a change to grading, assessment or rules needs a reason once results are published in the session. */
export function ReasonField({ value, onChange }: { value: string; onChange: (value: string) => void }) {
  const id = useId();
  return (
    <div className="flex max-w-xl flex-col gap-1 text-sm">
      <label htmlFor={id} className="text-foreground">
        Reason for the change (required once results are published this session, at least 10 characters)
      </label>
      <Input id={id} value={value} onChange={(event) => onChange(event.target.value)} />
    </div>
  );
}
