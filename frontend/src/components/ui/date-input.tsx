import { CalendarDays } from 'lucide-react';
import { useRef, useState, type Ref, type RefCallback } from 'react';
import { cn } from '@/lib/utils/cn';
import { ISO, isoToSchool, schoolToIso } from './date-text';
import { Input } from './input';

/** What `onChange` receives: shaped like an input event, so react-hook-form's `register` and a `(e) => e.target.value` handler both work. */
export interface DateInputChange {
  target: { name: string; value: string };
  type: 'change';
}

export interface DateInputProps {
  /** Controlled value, ISO `YYYY-MM-DD`; `''` for none. Omit it when the field is `register`ed instead. */
  value?: string;
  onChange?: (event: DateInputChange) => unknown;
  /** Carries the value too: react-hook-form's blur handler is its change handler, and reads `target.value`. */
  onBlur?: (event: { target: { name: string; value: string }; type: 'blur' }) => unknown;
  name?: string;
  /** ISO bounds, applied to the calendar. (`register` types them wider; only strings are meaningful here.) */
  min?: string | number | undefined;
  max?: string | number | undefined;
  id?: string | undefined;
  'aria-label'?: string | undefined;
  disabled?: boolean | undefined;
  /** Spread in by `register`; validation lives in the form's schema, so these are ignored. */
  required?: boolean | undefined;
  minLength?: number | undefined;
  maxLength?: number | undefined;
  pattern?: string | undefined;
  /** Sizes the whole control (a width utility, e.g. `w-44`). */
  className?: string;
  /**
   * The hidden native date input, which always holds the ISO value. react-hook-form's `register` needs a real, connected
   * element (it drops a field whose ref is anything else), and writes `reset()` and defaults through its `value`, which
   * is intercepted so the visible text follows. Its `focus()` moves to the visible text.
   */
  ref?: Ref<HTMLInputElement>;
}

const LIMITS = [2, 2, 4];

/**
 * Typing fills day, month and year in turn, inserting each slash once a part is full; a slash typed early ("1/9/2026")
 * closes the part instead. Deleting is left alone, so a slash can be backspaced over.
 */
function mask(raw: string, previous: string): string {
  if (ISO.test(raw.trim())) return isoToSchool(raw.trim());
  const clean = raw.replace(/[^\d/]/g, '');
  if (clean.length < previous.length) return clean.slice(0, 10);
  const parts = [''];
  for (const character of clean) {
    const at = parts.length - 1;
    if (character === '/') {
      if (parts[at] !== '' && parts.length < 3) parts.push('');
      continue;
    }
    if ((parts[at] ?? '').length >= (LIMITS[at] ?? 4)) {
      if (parts.length === 3) continue;
      parts.push('');
    }
    parts[parts.length - 1] += character;
  }
  return parts.join('/');
}

const valueOf = Object.getOwnPropertyDescriptor(HTMLInputElement.prototype, 'value');
const intercepted = new WeakSet<HTMLInputElement>();

function assignRef<T>(ref: Ref<T> | undefined, value: T | null) {
  if (typeof ref === 'function') ref(value);
  else if (ref) ref.current = value;
}

/**
 * A date field that always reads DD/MM/YYYY, the way the school writes dates. A native `<input type="date">` shows the
 * browser's locale instead (MM/DD/YYYY on most machines here), so the text is ours and the browser's calendar is only
 * borrowed, behind the button. Values in and out stay ISO `YYYY-MM-DD`, so a call site swaps `<Input type="date">` for
 * this and changes nothing else. Incomplete or impossible text reports `''`, and is flagged once the field is left.
 */
export function DateInput(props: DateInputProps) {
  // eslint-disable-next-line @typescript-eslint/no-unused-vars -- `register`'s constraint props, dropped on purpose
  const { value, onChange, onBlur, name = '', min, max, id, disabled, className, ref, required, minLength, maxLength, pattern, ...rest } = props;
  const textRef = useRef<HTMLInputElement>(null);
  const pickerRef = useRef<HTMLInputElement | null>(null);
  const [text, setText] = useState(() => isoToSchool(value));
  const [iso, setIso] = useState(() => value ?? '');
  const [touched, setTouched] = useState(false);

  // A controlled parent changed the value from outside (a reset, a default arriving): show it.
  if (value !== undefined && value !== iso) {
    setIso(value);
    setText(isoToSchool(value));
  }

  const writePicker = (next: string) => {
    if (pickerRef.current) valueOf?.set?.call(pickerRef.current, next);
  };

  const emit = (next: string) => {
    if (next === (pickerRef.current ? valueOf?.get?.call(pickerRef.current) : iso)) return;
    writePicker(next);
    setIso(next);
    onChange?.({ target: { name, value: next }, type: 'change' });
  };

  // A new callback each render, as a native input's `register` ref is: React detaches the old and attaches this one.
  const attachPicker: RefCallback<HTMLInputElement> = (element) => {
    pickerRef.current = element;
    if (element && !intercepted.has(element)) {
      intercepted.add(element);
      Object.defineProperty(element, 'value', {
        configurable: true,
        get: () => valueOf?.get?.call(element) as string,
        set: (next: string | null | undefined) => {
          const clean = next ?? '';
          valueOf?.set?.call(element, clean);
          setIso(clean);
          setText(isoToSchool(clean));
        },
      });
      element.focus = () => textRef.current?.focus();
    }
    assignRef(ref, element);
  };

  const openCalendar = () => {
    const picker = pickerRef.current;
    if (!picker) return;
    try {
      picker.showPicker();
    } catch {
      picker.click();
    }
  };

  const invalid = touched && text.trim() !== '' && schoolToIso(text) === null;

  return (
    <div className={cn('relative w-full', className)}>
      <Input
        {...rest}
        ref={textRef}
        id={id}
        value={text}
        disabled={disabled}
        inputMode="numeric"
        autoComplete="off"
        placeholder="dd/mm/yyyy"
        aria-invalid={invalid || undefined}
        className={cn('pr-10', invalid && 'border-destructive')}
        onChange={(event) => {
          const next = mask(event.target.value, text);
          setText(next);
          emit(schoolToIso(next) ?? '');
        }}
        onBlur={() => {
          setTouched(true);
          const parsed = schoolToIso(text);
          if (parsed) setText(isoToSchool(parsed));
          onBlur?.({ target: { name, value: parsed ?? '' }, type: 'blur' });
        }}
      />
      <button
        type="button"
        aria-label="Choose a date from the calendar"
        disabled={disabled}
        onClick={openCalendar}
        className="absolute top-1/2 right-1 flex size-8 -translate-y-1/2 items-center justify-center rounded-md text-muted-foreground hover:bg-muted hover:text-foreground focus-visible:outline-2 focus-visible:outline-ring disabled:opacity-60"
      >
        <CalendarDays className="size-4" aria-hidden="true" />
      </button>
      {/* The browser's calendar, borrowed: never seen or tabbed to, only opened by the button beside the text. */}
      <input
        ref={attachPicker}
        type="date"
        name={name}
        defaultValue={iso}
        tabIndex={-1}
        aria-hidden="true"
        min={min === undefined ? undefined : String(min)}
        max={max === undefined ? undefined : String(max)}
        className="pointer-events-none absolute bottom-0 left-0 h-px w-full opacity-0"
        onChange={(event) => {
          const next = event.target.value;
          setText(isoToSchool(next));
          setTouched(false);
          setIso(next);
          onChange?.({ target: { name, value: next }, type: 'change' });
        }}
      />
    </div>
  );
}
