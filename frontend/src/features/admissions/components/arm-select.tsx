import { Select, SelectContent, SelectItem, SelectTrigger } from '@/components/ui/select';
import type { ArmDto } from '@/features/arms/types';

/**
 * Picks a real arm by its opaque id — never a free-text id (TASK-0064 AC).
 * Local to this dialog; not promoted to `@/features/arms/components/arm-
 * pickers.tsx` because that file picks a SESSION or LEVEL to scope arms by,
 * never an arm itself, so this is a genuinely new picker, not a duplicate.
 */
export function ArmSelect({
  value,
  onChange,
  arms,
}: {
  value: string;
  onChange: (value: string) => void;
  arms: ArmDto[];
}) {
  return (
    <Select
      items={arms.map((arm) => ({ value: arm.id, label: arm.displayName }))}
      value={value || null}
      onValueChange={(next) => onChange(next ?? '')}
    >
      <SelectTrigger aria-label="Arm" placeholder="Choose an arm" />
      <SelectContent>
        {arms.map((arm) => (
          <SelectItem key={arm.id} value={arm.id}>
            {arm.displayName}
          </SelectItem>
        ))}
      </SelectContent>
    </Select>
  );
}
