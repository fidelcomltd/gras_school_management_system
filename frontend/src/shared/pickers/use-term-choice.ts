import { useState } from 'react';
import { useSession, useSessions } from '@/features/sessions/api';
import type { SessionDto, TermDto } from '@/features/sessions/types';

export interface TermChoice {
  sessionId: string;
  termId: string;
  session: SessionDto | undefined;
  term: TermDto | undefined;
  sessions: SessionDto[];
  terms: TermDto[];
  isPending: boolean;
  setSessionId: (id: string) => void;
  setTermId: (id: string) => void;
}

/**
 * The session and term a term-scoped screen works on (subject mapping, mark entry, results). Defaults to the active
 * session and its active term; the admin's choice overrides. The defaults are derived on every render rather than
 * copied into state, so server data is never duplicated (CONVENTIONS.md §10).
 */
export function useTermChoice(): TermChoice {
  const [sessionChoice, setSessionChoice] = useState<string | null>(null);
  const [termChoice, setTermChoice] = useState<string | null>(null);
  const sessionsQuery = useSessions();
  const sessions = sessionsQuery.data?.pages.flatMap((page) => page.items) ?? [];
  const sessionId = sessionChoice ?? sessions.find((s) => s.state === 'Active')?.id ?? sessions[0]?.id ?? '';
  const detail = useSession(sessionId);
  const terms = [...(detail.data?.terms ?? [])].sort((a, b) => Number(a.ordinal) - Number(b.ordinal));
  const termId =
    termChoice !== null && terms.some((t) => t.id === termChoice)
      ? termChoice
      : (terms.find((t) => t.state === 'Active')?.id ?? terms[0]?.id ?? '');

  return {
    sessionId,
    termId,
    session: sessions.find((s) => s.id === sessionId),
    term: terms.find((t) => t.id === termId),
    sessions,
    terms,
    isPending: sessionsQuery.isPending || (sessionId !== '' && detail.isPending),
    setSessionId: (id) => {
      setSessionChoice(id);
      setTermChoice(null);
    },
    setTermId: setTermChoice,
  };
}
