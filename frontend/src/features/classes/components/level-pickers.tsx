import { Select, SelectContent, SelectItem, SelectTrigger } from '@/components/ui/select';
import type { LevelDto, SectionDto } from '../types';

/** Shared `<Select>` wiring for a section/level picker — used by both the create and edit dialogs. */
export function SectionSelect({
  value,
  onChange,
  sections,
}: {
  value: string;
  onChange: (value: string) => void;
  sections: SectionDto[];
}) {
  return (
    <Select
      items={sections.map((s) => ({ value: s.id, label: s.name }))}
      value={value || null}
      onValueChange={(next) => onChange(next ?? '')}
    >
      <SelectTrigger aria-label="Section" placeholder="Choose a section" />
      <SelectContent>
        {sections.map((s) => (
          <SelectItem key={s.id} value={s.id}>
            {s.name}
          </SelectItem>
        ))}
      </SelectContent>
    </Select>
  );
}

/** A level picker, optionally offering a "none" choice (for `nextLevelId`, the graduating case). */
export function LevelSelect({
  value,
  onChange,
  levels,
  label,
  noneLabel,
}: {
  value: string;
  onChange: (value: string) => void;
  levels: LevelDto[];
  label: string;
  noneLabel?: string | undefined;
}) {
  const items = noneLabel ? [{ value: '', label: noneLabel }, ...levels.map((l) => ({ value: l.id, label: l.name }))] : levels.map((l) => ({ value: l.id, label: l.name }));

  return (
    <Select items={items} value={noneLabel ? value : value || null} onValueChange={(next) => onChange(next ?? '')}>
      <SelectTrigger aria-label={label} placeholder={noneLabel ? undefined : 'Choose a level'} />
      <SelectContent>
        {noneLabel ? <SelectItem value="">{noneLabel}</SelectItem> : null}
        {levels.map((l) => (
          <SelectItem key={l.id} value={l.id}>
            {l.name}
          </SelectItem>
        ))}
      </SelectContent>
    </Select>
  );
}
