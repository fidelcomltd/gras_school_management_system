import { Select, SelectContent, SelectItem, SelectTrigger } from '@/components/ui/select';
import type { TermChoice } from './use-term-choice';

/** Session and term selects for a `useTermChoice` value. */
export function TermPicker({ choice }: { choice: TermChoice }) {
  const sessionItems = choice.sessions.map((s) => ({ value: s.id, label: s.name }));
  const termItems = choice.terms.map((t) => ({ value: t.id, label: `${t.name} (${t.state})` }));

  return (
    <div className="flex flex-wrap gap-3">
      <div className="w-40">
        <Select items={sessionItems} value={choice.sessionId || null} onValueChange={(next) => next && choice.setSessionId(next)}>
          <SelectTrigger aria-label="Session" placeholder="Session" />
          <SelectContent>
            {sessionItems.map((item) => (
              <SelectItem key={item.value} value={item.value}>
                {item.label}
              </SelectItem>
            ))}
          </SelectContent>
        </Select>
      </div>
      <div className="w-56">
        <Select items={termItems} value={choice.termId || null} onValueChange={(next) => next && choice.setTermId(next)}>
          <SelectTrigger aria-label="Term" placeholder="Term" />
          <SelectContent>
            {termItems.map((item) => (
              <SelectItem key={item.value} value={item.value}>
                {item.label}
              </SelectItem>
            ))}
          </SelectContent>
        </Select>
      </div>
    </div>
  );
}
