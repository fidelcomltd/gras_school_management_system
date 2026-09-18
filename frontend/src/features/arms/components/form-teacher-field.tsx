import { Select, SelectContent, SelectItem, SelectTrigger } from '@/components/ui/select';
import { Button } from '@/components/ui/button';
import { Field, FieldLabel } from '@/components/ui/field';
import { useAdminOptions } from '../hooks/use-admin-options';

const NONE = '';

/**
 * `EditArmDialog`'s form-teacher control. Never a free-text id field — see
 * `use-admin-options.ts`'s doc comment. Three cases:
 *
 * 1. `canResolve` (caller holds `admin.view`): a real `<Select>` of active
 *    admins by name, plus the currently-assigned id if it fell out of that
 *    list (suspended, or simply page 2+), so the picker never silently
 *    drops the existing selection.
 * 2. Not `canResolve`, someone IS assigned: the honest gap message plus a
 *    single "Clear form teacher" action — the only mutation of this field
 *    such a caller can make without a name to pick from.
 * 3. Not `canResolve`, no one assigned: nothing to show or clear.
 */
export function FormTeacherField({
  value,
  onChange,
  currentName,
  canResolve,
}: {
  value: string;
  onChange: (value: string) => void;
  currentName: string | undefined;
  canResolve: boolean;
}) {
  const options = useAdminOptions(canResolve);
  const admins = options.data?.items ?? [];

  if (canResolve) {
    const known = admins.some((a) => a.id === value);
    const items = [
      { value: NONE, label: '— none (clear) —' },
      ...(value !== NONE && !known ? [{ value, label: currentName ?? 'Currently assigned' }] : []),
      ...admins.map((a) => ({ value: a.id, label: a.staffName })),
    ];
    return (
      <Field>
        <FieldLabel>Form teacher</FieldLabel>
        <Select items={items} value={value} onValueChange={(next) => onChange(next ?? NONE)}>
          <SelectTrigger aria-label="Form teacher" />
          <SelectContent>
            {items.map((item) => (
              <SelectItem key={item.value || 'none'} value={item.value}>
                {item.label}
              </SelectItem>
            ))}
          </SelectContent>
        </Select>
      </Field>
    );
  }

  if (value === NONE) return null;

  return (
    <Field>
      <FieldLabel>Form teacher</FieldLabel>
      <p className="text-sm text-muted-foreground italic">
        {currentName ?? 'Assigned — name not visible to you'}
      </p>
      <Button type="button" variant="outline" size="sm" onClick={() => onChange(NONE)}>
        Clear form teacher
      </Button>
    </Field>
  );
}
