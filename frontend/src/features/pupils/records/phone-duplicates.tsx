import { useQueries } from '@tanstack/react-query';
import { TriangleAlert } from 'lucide-react';
import { Link } from 'react-router';
import { paths } from '@/app/router/paths';
import { fetchDuplicates, usePupil } from '../api';
import { pupilName, PupilsKeys, type PupilDto } from '../types';

/** The saved phones checked at most: a form has five contact slots, each with a phone and a WhatsApp number. */
const MAX_PHONES = 10;

/**
 * Spec 6.5.11 step 1's second half: once a contact phone is on a pending admission, any existing record with the same
 * surname and that phone is flagged, since it may be a returning pupil or a second admission for the same child. A
 * warning only: the office decides, and the check never blocks a save.
 */
export function PhoneDuplicateWarning({ pupilId, phones }: { pupilId: string; phones: (string | null | undefined)[] }) {
  const pupil = usePupil(pupilId);
  const record = pupil.data;
  const pending = record?.status === 'Pending';
  const unique = [...new Set(phones.filter((phone): phone is string => !!phone))].slice(0, MAX_PHONES);

  const results = useQueries({
    queries: unique.map((phone) => ({
      queryKey: [PupilsKeys.List, 'phone-duplicates', pupilId, phone],
      queryFn: () => fetchDuplicates(record?.surname ?? '', record?.firstName ?? '', record?.dateOfBirth ?? '', phone),
      enabled: pending && !!record,
      staleTime: 30_000,
    })),
  });

  const matches = new Map<string, PupilDto>();
  for (const result of results) for (const match of result.data ?? []) if (match.id !== pupilId) matches.set(match.id, match);
  if (!pending || matches.size === 0) return null;

  return (
    <output className="flex gap-3 rounded-md border border-warning/40 bg-warning-subtle p-3 text-sm">
      <TriangleAlert className="mt-0.5 size-4 shrink-0 text-warning" aria-hidden="true" />
      <div className="flex flex-col gap-1">
        <p className="font-medium text-foreground">A record with the same surname shares a contact phone or name and birthday</p>
        <p className="text-muted-foreground">It may be a returning pupil, who is reactivated rather than admitted again. Check before approving:</p>
        <ul className="flex flex-col gap-0.5">
          {[...matches.values()].map((match) => (
            <li key={match.id}>
              <Link to={paths.pupilDetail(match.id)} className="text-primary hover:underline">
                {pupilName(match)}
              </Link>{' '}
              <span className="text-muted-foreground">({match.registrationNumber ?? match.status})</span>
            </li>
          ))}
        </ul>
      </div>
    </output>
  );
}
