import { Select, SelectContent, SelectItem, SelectTrigger } from '@/components/ui/select';

/** A small labelled select for the results screens' class and subject choosers. */
export function LabelledSelect({
  label,
  value,
  options,
  onChange,
  placeholder,
  className = 'w-52',
}: {
  label: string;
  value: string;
  options: { value: string; label: string }[];
  onChange: (value: string) => void;
  placeholder: string;
  className?: string | undefined;
}) {
  return (
    <div className={className}>
      <Select items={options} value={value || null} onValueChange={(next) => next && onChange(next)}>
        <SelectTrigger aria-label={label} placeholder={placeholder} />
        <SelectContent>
          {options.map((option) => (
            <SelectItem key={option.value} value={option.value}>
              {option.label}
            </SelectItem>
          ))}
        </SelectContent>
      </Select>
    </div>
  );
}
