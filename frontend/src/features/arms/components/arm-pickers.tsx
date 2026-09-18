import { Select, SelectContent, SelectItem, SelectTrigger } from '@/components/ui/select';
import type { LevelDto } from '@/features/classes/types';
import type { SessionDto } from '@/features/sessions/types';
import type { ArmStatus } from '../types';

/**
 * Arms-local pickers, mirroring `features/classes/components/level-pickers.tsx`'s
 * shape. Not promoted to `src/shared/` — arms is the only consumer so far
 * (CONVENTIONS.md §4: promote on the second genuine consumer, not before).
 */

export function SessionSelect({
  value,
  onChange,
  sessions,
  label = 'Session',
}: {
  value: string;
  onChange: (value: string) => void;
  sessions: SessionDto[];
  label?: string;
}) {
  return (
    <Select
      items={sessions.map((s) => ({ value: s.id, label: s.name }))}
      value={value || null}
      onValueChange={(next) => onChange(next ?? '')}
    >
      <SelectTrigger aria-label={label} placeholder="Choose a session" />
      <SelectContent>
        {sessions.map((s) => (
          <SelectItem key={s.id} value={s.id}>
            {s.name}
          </SelectItem>
        ))}
      </SelectContent>
    </Select>
  );
}

export function ArmLevelSelect({
  value,
  onChange,
  levels,
  label = 'Level',
}: {
  value: string;
  onChange: (value: string) => void;
  levels: LevelDto[];
  label?: string;
}) {
  return (
    <Select
      items={levels.map((l) => ({ value: l.id, label: l.name }))}
      value={value || null}
      onValueChange={(next) => onChange(next ?? '')}
    >
      <SelectTrigger aria-label={label} placeholder="Choose a level" />
      <SelectContent>
        {levels.map((l) => (
          <SelectItem key={l.id} value={l.id}>
            {l.name}
          </SelectItem>
        ))}
      </SelectContent>
    </Select>
  );
}

const STATUS_ITEMS: { value: ArmStatus | ''; label: string }[] = [
  { value: '', label: 'Any status' },
  { value: 'Active', label: 'Active' },
  { value: 'Inactive', label: 'Inactive' },
  { value: 'Closed', label: 'Closed' },
];

export function ArmStatusSelect({
  value,
  onChange,
  label = 'Status',
}: {
  value: ArmStatus | '';
  onChange: (value: ArmStatus | '') => void;
  label?: string;
}) {
  return (
    <Select
      items={STATUS_ITEMS}
      value={value}
      onValueChange={(next: ArmStatus | '' | null) => onChange(next ?? '')}
    >
      <SelectTrigger aria-label={label} />
      <SelectContent>
        {STATUS_ITEMS.map((item) => (
          <SelectItem key={item.value} value={item.value}>
            {item.label}
          </SelectItem>
        ))}
      </SelectContent>
    </Select>
  );
}
