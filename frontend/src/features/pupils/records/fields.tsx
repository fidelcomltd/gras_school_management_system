import { useId, type HTMLAttributes } from 'react';

/** A labelled text input for the record panels. */
export function TextField({
  label,
  value,
  onChange,
  placeholder,
  inputMode,
  multiline = false,
}: {
  label: string;
  value: string;
  onChange: (value: string) => void;
  placeholder?: string;
  inputMode?: HTMLAttributes<HTMLInputElement>['inputMode'];
  multiline?: boolean;
}) {
  const id = useId();
  const className = 'rounded-md border border-input bg-background px-2 text-sm text-foreground disabled:opacity-60';
  return (
    <label htmlFor={id} className="flex flex-col gap-1 text-xs text-muted-foreground">
      {label}
      {multiline ? (
        <textarea id={id} rows={2} className={`${className} py-1.5`} value={value} placeholder={placeholder} onChange={(event) => onChange(event.target.value)} />
      ) : (
        <input id={id} className={`${className} h-9`} value={value} placeholder={placeholder} inputMode={inputMode} onChange={(event) => onChange(event.target.value)} />
      )}
    </label>
  );
}

/**
 * An explicit Yes / No question (spec 6.5.7): no default, because an unanswered question is not the same as No. `null`
 * renders with neither option chosen.
 */
export function YesNo({ legend, value, onChange, disabled }: { legend: string; value: boolean | null; onChange: (value: boolean) => void; disabled?: boolean }) {
  const name = useId();
  return (
    <fieldset className="flex flex-wrap items-center gap-4 text-sm text-foreground" disabled={disabled}>
      <legend className="mb-1 w-full font-medium">{legend}</legend>
      <label className="flex items-center gap-1.5">
        <input type="radio" name={name} checked={value === true} onChange={() => onChange(true)} /> Yes
      </label>
      <label className="flex items-center gap-1.5">
        <input type="radio" name={name} checked={value === false} onChange={() => onChange(false)} /> No
      </label>
      {value === null ? <span className="text-xs text-destructive">Not answered yet</span> : null}
    </fieldset>
  );
}
