import { Combobox } from '@base-ui/react/combobox';
import { Check, ChevronDown } from 'lucide-react';
import { cn } from '@/lib/utils/cn';

/**
 * A single-choice dropdown for long lists (states, LGAs): type to narrow, arrows to move, Enter to choose. Only a value
 * from `options` can be chosen, never free text. Base UI's Combobox owns the ARIA combobox semantics and the keyboard.
 */
export function SearchableSelect({
  id,
  label,
  options,
  value,
  onChange,
  placeholder = 'Type to search…',
  disabled,
  invalid,
  emptyText = 'No matches.',
}: {
  id?: string;
  /** The accessible name of the input. */
  label: string;
  options: readonly string[];
  value: string;
  onChange: (value: string) => void;
  placeholder?: string;
  disabled?: boolean;
  invalid?: boolean;
  emptyText?: string;
}) {
  return (
    <Combobox.Root
      items={options}
      value={value || null}
      onValueChange={(next: string | null) => onChange(next ?? '')}
      disabled={disabled}
      openOnInputClick
    >
      <div className="relative">
        <Combobox.Input
          id={id}
          aria-label={label}
          aria-invalid={invalid || undefined}
          placeholder={placeholder}
          className={cn(
            'h-10 w-full rounded-md border border-input bg-background pr-9 pl-3 text-sm text-foreground placeholder:text-muted-foreground',
            'focus-visible:outline-2 focus-visible:outline-ring disabled:opacity-50',
            invalid && 'border-destructive',
          )}
        />
        <Combobox.Trigger
          aria-label={`Show ${label.toLowerCase()} options`}
          className="absolute top-1/2 right-1 flex size-8 -translate-y-1/2 items-center justify-center rounded-md text-muted-foreground hover:bg-muted"
        >
          <ChevronDown className="size-4" aria-hidden="true" />
        </Combobox.Trigger>
      </div>
      <Combobox.Portal>
        <Combobox.Positioner sideOffset={4} className="z-50 w-[var(--anchor-width)]">
          <Combobox.Popup className="max-h-72 overflow-y-auto rounded-lg border border-border bg-surface p-1 shadow-lg">
            <Combobox.Empty className="px-2 py-2 text-sm text-muted-foreground empty:hidden">{emptyText}</Combobox.Empty>
            <Combobox.List>
              {(option: string) => (
                <Combobox.Item
                  key={option}
                  value={option}
                  className="flex cursor-pointer items-center gap-2 rounded-md px-2 py-1.5 text-sm text-foreground outline-none data-[highlighted]:bg-muted"
                >
                  <Combobox.ItemIndicator className="flex w-4 justify-center">
                    <Check className="size-4 text-primary" aria-hidden="true" />
                  </Combobox.ItemIndicator>
                  <span className="flex-1">{option}</span>
                </Combobox.Item>
              )}
            </Combobox.List>
          </Combobox.Popup>
        </Combobox.Positioner>
      </Combobox.Portal>
    </Combobox.Root>
  );
}
