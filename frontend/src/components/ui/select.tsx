import { Select as BaseSelect } from '@base-ui/react/select';
import type { ComponentPropsWithRef, ReactNode } from 'react';
import { cn } from '@/lib/utils/cn';
import { CheckIcon, ChevronDownIcon } from './icons';
import type { WithClassName } from './props';

/**
 * Listbox select over Base UI — keyboard navigation, typeahead, and the
 * `aria-activedescendant` wiring come from the primitive.
 *
 * ALWAYS pass `items`. The trigger has no way to map a value back to its label
 * on its own, so without `items` it displays the raw value — a user picks
 * "Michaelmas" and the closed trigger reads "michaelmas". Drive both `items`
 * and the rendered options from the same array so they cannot drift:
 *
 *   const TERMS = [
 *     { value: 'michaelmas', label: 'Michaelmas' },
 *     { value: 'hilary', label: 'Hilary' },
 *   ];
 *
 *   <Select items={TERMS} value={termId} onValueChange={setTermId}>
 *     <SelectTrigger aria-label="Term" placeholder="Choose a term" />
 *     <SelectContent>
 *       {TERMS.map((term) => (
 *         <SelectItem key={term.value} value={term.value}>{term.label}</SelectItem>
 *       ))}
 *     </SelectContent>
 *   </Select>
 *
 * `Select.Root` is generic over the value type, so it is re-exported as-is
 * rather than wrapped — wrapping would erase that inference.
 */

export const Select = BaseSelect.Root;
export const SelectGroup = BaseSelect.Group;

export interface SelectTriggerProps
  extends WithClassName<ComponentPropsWithRef<typeof BaseSelect.Trigger>> {
  placeholder?: ReactNode | undefined;
}

export function SelectTrigger({
  className,
  placeholder = 'Select an option',
  children,
  ...props
}: SelectTriggerProps) {
  return (
    <BaseSelect.Trigger
      className={cn(
        'flex h-10 w-full items-center justify-between gap-2 rounded-md border border-input',
        'bg-surface px-3 text-sm text-foreground transition-colors',
        'hover:bg-muted focus-visible:outline-2 focus-visible:outline-offset-2 focus-visible:outline-ring',
        'disabled:cursor-not-allowed disabled:opacity-60',
        'data-invalid:border-destructive',
        className,
      )}
      {...props}
    >
      {children ?? (
        <BaseSelect.Value
          className="truncate text-left data-placeholder:text-muted-foreground"
          placeholder={placeholder}
        />
      )}
      <BaseSelect.Icon className="shrink-0 text-muted-foreground">
        <ChevronDownIcon className="size-4" />
      </BaseSelect.Icon>
    </BaseSelect.Trigger>
  );
}

export interface SelectContentProps
  extends WithClassName<ComponentPropsWithRef<typeof BaseSelect.Popup>> {
  sideOffset?: number | undefined;
}

export function SelectContent({
  className,
  sideOffset = 6,
  children,
  ...props
}: SelectContentProps) {
  return (
    <BaseSelect.Portal>
      <BaseSelect.Positioner sideOffset={sideOffset} className="z-50 outline-none">
        <BaseSelect.Popup
          className={cn(
            'max-h-[min(24rem,var(--available-height))] min-w-(--anchor-width) overflow-y-auto',
            'rounded-lg border border-border bg-surface-raised p-1 text-surface-foreground shadow-lg',
            'transition-[opacity,transform] duration-150',
            'data-starting-style:scale-95 data-starting-style:opacity-0',
            'data-ending-style:scale-95 data-ending-style:opacity-0',
            className,
          )}
          {...props}
        >
          {children}
        </BaseSelect.Popup>
      </BaseSelect.Positioner>
    </BaseSelect.Portal>
  );
}

export type SelectItemProps = WithClassName<ComponentPropsWithRef<typeof BaseSelect.Item>>;

export function SelectItem({ className, children, ...props }: SelectItemProps) {
  return (
    <BaseSelect.Item
      className={cn(
        'relative flex cursor-default items-center gap-2 rounded-md py-2 pr-2 pl-8 text-sm',
        'select-none data-highlighted:bg-primary-subtle data-highlighted:text-primary-subtle-foreground',
        'data-disabled:pointer-events-none data-disabled:opacity-50',
        className,
      )}
      {...props}
    >
      <BaseSelect.ItemIndicator className="absolute left-2 flex items-center text-primary">
        <CheckIcon className="size-4" />
      </BaseSelect.ItemIndicator>
      <BaseSelect.ItemText className="truncate">{children}</BaseSelect.ItemText>
    </BaseSelect.Item>
  );
}

export function SelectGroupLabel({
  className,
  ...props
}: WithClassName<ComponentPropsWithRef<typeof BaseSelect.GroupLabel>>) {
  return (
    <BaseSelect.GroupLabel
      className={cn(
        'px-2 py-1.5 text-xs font-semibold tracking-wide text-muted-foreground uppercase',
        className,
      )}
      {...props}
    />
  );
}
