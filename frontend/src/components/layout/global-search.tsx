import { Autocomplete } from '@base-ui/react/autocomplete';
import { FileText, Search, User } from 'lucide-react';
import { useEffect, useMemo, useRef, useState } from 'react';
import { useNavigate } from 'react-router';
import { paths } from '@/app/router/paths';
import { usePupilSearch } from '@/features/pupils/api';
import { pupilName } from '@/features/pupils/types';
import { hasPrivilege, type AuthSession } from '@/lib/auth/auth-session';
import { visibleNavGroups } from './nav-config';

interface Result {
  key: string;
  kind: 'page' | 'pupil';
  label: string;
  detail: string;
  to: string;
}

const MIN_PUPIL_TERM = 2;

function useDebounced(value: string, ms: number): string {
  const [debounced, setDebounced] = useState(value);
  useEffect(() => {
    const timer = setTimeout(() => setDebounced(value), ms);
    return () => clearTimeout(timer);
  }, [value, ms]);
  return debounced;
}

/**
 * The header's app-wide search (human ruling 2026-09-26: pupils and pages): a pupil by name or registration number
 * (`pupil.view`), or any page the caller's menu offers. Ctrl+K (Cmd+K) focuses it. Base UI's Autocomplete owns the
 * combobox semantics and keyboard; the results are filtered here, so its own filter is off.
 */
export function GlobalSearch({ session }: { session: AuthSession }) {
  const navigate = useNavigate();
  const inputRef = useRef<HTMLInputElement>(null);
  const [term, setTerm] = useState('');
  const trimmed = term.trim();
  const debounced = useDebounced(trimmed, 250);
  const canSeePupils = hasPrivilege(session, 'pupil.view');

  useEffect(() => {
    const onKey = (event: KeyboardEvent) => {
      // Not while a modal is open: focus belongs inside it, and the input behind it is unreachable.
      if (document.querySelector('[role="dialog"][aria-modal="true"]')) return;
      if ((event.ctrlKey || event.metaKey) && event.key.toLowerCase() === 'k') {
        event.preventDefault();
        inputRef.current?.focus();
      }
    };
    window.addEventListener('keydown', onKey);
    return () => window.removeEventListener('keydown', onKey);
  }, []);

  const pupils = usePupilSearch(debounced, canSeePupils && debounced.length >= MIN_PUPIL_TERM);
  // Pupil results show only for the term actually in the box: never the previous term's during the debounce.
  const pupilsCurrent = debounced === trimmed && trimmed.length >= MIN_PUPIL_TERM;

  const results = useMemo<Result[]>(() => {
    if (!trimmed) return [];
    const needle = trimmed.toLowerCase();
    const pages: Result[] = visibleNavGroups(session)
      .flatMap((group) => group.items.map((item) => ({ item, group: group.label })))
      .filter(({ item, group }) => item.label.toLowerCase().includes(needle) || (group ?? '').toLowerCase().includes(needle))
      .slice(0, 5)
      .map(({ item, group }) => ({ key: `page:${item.to}`, kind: 'page', label: item.label, detail: group ?? 'Page', to: item.to }));
    const found: Result[] =
      pupilsCurrent
        ? (pupils.data?.items ?? []).map((pupil) => ({
            key: `pupil:${pupil.id}`,
            kind: 'pupil',
            label: pupilName(pupil),
            detail: pupil.registrationNumber ?? `${pupil.status} admission`,
            to: paths.pupilDetail(pupil.id),
          }))
        : [];
    // Pages first: they are known at once, so the pupils arriving later append below and never move the highlight.
    return [...pages, ...found];
  }, [trimmed, pupilsCurrent, session, pupils.data]);

  const searching = canSeePupils && trimmed.length >= MIN_PUPIL_TERM && (debounced !== trimmed || pupils.isFetching);

  return (
    <Autocomplete.Root
      items={results}
      filter={null}
      value={term}
      itemToStringValue={(result: Result) => result.key}
      autoHighlight
      onValueChange={(value, details) => {
        const chosen = details.reason === 'item-press' ? results.find((result) => result.key === value) : undefined;
        if (chosen) {
          setTerm('');
          inputRef.current?.blur();
          void navigate(chosen.to);
          return;
        }
        setTerm(value);
      }}
    >
      <div className="relative w-full max-w-md">
        <Search className="pointer-events-none absolute top-1/2 left-3 size-4 -translate-y-1/2 text-muted-foreground" aria-hidden="true" />
        <Autocomplete.Input
          ref={inputRef}
          aria-label="Search pupils and pages"
          placeholder={canSeePupils ? 'Search pupils or pages…' : 'Search pages…'}
          className="h-9 w-full rounded-md border border-input bg-background pr-14 pl-9 text-sm text-foreground placeholder:text-muted-foreground focus-visible:outline-2 focus-visible:outline-ring"
        />
        <kbd className="pointer-events-none absolute top-1/2 right-2 hidden -translate-y-1/2 rounded border border-border px-1.5 text-[10px] text-muted-foreground md:block">
          Ctrl K
        </kbd>
      </div>
      <Autocomplete.Portal>
        <Autocomplete.Positioner sideOffset={4} className="z-50 w-[var(--anchor-width)]">
          <Autocomplete.Popup className="max-h-80 overflow-y-auto rounded-lg border border-border bg-surface p-1 shadow-lg">
            {canSeePupils && pupils.isError && pupilsCurrent ? (
              <output className="block px-2 py-2 text-sm text-destructive">
                Pupil search failed. Check the connection and try again.
              </output>
            ) : null}
            <Autocomplete.Empty className="px-2 py-2 text-sm text-muted-foreground empty:hidden">
              {trimmed && !(pupils.isError && pupilsCurrent) ? (searching ? 'Searching…' : 'No matches.') : null}
            </Autocomplete.Empty>
            <Autocomplete.List>
              {(result: Result) => (
                <Autocomplete.Item
                  key={result.key}
                  value={result}
                  className="flex cursor-pointer items-center gap-3 rounded-md px-2 py-2 text-sm outline-none data-[highlighted]:bg-muted"
                >
                  {result.kind === 'pupil' ? (
                    <User className="size-4 shrink-0 text-muted-foreground" aria-hidden="true" />
                  ) : (
                    <FileText className="size-4 shrink-0 text-muted-foreground" aria-hidden="true" />
                  )}
                  <span className="min-w-0 flex-1 truncate text-foreground">{result.label}</span>
                  <span className="shrink-0 text-xs text-muted-foreground">{result.detail}</span>
                </Autocomplete.Item>
              )}
            </Autocomplete.List>
          </Autocomplete.Popup>
        </Autocomplete.Positioner>
      </Autocomplete.Portal>
    </Autocomplete.Root>
  );
}
